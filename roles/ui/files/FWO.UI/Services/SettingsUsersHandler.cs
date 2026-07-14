using System.Net;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Services;
using RestSharp;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Encapsulates the data access and business logic of the settings users page,
    /// keeping the Razor component limited to presentation.
    /// </summary>
    public class SettingsUsersHandler(
        ApiConnection apiConnection,
        MiddlewareClient middlewareClient,
        UserConfig userConfig,
        GlobalConfig globalConfig,
        Action<Exception?, string, string, bool> displayMessageInUi)
    {
        public List<UiLdapConnection> ConnectedLdaps { get; set; } = [];
        public List<UiLdapConnection> WritableLdaps { get; set; } = [];
        public UiLdapConnection InternalLdap { get; set; } = new();

        public List<UiUser> LdapUsers { get; set; } = [];
        public List<UiUser> UiUsers { get; set; } = [];
        public List<UiUser> SampleUsers { get; set; } = [];
        public List<Tenant> Tenants { get; set; } = [];
        public List<Tenant> AvailableTenants { get; set; } = [];
        public List<UserGroup> Groups { get; set; } = [];
        public List<Role> Roles { get; set; } = [];
        public List<Role> AvailableRoles { get; set; } = [];

        public bool EditMode { get; set; } = false;
        public bool DeleteMode { get; set; } = false;
        public bool SampleRemoveMode { get; set; } = false;
        public bool SampleRemoveAllowed { get; set; } = false;
        public bool AddMode { get; set; } = false;
        public bool ShowSampleRemoveButton { get; set; } = false;
        public bool ResetPasswordMode { get; set; } = false;

        public UiUser NewUser { get; set; } = new();
        public UiUser ActUser { get; set; } = new();
        public UiLdapConnection? SelectedLdap { get; set; }
        public Tenant? SelectedTenant { get; set; }
        public Role? SelectedRole { get; set; }
        public UserGroup? SelectedGroup { get; set; }

        public string DeleteMessage { get; set; } = "";
        public string SampleRemoveMessage { get; set; } = "";
        public bool WorkInProgress { get; set; } = false;

        /// <summary>
        /// Loads users, tenants, groups and roles for the initial page display.
        /// </summary>
        public async Task Init()
        {
            if (await FetchFromDb())
            {
                await SynchronizeGroupsAndRoles();
            }
        }

        private async Task<bool> FetchFromDb()
        {
            try
            {
                ConnectedLdaps = await apiConnection.SendQueryAsync<List<UiLdapConnection>>(AuthQueries.getLdapConnections);
                WritableLdaps = ConnectedLdaps.FindAll(x => x.IsWritable());
                InternalLdap = ConnectedLdaps.FirstOrDefault(x => x.IsInternal()) ?? throw new KeyNotFoundException(userConfig.GetText("E5207"));

                // Get the tenants
                Tenants = await apiConnection.SendQueryAsync<List<Tenant>>(AuthQueries.getTenants);

                // Get users from uiusers table
                RestResponse<List<UserGetReturnParameters>> middlewareServerResponse = await middlewareClient.GetUsers();
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == null)
                {
                    displayMessageInUi(null, userConfig.GetText("fetch_users_local"), userConfig.GetText("E5209"), true);
                }
                else
                {
                    UiUsers = MapApiUsersToUiUsers(middlewareServerResponse.Data, ConnectedLdaps, Tenants);
                    AnalyseSampleUsers();
                }
                return true;
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
                return false;
            }
        }

        /// <summary>
        /// Maps the users returned by the middleware to UI users, resolving their LDAP
        /// connection and tenant name through id lookups instead of repeated linear scans.
        /// </summary>
        /// <param name="apiUsers">Users as returned by the middleware.</param>
        /// <param name="connectedLdaps">All known LDAP connections.</param>
        /// <param name="tenants">All known tenants.</param>
        /// <returns>The mapped UI users.</returns>
        public static List<UiUser> MapApiUsersToUiUsers(List<UserGetReturnParameters> apiUsers, List<UiLdapConnection> connectedLdaps, List<Tenant> tenants)
        {
            Dictionary<int, UiLdapConnection> ldapsById = connectedLdaps
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());
            Dictionary<int, string> tenantNamesById = tenants
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            List<UiUser> mappedUsers = [];
            foreach (UserGetReturnParameters apiUser in apiUsers)
            {
                UiUser user = new(apiUser);
                user.LdapConnection = ldapsById.TryGetValue(user.LdapConnection.Id, out UiLdapConnection? ldap)
                    ? ldap
                    : throw new ArgumentNullException(nameof(apiUsers));
                if (user.Tenant != null)
                {
                    user.Tenant.Name = tenantNamesById.TryGetValue(user.Tenant.Id, out string? tenantName)
                        ? tenantName
                        : throw new ArgumentNullException(nameof(apiUsers));
                }
                mappedUsers.Add(user);
            }
            return mappedUsers;
        }

        private void AnalyseSampleUsers()
        {
            SampleUsers = [.. UiUsers.Where(u => u.Name.EndsWith(GlobalConst.k_demo))];
            ShowSampleRemoveButton = (SampleUsers.Count > 0);
        }

        private async Task SynchronizeGroupsAndRoles()
        {
            // get groups from internal ldap
            await GetGroupsFromInternalLdap();
            SynchronizeUsersToGroups();

            // get roles from internal ldap
            await GetRolesFromInternalLdap();
            SynchronizeUsersToRoles();

            AvailableRoles = Roles.Where(x => (x.Name != Basics.Roles.Anonymous && x.Name != Basics.Roles.MiddlewareServer)).OrderBy(x => x.Name).ToList();
        }

        private void SynchronizeUsersToGroups()
        {
            try
            {
                AssignGroupsToUsers(UiUsers, Groups);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("sync_users"), "", true);
            }
        }

        private void SynchronizeUsersToRoles()
        {
            try
            {
                AssignRolesToUsers(UiUsers, Roles);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("sync_users"), "", true);
            }
        }

        /// <summary>
        /// Assigns each group's name to its member users, resolving membership in
        /// O(users + memberships) via a normalized-DN lookup.
        /// </summary>
        /// <param name="users">Users whose group memberships are recomputed.</param>
        /// <param name="groups">Groups holding their member users.</param>
        public static void AssignGroupsToUsers(List<UiUser> users, List<UserGroup> groups)
        {
            foreach (UiUser user in users)
            {
                user.Groups = [];
            }
            Dictionary<string, List<UiUser>> usersByDn = BuildUsersByNormalizedDn(users);
            foreach (UserGroup group in groups)
            {
                if (group.Users == null)
                {
                    continue;
                }
                foreach (UiUser member in group.Users)
                {
                    if (usersByDn.TryGetValue(DistName.NormalizeDnForComparison(member.Dn), out List<UiUser>? matchingUsers))
                    {
                        foreach (UiUser user in matchingUsers)
                        {
                            user.Groups.Add(group.Name);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Assigns each role's name to its member users, resolving membership in
        /// O(users + memberships) via a normalized-DN lookup.
        /// </summary>
        /// <param name="users">Users whose role memberships are recomputed.</param>
        /// <param name="roles">Roles holding their member users.</param>
        public static void AssignRolesToUsers(List<UiUser> users, List<Role> roles)
        {
            foreach (UiUser user in users)
            {
                user.Roles = [];
            }
            Dictionary<string, List<UiUser>> usersByDn = BuildUsersByNormalizedDn(users);
            foreach (Role role in roles)
            {
                if (role.Users == null)
                {
                    continue;
                }
                foreach (UiUser member in role.Users)
                {
                    if (usersByDn.TryGetValue(DistName.NormalizeDnForComparison(member.Dn), out List<UiUser>? matchingUsers))
                    {
                        foreach (UiUser user in matchingUsers)
                        {
                            user.Roles.Add(role.Name);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a lookup of UI users keyed by their normalized distinguished name.
        /// This allows resolving group and role memberships in O(memberships) instead of
        /// O(users × memberships), and normalizes each user DN only once.
        /// </summary>
        /// <param name="users">Users to index by their normalized DN.</param>
        /// <returns>Dictionary mapping a normalized DN to all users sharing that DN.</returns>
        public static Dictionary<string, List<UiUser>> BuildUsersByNormalizedDn(List<UiUser> users)
        {
            Dictionary<string, List<UiUser>> usersByDn = [];
            foreach (UiUser user in users)
            {
                string normalizedDn = DistName.NormalizeDnForComparison(user.Dn);
                if (string.IsNullOrEmpty(normalizedDn))
                {
                    continue;
                }
                if (!usersByDn.TryGetValue(normalizedDn, out List<UiUser>? matchingUsers))
                {
                    matchingUsers = [];
                    usersByDn[normalizedDn] = matchingUsers;
                }
                matchingUsers.Add(user);
            }
            return usersByDn;
        }

        /// <summary>
        /// Re-reads all data and synchronizes the internal LDAP users into the local store.
        /// </summary>
        public async Task Resynchronize()
        {
            await FetchFromDb();

            // Get all users from internal ldap
            await GetUsersFromInternalLdap();

            // Synchronize both
            await SynchronizeUsers();

            await SynchronizeGroupsAndRoles();

            Log.WriteAudit(
                Title: $"Users Settings",
                Text: $"LDAP re-sync started",
                UserName: userConfig.User.Name,
                UserDN: userConfig.User.Dn);
        }

        private async Task SynchronizeUsers()
        {
            try
            {
                foreach (UiUser ldapUser in LdapUsers)
                {
                    UiUser? relatedUiUser = UiUsers.FirstOrDefault(x => DistName.DnEquals(x.Dn, ldapUser.Dn));
                    if (relatedUiUser != null)
                    {
                        // Update related user
                        if (relatedUiUser.Email != ldapUser.Email)
                        {
                            relatedUiUser.Email = ldapUser.Email;
                            await UpdateUserInDb(relatedUiUser);
                        }
                        if (relatedUiUser.Firstname != ldapUser.Firstname)
                        {
                            relatedUiUser.Firstname = ldapUser.Firstname;
                            await UpdateUserInDb(relatedUiUser);
                        }
                        if (relatedUiUser.Lastname != ldapUser.Lastname)
                        {
                            relatedUiUser.Lastname = ldapUser.Lastname;
                            await UpdateUserInDb(relatedUiUser);
                        }
                    }
                    else
                    {
                        // Add new user to UiUsers table
                        await AddUserToDb(ldapUser);
                        UiUsers.Add(ldapUser);
                    }
                }
                // Todo: CleanUpUser for deleted users?
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("sync_users"), "", true);
            }
        }

        private async Task GetUsersFromInternalLdap()
        {
            try
            {
                LdapUsers.Clear();

                // get users from Ldap
                LdapUserGetParameters userGetParameters = new() { LdapId = InternalLdap.Id, SearchPattern = "" };
                RestResponse<List<LdapUserGetReturnParameters>> middlewareServerResponse = await middlewareClient.GetLdapUsers(userGetParameters);
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == null)
                {
                    displayMessageInUi(null, userConfig.GetText("get_user_from_ldap"), userConfig.GetText("E5208"), true);
                }
                else
                {
                    foreach (LdapUserGetReturnParameters user in middlewareServerResponse.Data)
                    {
                        DistName distname = new(user.UserDn);
                        UiUser newLdapUser = new()
                        {
                            Dn = user.UserDn,
                            Name = distname.UserName,
                            Email = user.Email,
                            Firstname = user.Firstname,
                            Lastname = user.Lastname,
                            PasswordMustBeChanged = false,
                            LdapConnection = InternalLdap
                        };
                        string tenantName = distname.GetTenantNameViaLdapTenantLevel(InternalLdap.TenantLevel);
                        if (tenantName != "")
                        {
                            newLdapUser.Tenant = new Tenant() { Name = tenantName };
                        }
                        LdapUsers.Add(newLdapUser);
                    }
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("fetch_users_ldap"), "", true);
            }
        }

        private async Task GetGroupsFromInternalLdap()
        {
            try
            {
                Groups = await GroupAccess.GetGroupsFromInternalLdap(middlewareClient, userConfig, displayMessageInUi);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("fetch_groups"), "", true);
            }
        }

        private async Task GetRolesFromInternalLdap()
        {
            try
            {
                Roles = await RoleAccess.GetRolesFromInternalLdap(middlewareClient);
                if (Roles.Count == 0)
                {
                    displayMessageInUi(null, userConfig.GetText("fetch_roles"), userConfig.GetText("E5251"), true);
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("fetch_roles"), "", true);
            }
        }

        private async Task UpdateUserInDb(UiUser user)
        {
            try
            {
                var Variables = new
                {
                    id = user.DbId,
                    email = user.Email
                };
                await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserEmail, Variables);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("update_user_local"), "", false);
            }
        }

        private async Task AddUserToDb(UiUser user)
        {
            try
            {
                Tenant? actTenant = Tenants.FirstOrDefault(x => x.Name == user.Tenant?.Name);
                if (user.Tenant != null && actTenant != null)
                {
                    user.Tenant.Id = actTenant.Id;
                }

                var Variables = new
                {
                    uuid = user.Dn,
                    uiuser_username = user.Name,
                    email = user.Email,
                    uiuser_first_name = user.Firstname,
                    uiuser_last_name = user.Lastname,
                    tenant = (user.Tenant != null ? user.Tenant.Id : (int?)null),
                    passwordMustBeChanged = user.PasswordMustBeChanged,
                    ldapConnectionId = user.LdapConnection.Id
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(AuthQueries.upsertUiUser, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    user.DbId = returnIds[0].NewId;
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("add_user_local"), "", false);
            }
        }

        /// <summary>
        /// Updates the available tenants for the currently selected LDAP connection.
        /// </summary>
        /// <param name="newLdap">Newly selected LDAP connection.</param>
        public void SetAvailableTenants(UiLdapConnection? newLdap)
        {
            try
            {
                SelectedLdap = newLdap;
                AvailableTenants = GetAvailableTenants(SelectedLdap, Tenants);
                if (SelectedTenant != null && !AvailableTenants.Contains(SelectedTenant) && AvailableTenants.Count > 0)
                {
                    SelectedTenant = AvailableTenants.First();
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("get_tenant_data"), "", false);
            }
        }

        /// <summary>
        /// Determines which tenants can be selected for the given LDAP connection.
        /// </summary>
        /// <param name="selectedLdap">Selected LDAP connection, or null.</param>
        /// <param name="tenants">All known tenants.</param>
        /// <returns>The selectable tenants for the connection.</returns>
        public static List<Tenant> GetAvailableTenants(UiLdapConnection? selectedLdap, List<Tenant> tenants)
        {
            List<Tenant> availableTenants = [];
            if (selectedLdap != null)
            {
                if (selectedLdap.TenantId != null && selectedLdap.TenantId != 0)
                {
                    Tenant? ten = tenants.FirstOrDefault(x => x.Id == selectedLdap.TenantId);
                    if (ten != null)
                    {
                        availableTenants.Add(ten);
                    }
                }
                else if (selectedLdap.TenantLevel > 0)
                {
                    availableTenants = tenants;
                }
            }
            return availableTenants;
        }

        /// <summary>
        /// Prepares the handler state for adding a brand-new user.
        /// </summary>
        public void AddUser()
        {
            try
            {
                AddMode = true;
                NewUser = new UiUser() { Email = "" };
                SelectedLdap = WritableLdaps.FirstOrDefault();
                SetAvailableTenants(SelectedLdap);
                SelectedTenant = AvailableTenants.FirstOrDefault();
                SelectedGroup = null;
                NewUser.Groups = [];
                SelectedRole = null;
                NewUser.Roles = [];
                Edit(NewUser);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("add_user"), "", false);
            }
        }

        /// <summary>
        /// Prepares the handler state for cloning an existing user.
        /// </summary>
        /// <param name="user">User to clone.</param>
        public void Clone(UiUser user)
        {
            try
            {
                AddMode = true;
                NewUser = new UiUser(user);
                NewUser.Password = "";
                NewUser.Firstname = user.Firstname;
                NewUser.Lastname = user.Lastname;
                NewUser.Email = user.Email;
                SelectedLdap = (NewUser.LdapConnection != null ? NewUser.LdapConnection : WritableLdaps.FirstOrDefault());
                SetAvailableTenants(SelectedLdap);
                SelectedTenant = NewUser.Tenant;
                SelectedGroup = (NewUser.Groups != null ? Groups.FirstOrDefault(x => x.Name == NewUser.Groups.FirstOrDefault()) : null);
                NewUser.Groups = [];
                SelectedRole = (NewUser.Roles != null ? Roles.FirstOrDefault(x => x.Name == NewUser.Roles.FirstOrDefault()) : null);
                NewUser.Roles = [];
                Edit(NewUser);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("add_user"), "", false);
            }
        }

        /// <summary>
        /// Opens the edit dialog for the given user.
        /// </summary>
        /// <param name="user">User to edit.</param>
        public void Edit(UiUser user)
        {
            ActUser = new UiUser(user);
            ActUser.LdapConnection ??= new UiLdapConnection();
            if (ActUser.Tenant != null && ActUser.Tenant.Id == GlobalConst.kTenant0Id && ActUser.LdapConnection.GlobalTenantName != null && ActUser.LdapConnection.GlobalTenantName != "")
            {
                ActUser.Tenant.Name = ActUser.LdapConnection.GlobalTenantName;
            }
            EditMode = true;
        }

        private void SynchronizeUserData(UiUser user)
        {
            if (SelectedLdap != null)
            {
                user.LdapConnection = SelectedLdap;
                user.Tenant = (SelectedLdap.TenantLevel > 0 ? SelectedTenant : null);
                user.Dn = BuildUserDn(user.Name, SelectedLdap, SelectedTenant);
            }
            user.Groups = [];
            if (SelectedGroup != null)
            {
                user.Groups.Add(SelectedGroup.Name);
            }
            user.PasswordMustBeChanged = true;
            user.Roles = [];
            if (SelectedRole != null)
            {
                user.Roles.Add(SelectedRole.Name);
                if (SelectedRole.Name == Basics.Roles.Auditor)
                {
                    user.PasswordMustBeChanged = false;
                }
            }
        }

        /// <summary>
        /// Builds the distinguished name for a user within the given LDAP connection and tenant.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="selectedLdap">LDAP connection the user is created in.</param>
        /// <param name="selectedTenant">Tenant of the user, or null.</param>
        /// <returns>The constructed distinguished name.</returns>
        public static string BuildUserDn(string userName, UiLdapConnection selectedLdap, Tenant? selectedTenant)
        {
            string namePrefix = selectedLdap.Type == (int)LdapType.ActiveDirectory ? "cn=" : "uid=";
            string tenantPart = "";
            if (selectedLdap.TenantLevel > 0)
            {
                string tenantName = selectedTenant?.Id == GlobalConst.kTenant0Id && selectedLdap.GlobalTenantName != null && selectedLdap.GlobalTenantName != ""
                    ? selectedLdap.GlobalTenantName
                    : selectedTenant?.Name ?? "";
                tenantPart = ",ou=" + tenantName;
            }
            return namePrefix + userName + tenantPart + "," + selectedLdap.UserSearchPath;
        }

        /// <summary>
        /// Persists the user currently being added or edited.
        /// </summary>
        public async Task Save()
        {
            try
            {
                if (ActUser.Sanitize())
                {
                    displayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("U0001"), true);
                }
                if (AddMode)
                {
                    await SaveNewUser();
                }
                else
                {
                    await SaveExistingUser();
                }
                AnalyseSampleUsers();
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("save_user"), "", true);
            }
        }

        private async Task SaveNewUser()
        {
            SynchronizeUserData(ActUser);
            if (!CheckValues())
            {
                return;
            }
            // insert new user to ldap
            UserAddParameters parameters = new()
            {
                Email = ActUser.Email,
                Firstname = ActUser.Firstname,
                Lastname = ActUser.Lastname,
                LdapId = ActUser.LdapConnection.Id,
                Password = ActUser.Password,
                UserDn = ActUser.Dn,
                TenantId = (ActUser.Tenant != null ? ActUser.Tenant.Id : 0),
                PwChangeRequired = ActUser.PasswordMustBeChanged
            };
            RestResponse<int> middlewareServerResponse = await middlewareClient.AddUser(parameters);
            if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == 0)
            {
                displayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5213"), true);
                return;
            }
            ActUser.DbId = middlewareServerResponse.Data;
            UiUsers.Add(ActUser);
            await AddUserToGroupsInLdap(ActUser);
            await AddUserToRolesInLdap(ActUser);
            AddMode = false;
            EditMode = false;

            Log.WriteAudit(
                Title: $"Users Settings",
                Text: $"Added User: {ActUser.Name} (DN: {ActUser.Dn})",
                UserName: userConfig.User.Name,
                UserDN: userConfig.User.Dn);
        }

        private async Task SaveExistingUser()
        {
            // Update existing user in ldap --> currently only email; TODO: add first and last name
            UserEditParameters parameters = new() { Email = ActUser.Email, LdapId = ActUser.LdapConnection.Id, UserId = ActUser.DbId };
            RestResponse<bool> middlewareServerResponse = await middlewareClient.UpdateUser(parameters);
            if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == false)
            {
                displayMessageInUi(null, userConfig.GetText("update_user"), userConfig.GetText("E5214"), true);
                return;
            }
            UiUsers[UiUsers.FindIndex(x => x.DbId == ActUser.DbId)].Email = ActUser.Email;
            EditMode = false;

            Log.WriteAudit(
                Title: $"Users Settings",
                Text: $"Edited User: {ActUser.Name} (DN: {ActUser.Dn})",
                UserName: userConfig.User.Name,
                UserDN: userConfig.User.Dn);
        }

        private bool CheckValues()
        {
            if (ActUser.Name == null || ActUser.Name == "" || ActUser.Password == null || ActUser.Password == "" || ActUser.Roles.Count == 0)
            {
                displayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5211"), true);
                return false;
            }
            if (!PasswordPolicy.CheckPolicy(ActUser.Password, globalConfig, userConfig, out string errorMsg))
            {
                displayMessageInUi(null, userConfig.GetText("add_user"), errorMsg, true);
                return false;
            }
            if (ActUser.LdapConnection.TenantLevel > 0 && (ActUser.Tenant == null || !Tenants.Exists(x => x.Name == ActUser.Tenant.Name)))
            {
                displayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5212"), true);
                return false;
            }
            if (UiUsers.Exists(x => DistName.DnEquals(x.Dn, ActUser.Dn)))
            {
                displayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5210"), true);
                return false;
            }
            return true;
        }

        private async Task AddUserToGroupsInLdap(UiUser user)
        {
            try
            {
                foreach (string groupName in user.Groups)
                {
                    UserGroup? group = Groups.FirstOrDefault(x => x.Name == groupName);
                    if (group != null)
                    {
                        GroupAddDeleteUserParameters addUserParameters = new() { GroupDn = group.Dn, UserDn = user.Dn };
                        RestResponse<bool> middlewareServerResponse = await middlewareClient.AddUserToGroup(addUserParameters);
                        if ((middlewareServerResponse.StatusCode != HttpStatusCode.OK) || (middlewareServerResponse.Data == false))
                        {
                            displayMessageInUi(null, userConfig.GetText("assign_user_to_group"), userConfig.GetText("E5242"), true);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("assign_user_to_group"), "", true);
            }
        }

        private async Task AddUserToRolesInLdap(UiUser user)
        {
            try
            {
                foreach (string roleName in user.Roles)
                {
                    Role? role = Roles.FirstOrDefault(x => x.Name == roleName);
                    if (role != null)
                    {
                        RoleAddDeleteUserParameters parameters = new() { Role = role.Dn, UserDn = user.Dn };
                        RestResponse<bool> middlewareServerResponse = await middlewareClient.AddUserToRole(parameters);
                        if ((middlewareServerResponse.StatusCode != HttpStatusCode.OK) || (middlewareServerResponse.Data == false))
                        {
                            displayMessageInUi(null, userConfig.GetText("assign_user_group_to_role"), userConfig.GetText("E5255"), true);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("assign_user_group_to_role"), "", true);
            }
        }

        /// <summary>
        /// Validates and opens the delete confirmation for the given user.
        /// </summary>
        /// <param name="user">User to delete.</param>
        public void RequestDelete(UiUser user)
        {
            try
            {
                ActUser = user;
                if (DistName.DnEquals(ActUser.Dn, userConfig.User.Dn))
                {
                    displayMessageInUi(null, userConfig.GetText("delete_user"), userConfig.GetText("E5215"), true);
                }
                else
                {
                    DeleteMessage = userConfig.GetText("U5201") + ActUser.Name + "?";
                    if (!ActUser.IsInternal())
                    {
                        DeleteMessage += userConfig.GetText("U5202");
                    }
                    DeleteMode = true;
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("delete_user"), "", true);
            }
        }

        /// <summary>
        /// Deletes the user currently selected for deletion.
        /// </summary>
        public async Task Delete()
        {
            try
            {
                // delete user from Ldap
                UserDeleteParameters parameters = new()
                {
                    LdapId = ActUser.LdapConnection.Id,
                    UserId = ActUser.DbId
                };
                RestResponse<bool> middlewareServerResponse = await middlewareClient.DeleteUser(parameters);
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == false)
                {
                    displayMessageInUi(null, userConfig.GetText("delete_user"), userConfig.GetText("E5216"), true);
                }
                else
                {
                    UiUsers.Remove(ActUser);
                    DeleteMode = false;

                    Log.WriteAudit(
                        Title: $"Users Settings",
                        Text: $"Deleted User: {ActUser.Name} (DN: {ActUser.Dn})",
                        UserName: userConfig.User.Name,
                        UserDN: userConfig.User.Dn);
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("delete_user"), "", true);
            }
        }

        private async Task CleanUpUser(UiUser user)
        {
            try
            {
                var Variables = new { id = user.DbId };
                int delId = (await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.deleteUser, Variables)).DeletedId;
                if (delId == user.DbId)
                {
                    UiUsers.Remove(user);
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("delete_user_local"), "", false);
            }
        }

        /// <summary>
        /// Validates and opens the password reset dialog for the given user.
        /// </summary>
        /// <param name="user">User whose password should be reset.</param>
        public void RequestResetPassword(UiUser user)
        {
            if (!(new DistName(user.Dn)).IsInternal())
            {
                displayMessageInUi(null, userConfig.GetText("reset_password"), userConfig.GetText("E5217"), true);
            }
            else
            {
                ActUser = user;
                ActUser.Password = "";
                ResetPasswordMode = true;
            }
        }

        /// <summary>
        /// Resets the password of the user currently selected for password reset.
        /// </summary>
        public async Task ResetPassword()
        {
            try
            {
                string errorMsg = "";
                if (ActUser.Password == null || ActUser.Password == "")
                {
                    displayMessageInUi(null, userConfig.GetText("reset_password"), userConfig.GetText("E5218"), true);
                }
                else if (!PasswordPolicy.CheckPolicy(ActUser.Password, globalConfig, userConfig, out errorMsg))
                {
                    displayMessageInUi(null, userConfig.GetText("reset_password"), errorMsg, true);
                }
                else
                {
                    UserResetPasswordParameters parameters = new()
                    {
                        LdapId = ActUser.LdapConnection.Id,
                        NewPassword = ActUser.Password,
                        UserId = ActUser.DbId
                    };
                    RestResponse<string> middlewareServerResponse = await middlewareClient.SetPassword(parameters);
                    if (middlewareServerResponse.StatusCode != HttpStatusCode.OK)
                    {
                        displayMessageInUi(null, userConfig.GetText("reset_password"), userConfig.GetText("E5219"), true);
                    }
                    else
                    {
                        if (middlewareServerResponse.Data != null)
                        {
                            errorMsg = middlewareServerResponse.Data;
                        }
                        if (errorMsg != "")
                        {
                            displayMessageInUi(null, userConfig.GetText("reset_password"), errorMsg, true);
                        }

                        Log.WriteAudit(
                            Title: $"Users Settings",
                            Text: $"Password reset User: {ActUser.Name} (DN: {ActUser.Dn})",
                            UserName: userConfig.User.Name,
                            UserDN: userConfig.User.Dn);
                    }
                    ResetPasswordMode = false;
                }
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("reset_password"), "", true);
            }
        }

        /// <summary>
        /// Prepares the confirmation for removing the seeded sample data.
        /// </summary>
        public void RequestRemoveSampleData()
        {
            if (SampleUsers.Exists(user => user.DbId == userConfig.User.DbId))
            {
                SampleRemoveMessage = userConfig.GetText("E5220");
                SampleRemoveAllowed = false;
            }
            else
            {
                SampleRemoveMessage = userConfig.GetText("U5203");
                SampleRemoveAllowed = true;
            }
            SampleRemoveMode = true;
        }

        /// <summary>
        /// Removes all seeded sample users.
        /// </summary>
        public async Task RemoveSampleData()
        {
            ShowSampleRemoveButton = false;
            SampleRemoveMode = false;
            WorkInProgress = true;
            foreach (UiUser user in SampleUsers)
            {
                try
                {
                    UserDeleteAllEntriesParameters parameters = new() { UserId = user.DbId };
                    RestResponse<bool> middlewareServerResponse = await middlewareClient.RemoveUserFromAllEntries(parameters);
                    if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == false)
                    {
                        displayMessageInUi(null, userConfig.GetText("remove_sample_data"), userConfig.GetText("E5221"), true);
                        ShowSampleRemoveButton = true;
                    }
                    else
                    {
                        ActUser = user;
                        await Delete();
                    }
                }
                catch (Exception exception)
                {
                    displayMessageInUi(exception, userConfig.GetText("remove_sample_data"), "", false);
                }
            }
            WorkInProgress = false;
        }

        /// <summary>
        /// Closes all open dialogs.
        /// </summary>
        public void Cancel()
        {
            AddMode = false;
            EditMode = false;
            DeleteMode = false;
            SampleRemoveMode = false;
            ResetPasswordMode = false;
        }
    }
}

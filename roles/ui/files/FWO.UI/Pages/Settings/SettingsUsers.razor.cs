using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using RestSharp;

namespace FWO.Ui.Pages.Settings
{
    public partial class SettingsUsers
    {
    [CascadingParameter]
    Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;

    private List<UiLdapConnection> connectedLdaps = [];
    private List<UiLdapConnection> writableLdaps = [];
    UiLdapConnection internalLdap = new();

    private List<UiUser> ldapUsers = [];
    private List<UiUser> uiUsers = [];
    private List<UiUser> sampleUsers = [];
    private List<Tenant> tenants = [];
    private List<Tenant> availableTenants = [];
    private List<UserGroup> groups = [];
    private List<Role> roles = [];
    private List<Role> availableRoles = [];

    private bool EditMode = false;
    private bool DeleteMode = false;
    private bool SampleRemoveMode = false;
    private bool sampleRemoveAllowed = false;
    private bool AddMode = false;
    private bool showSampleRemoveButton = false;
    private bool ResetPasswordMode = false;

    private UiUser newUser = new();
    private UiUser actUser = new();
    private UiLdapConnection? selectedLdap;
    private Tenant? selectedTenant;
    private Role? selectedRole;
    private UserGroup? selectedGroup;

    private string deleteMessage = "";
    private string sampleRemoveMessage = "";
    private bool workInProgress = false;

    protected override async Task OnInitializedAsync()
    {
        if (await FetchFromDb())
            await SynchronizeGroupsAndRoles();
    }

    private async Task<bool> FetchFromDb()
    {
        try
        {
            connectedLdaps = await apiConnection.SendQueryAsync<List<UiLdapConnection>>(AuthQueries.getLdapConnections);
            writableLdaps = connectedLdaps.FindAll(x => x.IsWritable());
            internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal()) ?? throw new KeyNotFoundException(userConfig.GetText("E5207"));

            // Get the tenants
            tenants = await apiConnection.SendQueryAsync<List<Tenant>>(AuthQueries.getTenants);

            // Get users from uiusers table
            RestResponse<List<UserGetReturnParameters>> middlewareServerResponse = await middlewareClient.GetUsers();
            if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == null)
            {
                DisplayMessageInUi(null, userConfig.GetText("fetch_users_local"), userConfig.GetText("E5209"), true);
            }
            else
            {
                uiUsers = [];
                sampleUsers = [];
                foreach (UserGetReturnParameters apiUser in middlewareServerResponse.Data)
                {
                    UiUser user = new(apiUser);
                    user.LdapConnection = connectedLdaps.FirstOrDefault(x => x.Id == user.LdapConnection.Id) ?? throw new ArgumentNullException(nameof(user.LdapConnection.Id));
                    if(user.Tenant != null)
                    {
                        user.Tenant.Name = tenants.FirstOrDefault(x => x.Id == user.Tenant.Id)?.Name ?? throw new ArgumentNullException(nameof(user.Tenant.Id));
                    }
                    uiUsers.Add(user);
                }
                AnalyseSampleUsers();
            }
            return true;
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            return false;
        }
    }

    private void AnalyseSampleUsers()
    {
        sampleUsers = [.. uiUsers.Where(u => u.Name.EndsWith(GlobalConst.k_demo))];
        showSampleRemoveButton = (sampleUsers.Count > 0);
    }

    private async Task SynchronizeGroupsAndRoles()
    {
        // get groups from internal ldap
        await GetGroupsFromInternalLdap();
        SynchronizeUsersToGroups();

        // get roles from internal ldap
        await GetRolesFromInternalLdap();
        SynchronizeUsersToRoles();

        availableRoles = roles.Where(x => (x.Name != Roles.Anonymous && x.Name != Roles.MiddlewareServer)).OrderBy(x => x.Name).ToList();
    }

    private void SynchronizeUsersToGroups()
    {
        try
        {
            foreach (var user in uiUsers)
            {
                user.Groups = new ();
                foreach (var group in groups)
                {
                    if (group.Users != null && group.Users.Exists(x => x.Dn == user.Dn))
                    {
                        user.Groups.Add(group.Name);
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("sync_users"), "", true);
        }
    }

    private void SynchronizeUsersToRoles()
    {
        try
        {
            foreach (var user in uiUsers)
            {
                user.Roles = new ();
                foreach (var role in roles)
                {
                    if (role.Users != null && role.Users.Exists(x => x.Dn == user.Dn))
                    {
                        user.Roles.Add(role.Name);
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("sync_users"), "", true);
        }
    }

    private async Task Resynchronize()
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
            foreach (var ldapUser in ldapUsers)
            {
                UiUser? relatedUiUser = uiUsers.FirstOrDefault(x => x.Dn == ldapUser.Dn);
                if (relatedUiUser != null)
                {
                    // Update related user
                    if (relatedUiUser.Email != ldapUser.Email)
                    {
                        relatedUiUser.Email = ldapUser.Email;
                        await updateUserInDb(relatedUiUser);
                    }
                    if (relatedUiUser.Firstname != ldapUser.Firstname)
                    {
                        relatedUiUser.Firstname = ldapUser.Firstname;
                        await updateUserInDb(relatedUiUser);
                    }
                    if (relatedUiUser.Lastname != ldapUser.Lastname)
                    {
                        relatedUiUser.Lastname = ldapUser.Lastname;
                        await updateUserInDb(relatedUiUser);
                    }
                }
                else
                {
                    // Add new user to UiUsers table
                    await addUserToDb(ldapUser);
                    uiUsers.Add(ldapUser);
                }
            }
            // Todo: CleanUpUser for deleted users?
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("sync_users"), "", true);
        }
    }

    private async Task GetUsersFromInternalLdap()
    {
        try
        {
            ldapUsers.Clear();

            // get users from Ldap
            LdapUserGetParameters userGetParameters = new LdapUserGetParameters { LdapId = internalLdap.Id, SearchPattern = "" };
            RestResponse<List<LdapUserGetReturnParameters>> middlewareServerResponse = await middlewareClient.GetLdapUsers(userGetParameters);
            if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == null)
            {
                DisplayMessageInUi(null, userConfig.GetText("get_user_from_ldap"), userConfig.GetText("E5208"), true);
            }
            else
            {
                foreach (var user in middlewareServerResponse.Data)
                {
                    DistName distname = new DistName(user.UserDn);
                    UiUser newUser = new UiUser()
                    {
                        Dn = user.UserDn,
                        Name = distname.UserName,
                        Email = user.Email,
                        Firstname = user.Firstname,
                        Lastname = user.Lastname,
                        PasswordMustBeChanged = false,
                        LdapConnection = internalLdap
                    };
                    string tenantName = distname.GetTenantNameViaLdapTenantLevel(internalLdap.TenantLevel);
                    if (tenantName != "")
                    {
                        newUser.Tenant = new Tenant(){ Name = tenantName };
                    }
                    ldapUsers.Add(newUser);
                }
            }
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("fetch_users_ldap"), "", true);
        }
    }

    private async Task GetGroupsFromInternalLdap()
    {
        try
        {
            groups = await GroupAccess.GetGroupsFromInternalLdap(middlewareClient, userConfig, DisplayMessageInUi);
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("fetch_groups"), "", true);
        }
    }

    private async Task GetRolesFromInternalLdap()
    {
        try
        {
            roles = await RoleAccess.GetRolesFromInternalLdap(middlewareClient);
            if (roles.Count == 0)
            {
                DisplayMessageInUi(null, userConfig.GetText("fetch_roles"), userConfig.GetText("E5251"), true);
            }
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("fetch_roles"), "", true);
        }
    }

    private async Task updateUserInDb(UiUser user)
    {
        try
        {
            var Variables = new
            {
                id = user.DbId,
                email = user.Email
                // uiuser_first_name = user.Firstname,
                // uiuser_last_name = user.Lastname
            };
            await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserEmail, Variables);
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("update_user_local"), "", false);
        }
    }

    private async Task addUserToDb(UiUser user)
    {
        try
        {
            Tenant? actTenant = tenants.FirstOrDefault(x => x.Name == user.Tenant?.Name);
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
            if(returnIds != null)
            {
                user.DbId = returnIds[0].NewId;
            }
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("add_user_local"), "", false);
        }
    }

    }
}

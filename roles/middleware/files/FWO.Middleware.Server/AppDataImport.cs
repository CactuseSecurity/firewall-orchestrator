using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Recert;
using FWO.Services;
using Novell.Directory.Ldap;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the App Data Import
    /// </summary>
    public class AppDataImport : DataImportBase
    {
        private List<ModellingImportAppData> importedApps = [];
        private List<FwoOwner> existingApps = [];
        private List<ModellingAppServer> existingAppServers = [];

        private Ldap internalLdap = new();
        private Ldap ownerGroupLdap = new();

        private List<Ldap> connectedLdaps = [];
        private string? ownerGroupLdapPath = "";
        private List<GroupGetReturnParameters> allGroups = [];
        private Dictionary<OwnerResponsibleType, List<string>> rolesToSetByType = [];
        private ModellingNamingConvention NamingConvention = new();
        private UserConfig userConfig = new();
        private const string LogMessageTitle = "Import App Data";
        private const string LevelFile = "Import File";
        private const string LevelApp = "App";
        private const string LevelAppServer = "App Server";

        /// <summary>
        /// Constructor for App Data Import
        /// </summary>
        public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
        { }

        /// <summary>
        /// Run the App Data Import
        /// </summary>
        public async Task<List<string>> Run()
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(globalConfig.ModNamingConvention) ?? new();
            List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportAppDataPath) ?? throw new JsonException("Config Data could not be deserialized.");
            userConfig = new(globalConfig);
            userConfig.User.Name = Roles.MiddlewareServer;
            userConfig.AutoReplaceAppServer = globalConfig.AutoReplaceAppServer;
            await InitLdap();
            List<string> failedImports = [];
            foreach (var importfilePathAndName in importfilePathAndNames)
            {
                if (!RunImportScript(importfilePathAndName + ".py"))
                {
                    Log.WriteInfo(LogMessageTitle, $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                }
                await ImportSingleSource(importfilePathAndName + ".json", failedImports);
            }
            return failedImports;
        }

        private async Task InitLdap()
        {
            connectedLdaps = await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
            internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new KeyNotFoundException("No internal Ldap with group handling found.");
            ownerGroupLdap = connectedLdaps.FirstOrDefault(x => x.Id == globalConfig.OwnerLdapId) ?? throw new KeyNotFoundException("Ldap with group handling not found.");
            ownerGroupLdapPath = ownerGroupLdap.GroupWritePath;
            allGroups = globalConfig.OwnerLdapId == GlobalConst.kLdapInternalId ?
                await internalLdap.GetAllInternalGroups() :
                await ownerGroupLdap.GetAllGroupObjects(globalConfig.OwnerLdapGroupNames.
                    Replace(Placeholder.AppId, "*").
                    Replace(Placeholder.ExternalAppId, "*").
                    Replace(Placeholder.AppPrefix, "*"));
            rolesToSetByType = ParseRolesWithImport(globalConfig.RolesWithAppDataImport);
        }

        private async Task ImportSingleSource(string importfileName, List<string> failedImports)
        {
            try
            {
                ReadFile(importfileName);
                ModellingImportOwnerData? importedOwnerData = JsonSerializer.Deserialize<ModellingImportOwnerData>(importFile) ?? throw new JsonException("File could not be parsed.");
                if (importedOwnerData != null && importedOwnerData.Owners != null)
                {
                    importedApps = importedOwnerData.Owners;
                    await ImportApps(importfileName);
                }
            }
            catch (Exception exc)
            {
                string errorText = $"File {importfileName} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(2, LevelFile, errorText);
                failedImports.Add(importfileName);
            }
        }

        private async Task ImportApps(string importfileName)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            if (!(globalConfig.OwnerLdapGroupNames.Contains(Placeholder.AppId) ||
                globalConfig.OwnerLdapGroupNames.Contains(Placeholder.ExternalAppId)))
            {
                Log.WriteWarning(LogMessageTitle, $"Owner group pattern does not contain any of the placeholders {Placeholder.AppId} or {Placeholder.ExternalAppId}.");
            }
            else
            {
                existingApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
                foreach (var incomingApp in importedApps)
                {
                    if (await SaveApp(incomingApp))
                    {
                        ++successCounter;
                    }
                    else
                    {
                        ++failCounter;
                    }
                }
                string? importSource = importedApps.FirstOrDefault()?.ImportSource;
                if (importSource != null)
                {
                    foreach (var existingApp in existingApps.Where(x => x.ImportSource == importSource && x.Active))
                    {
                        if (importedApps.FirstOrDefault(x => x.ExtAppId == existingApp.ExtAppId) == null)
                        {
                            if (await DeactivateApp(existingApp))
                            {
                                ++deleteCounter;
                            }
                            else
                            {
                                ++deleteFailCounter;
                            }
                        }
                    }
                }
                string messageText = $"Imported from {importfileName}: {successCounter} apps, {failCounter} failed. Deactivated {deleteCounter} apps, {deleteFailCounter} failed.";
                Log.WriteInfo(LogMessageTitle, messageText);
                await AddLogEntry(0, LevelFile, messageText);
            }
        }

        private async Task<bool> SaveApp(ModellingImportAppData incomingApp)
        {
            try
            {
                int appId;
                FwoOwner? existingApp = existingApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId);
                string userGroupDn = await CreateOrUpdateUserGroup(incomingApp, existingApp);

                if (existingApp == null)
                {
                    appId = await NewApp(incomingApp, userGroupDn);
                }
                else
                {
                    appId = existingApp.Id;
                    await UpdateApp(incomingApp, existingApp, userGroupDn);
                }
                if (incomingApp.MainUser != null && incomingApp.MainUser != "")
                {
                    await UpdateRoles(incomingApp.MainUser, GetRolesForType(OwnerResponsibleType.kMainResponsible));
                }
                // in order to store email addresses of users in the group in UiUser for email notification:
                await AddAllGroupMembersToUiUser(userGroupDn);
                await InitRecert(incomingApp, existingApp, appId);
            }
            catch (Exception exc)
            {
                string errorText = $"App {incomingApp.Name} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(2, LevelApp, errorText);
                return false;
            }
            return true;
        }

        private async Task<int> NewApp(ModellingImportAppData incomingApp, string userGroupDn)
        {
            int appId = 0;
            var variables = new
            {
                name = incomingApp.Name,
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                recertInterval = incomingApp.RecertInterval ?? globalConfig.RecertificationPeriod,
                importSource = incomingApp.ImportSource,
                commSvcPossible = false,
                recertActive = false
            };
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.newOwner, variables)).ReturnIds;
            if (returnIds != null)
            {
                appId = returnIds[0].NewId;
                List<OwnerResponsible> responsibles = BuildOwnerResponsibles(incomingApp.MainUser, userGroupDn, []);
                await UpdateOwnerResponsibles(appId, responsibles);
                await ApplyRolesToResponsibles(responsibles, rolesToSetByType);
                foreach (var appServer in incomingApp.AppServers)
                {
                    await NewAppServer(appServer, appId, incomingApp.ImportSource);
                }
            }
            return appId;
        }

        private async Task UpdateApp(ModellingImportAppData incomingApp, FwoOwner existingApp, string userGroupDn)
        {
            var Variables = new
            {
                id = existingApp.Id,
                name = incomingApp.Name,
                appIdExternal = string.IsNullOrEmpty(incomingApp.ExtAppId) ? null : incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                recertInterval = incomingApp.RecertInterval ?? globalConfig.RecertificationPeriod,
                commSvcPossible = existingApp.CommSvcPossible,
                recertActive = incomingApp.RecertActive || existingApp.RecertActive
            };
            await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.updateOwner, Variables);
            List<OwnerResponsible> responsibles = BuildOwnerResponsibles(
                incomingApp.MainUser,
                userGroupDn,
                existingApp.GetOwnerResponsiblesByType(OwnerResponsibleType.kOptionalEscalationResponsible));
            await UpdateOwnerResponsibles(existingApp.Id, responsibles);
            await ApplyRolesToResponsibles(responsibles, rolesToSetByType);
            await ImportAppServers(incomingApp, existingApp.Id);
        }

        private async Task<bool> DeactivateApp(FwoOwner app)
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.deactivateOwner, new { id = app.Id });
            }
            catch (Exception exc)
            {
                string errorText = $"Outdated App {app.Name} could not be deactivated.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelApp, errorText);
                return false;
            }
            return true;
        }

        private static List<OwnerResponsible> BuildOwnerResponsibles(string? mainUserDn, string userGroupDn, IEnumerable<string> extraDns)
        {
            List<OwnerResponsible> responsibles = [];
            if (!string.IsNullOrWhiteSpace(mainUserDn))
            {
                responsibles.Add(new OwnerResponsible { Dn = mainUserDn, ResponsibleType = OwnerResponsibleType.kMainResponsible });
            }
            if (!string.IsNullOrWhiteSpace(userGroupDn))
            {
                responsibles.Add(new OwnerResponsible { Dn = userGroupDn, ResponsibleType = OwnerResponsibleType.kSupportingResponsible });
            }
            foreach (string dn in extraDns.Where(dn => !string.IsNullOrWhiteSpace(dn)))
            {
                responsibles.Add(new OwnerResponsible { Dn = dn, ResponsibleType = OwnerResponsibleType.kOptionalEscalationResponsible });
            }
            return responsibles;
        }

        private async Task UpdateOwnerResponsibles(int ownerId, List<OwnerResponsible> responsibles)
        {
            await apiConnection.SendQueryAsync<object>(OwnerQueries.deleteOwnerResponsibles, new { ownerId });
            if (responsibles.Count == 0)
            {
                return;
            }

            var objects = responsibles.ConvertAll(r => new
            {
                owner_id = ownerId,
                dn = r.Dn,
                responsible_type = (int)r.ResponsibleType
            });
            await apiConnection.SendQueryAsync<object>(OwnerQueries.newOwnerResponsibles, new { responsibles = objects });
        }

        private async Task ApplyRolesToResponsibles(List<OwnerResponsible> responsibles, Dictionary<OwnerResponsibleType, List<string>> rolesByType)
        {
            foreach (OwnerResponsible responsible in responsibles)
            {
                if (!rolesByType.TryGetValue(responsible.ResponsibleType, out List<string>? roles) || roles.Count == 0)
                {
                    continue;
                }
                await UpdateRoles(responsible.Dn, roles);
            }
        }

        private string GetGroupName(string extAppIdString)
        {
            // hard-coded GlobalConst.kAppIdSeparator could be moved to settings
            if (globalConfig.OwnerLdapGroupNames.Contains(Placeholder.ExternalAppId))
            {
                return globalConfig.OwnerLdapGroupNames.Replace(Placeholder.ExternalAppId, extAppIdString);
            }

            if (globalConfig.OwnerLdapGroupNames.Contains(Placeholder.AppPrefix) &&
                globalConfig.OwnerLdapGroupNames.Contains(Placeholder.AppId))
            {
                string[] parts = extAppIdString.Split(GlobalConst.kAppIdSeparator);
                string appPrefix = parts.Length > 0 ? parts[0] : "";
                string appId = parts.Length > 1 ? parts[1] : "";
                return globalConfig.OwnerLdapGroupNames.Replace(Placeholder.AppPrefix, appPrefix).Replace(Placeholder.AppId, appId);
            }
            Log.WriteInfo(LogMessageTitle, $"Could not find ayn placeholders in group name pattern \"{globalConfig.OwnerLdapGroupNames}\" " +
                $"({Placeholder.ExternalAppId}, {Placeholder.AppPrefix}, {Placeholder.AppId} ");
            return globalConfig.OwnerLdapGroupNames;
        }

        private string GetGroupDn(string extAppIdString)
        {
            if (ownerGroupLdapPath == null)
            {
                throw new ArgumentNullException(nameof(ownerGroupLdapPath));
            }
            return $"cn={GetGroupName(extAppIdString)},{ownerGroupLdapPath}";
        }

        private string GetRoleDn(string role)
        {
            return $"cn={role},{internalLdap.RoleSearchPath}";
        }

        private List<string> GetRolesForType(OwnerResponsibleType typeId)
        {
            return rolesToSetByType.TryGetValue(typeId, out List<string>? roles) ? roles : [];
        }

        private static Dictionary<OwnerResponsibleType, List<string>> ParseRolesWithImport(string rolesJson)
        {
            Dictionary<OwnerResponsibleType, List<string>> rolesByType = [];
            if (string.IsNullOrWhiteSpace(rolesJson))
            {
                return rolesByType;
            }

            string trimmed = rolesJson.TrimStart();
            if (trimmed.StartsWith("["))
            {
                List<string> roles = JsonSerializer.Deserialize<List<string>>(rolesJson) ?? [];
                rolesByType[OwnerResponsibleType.kSupportingResponsible] = roles;
                return rolesByType;
            }

            Dictionary<string, List<string>>? parsed = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(rolesJson);
            if (parsed != null)
            {
                foreach (var entry in parsed)
                {
                    if (int.TryParse(entry.Key, out int typeId))
                    {
                        if (Enum.IsDefined(typeof(OwnerResponsibleType), typeId))
                        {
                            rolesByType[(OwnerResponsibleType)typeId] = entry.Value;
                        }
                    }
                }
            }
            return rolesByType;
        }

        /// <summary>
        /// for each user of a remote ldap group create a user in uiuser
        /// this is necessary in order to get details like email address for users
        /// which have never logged in but who need to be notified via email
        /// </summary>
        private async Task AddAllGroupMembersToUiUser(string userGroupDn)
        {
            foreach (Ldap ldap in connectedLdaps)
            {
                foreach (string memberDn in await ldap.GetGroupMembers(userGroupDn))
                {
                    UiUser? uiUser = await ConvertLdapToUiUser(memberDn);
                    if (uiUser != null)
                    {
                        await UiUserHandler.UpsertUiUser(apiConnection, uiUser, false);
                    }
                }
            }
        }

        private async Task<UiUser?> ConvertLdapToUiUser(string userDn)
        {
            // add the modelling user to local uiuser table for later ref to email address
            // find the user in all connected ldaps
            foreach (Ldap ldap in connectedLdaps)
            {
                if (!string.IsNullOrEmpty(ldap.UserSearchPath) && userDn.ToLower().Contains(ldap.UserSearchPath!.ToLower()))
                {
                    LdapEntry? ldapUser = await ldap.GetUserDetailsFromLdap(userDn);

                    if (ldapUser != null)
                    {
                        // add data from ldap entry to uiUser
                        return new()
                        {
                            LdapConnection = new UiLdapConnection() { Id = ldap.Id },
                            Dn = ldapUser.Dn,
                            Name = Ldap.GetName(ldapUser),
                            Firstname = Ldap.GetFirstName(ldapUser),
                            Lastname = Ldap.GetLastName(ldapUser),
                            Email = Ldap.GetEmail(ldapUser),
                            Tenant = await DeriveTenantFromLdap(ldap, ldapUser)
                        };
                    }
                }
            }
            return null;
        }

        private async Task<Tenant> DeriveTenantFromLdap(Ldap ldap, LdapEntry ldapUser)
        {
            // try to derive the the user's tenant from the ldap settings
            Tenant tenant = new()
            {
                Id = GlobalConst.kTenant0Id  // default: tenant0 (id=1)
            };

            string tenantName = "";

            // can we derive the users tenant purely from its ldap?
            if (!string.IsNullOrEmpty(ldap.GlobalTenantName) || ldap.TenantLevel > 0)
            {
                if (ldap.TenantLevel > 0)
                {
                    // getting tenant via tenant level setting from distinguished name
                    tenantName = ldap.GetTenantName(ldapUser);
                }
                else if (!string.IsNullOrEmpty(ldap.GlobalTenantName))
                {
                    tenantName = ldap.GlobalTenantName ?? "";
                }

                var variables = new { tenant_name = tenantName };
                Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, variables, "getTenantId");
                if (tenants.Length == 1)
                {
                    tenant.Id = tenants[0].Id;
                }
            }
            return tenant;
        }

        private async Task<string> CreateOrUpdateUserGroup(ModellingImportAppData incomingApp, FwoOwner? existingApp)
        {
            string userGroupDn = GetGroupDn(incomingApp.ExtAppId);
            if (globalConfig.ManageOwnerLdapGroups)
            {
                if ((existingApp == null || existingApp.GetOwnerResponsiblesByType(OwnerResponsibleType.kSupportingResponsible).Count == 0)
                    && allGroups.FirstOrDefault(x => x.GroupDn == userGroupDn) == null)
                {
                    userGroupDn = await CreateUserGroup(incomingApp, userGroupDn);
                }
                else
                {
                    await UpdateUserGroup(incomingApp, userGroupDn);
                }
            }
            else
            {
                // add necessary roles for user group
                await UpdateRoles(userGroupDn, GetRolesForType(OwnerResponsibleType.kSupportingResponsible));
            }
            return userGroupDn;
        }

        private async Task<string> CreateUserGroup(ModellingImportAppData incomingApp, string userGroupDn)
        {
            if (incomingApp.Modellers != null && incomingApp.Modellers.Count > 0
                || incomingApp.ModellerGroups != null && incomingApp.ModellerGroups.Count > 0)
            {
                string groupName = GetGroupName(incomingApp.ExtAppId);
                string newDn = await internalLdap.AddGroup(groupName, true);
                if (newDn == "")
                {
                    throw new InternalException($"Group '{groupName}' could not be created in internal Ldap.");
                }
                if (newDn != userGroupDn) // may this happen?
                {
                    Log.WriteInfo(LogMessageTitle, $"New UserGroup DN {newDn} differs from settings value {userGroupDn}.");
                }
                // add users to internal group:
                await AddUsersToGroup(incomingApp.Modellers, [], newDn);
                await AddUsersToGroup(incomingApp.ModellerGroups, [], newDn);
                await AddRoles(newDn, GetRolesForType(OwnerResponsibleType.kSupportingResponsible));
                return newDn;
            }
            return "";
        }

        private async Task UpdateUserGroup(ModellingImportAppData incomingApp, string groupDn)
        {
            List<string> existingMembers = (allGroups.FirstOrDefault(x => x.GroupDn == groupDn) ?? throw new KeyNotFoundException($"Group with DN '{groupDn}' could not be found.")).Members;
            await AddUsersToGroup(incomingApp.Modellers, existingMembers, groupDn);
            await AddUsersToGroup(incomingApp.ModellerGroups, existingMembers, groupDn);
            foreach (var member in existingMembers)
            {
                if ((incomingApp.Modellers == null || incomingApp.Modellers.FirstOrDefault(x => x.Equals(member, StringComparison.OrdinalIgnoreCase)) == null)
                    && (incomingApp.ModellerGroups == null || incomingApp.ModellerGroups.FirstOrDefault(x => x.Equals(member, StringComparison.OrdinalIgnoreCase)) == null))
                {
                    await internalLdap.RemoveUserFromEntry(member, groupDn);
                }
            }
            await UpdateRoles(groupDn, GetRolesForType(OwnerResponsibleType.kSupportingResponsible));
        }

        private async Task AddUsersToGroup(List<string>? members, List<string> existingMembers, string groupDn)
        {
            if (members != null)
            {
                foreach (var member in members)
                {
                    if (existingMembers.FirstOrDefault(x => x.Equals(member, StringComparison.OrdinalIgnoreCase)) == null)
                    {
                        await internalLdap.AddUserToEntry(member, groupDn);
                    }
                }
            }
        }

        private async Task AddRoles(string dn, List<string> rolesToApply)
        {
            foreach (var role in rolesToApply)
            {
                await internalLdap.AddUserToEntry(dn, GetRoleDn(role));
            }
        }

        private async Task UpdateRoles(string dn, List<string> rolesToApply)
        {
            List<string> roles = await internalLdap.GetRoles([dn]);
            foreach (var role in rolesToApply)
            {
                if (!roles.Contains(role))
                {
                    await internalLdap.AddUserToEntry(dn, GetRoleDn(role));
                }
            }
        }

        private async Task ImportAppServers(ModellingImportAppData incomingApp, int applId)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            var Variables = new
            {
                importSource = incomingApp.ImportSource,
                appId = applId
            };
            existingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, Variables);
            foreach (var incomingAppServer in incomingApp.AppServers)
            {
                if (await SaveAppServer(incomingAppServer, applId, incomingApp.ImportSource))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            foreach (var existingAppServer in existingAppServers.Where(e => !e.IsDeleted).ToList())
            {
                if (incomingApp.AppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == existingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == existingAppServer.IpEnd.IpAsCidr()) == null)
                {
                    if (await MarkDeletedAppServer(existingAppServer))
                    {
                        ++deleteCounter;
                    }
                    else
                    {
                        ++deleteFailCounter;
                    }
                }
            }
            Log.WriteDebug(LogMessageTitle, $"for App {incomingApp.Name}: Imported {successCounter} app servers, {failCounter} failed. {deleteCounter} app servers marked as deleted, {deleteFailCounter} failed.");
        }

        private async Task<bool> SaveAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                if (incomingAppServer.IpEnd == "")
                {
                    incomingAppServer.IpEnd = incomingAppServer.Ip;
                }
                if (globalConfig.DnsLookup)
                {
                    incomingAppServer.Name = await BuildAppServerName(incomingAppServer);
                }
                ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == incomingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == incomingAppServer.IpEnd.IpAsCidr());
                if (existingAppServer == null)
                {
                    return await NewAppServer(incomingAppServer, appID, impSource);
                }

                if (existingAppServer.IsDeleted)
                {
                    if (!await ReactivateAppServer(existingAppServer))
                    {
                        return false;
                    }
                }
                else
                {
                    // in case there are still active appservers from other sources (resulting e.g. from older revisions)
                    await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, existingAppServer);
                }
                if (!existingAppServer.Name.Equals(incomingAppServer.Name))
                {
                    if (!await UpdateAppServerName(existingAppServer, incomingAppServer.Name))
                    {
                        return false;
                    }
                }
                if (existingAppServer.CustomType == null)
                {
                    if (!await UpdateAppServerType(existingAppServer))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exc)
            {
                string errorText = $"App Server {incomingAppServer.Name} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
        }

        private async Task<string> BuildAppServerName(ModellingImportAppServer appServer)
        {
            try
            {
                return await AppServerHelper.ConstructAppServerNameFromDns(appServer.ToModellingAppServer(), NamingConvention, globalConfig.OverwriteExistingNames, true);
            }
            catch (Exception exc)
            {
                string errorText = $"App Server name {appServer.Name} could not be set according to naming conventions.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
            }
            return appServer.Name;
        }

        private async Task<bool> NewAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                var Variables = new
                {
                    name = incomingAppServer.Name,
                    appId = appID,
                    ip = incomingAppServer.Ip.IpAsCidr(),
                    ipEnd = incomingAppServer.IpEnd != "" ? incomingAppServer.IpEnd.IpAsCidr() : incomingAppServer.Ip.IpAsCidr(),
                    importSource = impSource,
                    customType = 0
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppServer, Variables)).ReturnIds;
                if (returnIds != null && returnIds.Length > 0)
                {
                    ModellingAppServer newModAppServer = new(incomingAppServer.ToModellingAppServer()) { Id = returnIds[0].NewIdLong, ImportSource = impSource, AppId = appID };
                    await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppServer, newModAppServer.Id,
                        $"New App Server: {newModAppServer.Display()}", apiConnection, userConfig, newModAppServer.AppId, DefaultInit.DoNothing, null, newModAppServer.ImportSource);
                    await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, newModAppServer);
                }
            }
            catch (Exception exc)
            {
                string errorText = $"App Server {incomingAppServer.Name} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task<bool> ReactivateAppServer(ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    deleted = false
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Reactivate, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Reactivate App Server: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
                await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, appServer);
            }
            catch (Exception exc)
            {
                string errorText = $"App Server {appServer.Name} could not be reactivated.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task<bool> UpdateAppServerType(ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    customType = 0
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerType, Variables);
                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Update App Server Type: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
            }
            catch (Exception exc)
            {
                string errorText = $"Type of App Server {appServer.Name} could not be set.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task<bool> UpdateAppServerName(ModellingAppServer appServer, string newName)
        {
            if (appServer.Name != newName)
            {
                try
                {
                    var Variables = new
                    {
                        newName,
                        id = appServer.Id,
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerName, Variables);
                    await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                        $"Update App Server Name: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
                    Log.WriteWarning(LogMessageTitle, $"Name of App Server changed from {appServer.Name} changed to {newName}");
                }
                catch (Exception exc)
                {
                    string errorText = $"Name of App Server {appServer.Name} could not be set to {newName}.";
                    Log.WriteError(LogMessageTitle, errorText, exc);
                    await AddLogEntry(1, LevelAppServer, errorText);
                    return false;
                }
            }
            return true;
        }

        private async Task<bool> MarkDeletedAppServer(ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    deleted = true
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Deactivate App Server: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
                await AppServerHelper.ReactivateOtherSource(apiConnection, userConfig, appServer);
            }
            catch (Exception exc)
            {
                string errorText = $"Outdated AppServer {appServer.Name} could not be marked as deleted.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task InitRecert(ModellingImportAppData incomingApp, FwoOwner? existingApp, int appId)
        {
            if (userConfig.RecertificationMode == RecertificationMode.OwnersAndRules &&
                incomingApp.RecertActive && (existingApp == null || !existingApp.RecertActive))
            {
                RecertHandler recertHandler = new(apiConnection, userConfig);
                await recertHandler.InitOwnerRecert(new()
                {
                    Id = appId,
                    RecertInterval = incomingApp.RecertInterval
                });
            }
        }

        private async Task AddLogEntry(int severity, string level, string description)
        {
            try
            {
                var Variables = new
                {
                    user = 0,
                    source = GlobalConst.kImportAppData,
                    severity = severity,
                    suspectedCause = level,
                    description = description
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addDataImportLogEntry, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    Log.WriteError("Write Log", "Log could not be written to database");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Log", $"Could not write log: ", exc);
            }
        }
    }
}

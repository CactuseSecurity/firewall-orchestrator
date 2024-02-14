﻿using FWO.Logging;
using NetTools;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;
using FWO.Middleware.RequestParameters;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the App Data Import
    /// </summary>
    public class AppDataImport : DataImportBase
    {
        private List<ModellingImportAppData> importedApps= new();
        private List<FwoOwner> existingApps = new();
        private List<ModellingAppServer> existingAppServers = new();

        private Ldap internalLdap = new();
        private string roleDn = "";
        List<GroupGetReturnParameters> allGroups = new();


        /// <summary>
        /// Constructor for App Data Import
        /// </summary>
        public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base (apiConnection, globalConfig)
        {}

        /// <summary>
        /// Run the App Data Import
        /// </summary>
        public async Task<bool> Run()
        {
            try
            {
                List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportAppDataPath) ?? throw new Exception("Config Data could not be deserialized.");
                await InitLdap();
                foreach(var importfilePathAndName in importfilePathAndNames)
                {
                    if(!RunImportScript(importfilePathAndName + ".py"))
                    {
                        Log.WriteInfo("Import App Data", $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                    }
                    await ImportSingleSource(importfilePathAndName + ".json");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"Import could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task InitLdap()
        {
            List<Ldap> connectedLdaps = await apiConnection.SendQueryAsync<List<Ldap>>(Api.Client.Queries.AuthQueries.getLdapConnections);
            internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new Exception("No internal Ldap with group handling found.");
            roleDn = $"cn=modeller,{internalLdap.RoleSearchPath}";
            allGroups = internalLdap.GetAllInternalGroups();
        }

        private async Task<bool> ImportSingleSource(string importfileName)
        {
            try
            {
                ReadFile(importfileName);
                ModellingImportOwnerData? importedOwnerData = JsonSerializer.Deserialize<ModellingImportOwnerData>(importFile) ?? throw new Exception("File could not be parsed.");
                if(importedOwnerData != null && importedOwnerData.Owners != null)
                {
                    importedApps = importedOwnerData.Owners;
                    await ImportApps(importfileName);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"File {importfileName} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task ImportApps(string importfileName)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            existingApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(Api.Client.Queries.OwnerQueries.getOwners);
            foreach(var incomingApp in importedApps)
            {
                if(await SaveApp(incomingApp))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
                foreach(var existingApp in existingApps.Where(x => x.ImportSource == incomingApp.ImportSource && x.Active))
                {
                    if(importedApps.FirstOrDefault(x => x.Name == existingApp.Name) == null)
                    {
                        if(await DeactivateApp(existingApp))
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
            Log.WriteInfo("Import App Data", $"Imported from {importfileName}: {successCounter} apps, {failCounter} failed. Deactivated {deleteCounter} apps, {deleteFailCounter} failed.");
        }

        private async Task<bool> SaveApp(ModellingImportAppData incomingApp)
        {
            try
            {
                FwoOwner? existingApp = existingApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId);
                if(existingApp == null)
                {
                    await NewApp(incomingApp);
                }
                else
                {
                    await UpdateApp(incomingApp, existingApp);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"App {incomingApp.Name} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task NewApp(ModellingImportAppData incomingApp)
        {
            string userGroupDn = CreateUserGroup(incomingApp);
            var Variables = new 
            { 
                name = incomingApp.Name,
                dn = incomingApp.MainUser ?? "",
                groupDn = userGroupDn,
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                importSource = incomingApp.ImportSource,
                commSvcPossible = false
            };
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.newOwner, Variables)).ReturnIds;
            if (returnIds != null)
            {
                int appId = returnIds[0].NewId;
                foreach(var appServer in incomingApp.AppServers)
                {
                    await NewAppServer(appServer, appId, incomingApp.ImportSource);
                }
            }
        }

        private async Task UpdateApp(ModellingImportAppData incomingApp, FwoOwner existingApp)
        {
            string userGroupDn = existingApp.GroupDn;
            if(existingApp.GroupDn == null || existingApp.GroupDn == "")
            {
                GroupGetReturnParameters? groupWithSameName = allGroups.FirstOrDefault(x => new DistName(x.GroupDn).Group == GroupName(incomingApp.ExtAppId));
                if(groupWithSameName != null)
                {
                    if(userGroupDn == "")
                    {
                        userGroupDn = groupWithSameName.GroupDn;
                    }
                    UpdateUserGroup(incomingApp, groupWithSameName.GroupDn);
                }
                else
                {
                    userGroupDn = CreateUserGroup(incomingApp);
                }
            }
            else
            {
                UpdateUserGroup(incomingApp, userGroupDn);
            }
            var Variables = new 
            {
                id = existingApp.Id,
                name = incomingApp.Name,
                dn = incomingApp.MainUser ?? "",
                groupDn = userGroupDn,
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                commSvcPossible = existingApp.CommSvcPossible
            };
            await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.updateOwner, Variables);
            await ImportAppServers(incomingApp, existingApp.Id);
        }

        private async Task<bool> DeactivateApp(FwoOwner app)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.deactivateOwner, new { id = app.Id });
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"Outdated App {app.Name} could not be deactivated.", exc);
                return false;
            }
            return true;
        }

        private static string GroupName(string appName)
        {
            return GlobalConst.kModellerGroup + appName;
        }

        private string CreateUserGroup(ModellingImportAppData incomingApp)
        {
            string groupDn = "";
            if(incomingApp.Modellers != null && incomingApp.Modellers.Count > 0
                || incomingApp.ModellerGroups != null && incomingApp.ModellerGroups.Count > 0)
            {
                string groupName = GroupName(incomingApp.ExtAppId);
                groupDn = internalLdap.AddGroup(groupName, true);
                if(incomingApp.Modellers != null)
                {
                    foreach(var modeller in incomingApp.Modellers)
                    {
                        internalLdap.AddUserToEntry(modeller, groupDn);
                    }
                }
                if(incomingApp.ModellerGroups != null)
                {
                    foreach(var modellerGrp in incomingApp.ModellerGroups)
                    {
                        internalLdap.AddUserToEntry(modellerGrp, groupDn);
                    }
                }
                internalLdap.AddUserToEntry(groupDn, roleDn);
            }
            return groupDn;
        }

        private string UpdateUserGroup(ModellingImportAppData incomingApp, string groupDn)
        {
            List<string> existingMembers = (allGroups.FirstOrDefault(x => x.GroupDn == groupDn) ?? throw new Exception("Group could not be found.")).Members;
            if(incomingApp.Modellers != null)
            {
                foreach(var modeller in incomingApp.Modellers)
                {
                    if(existingMembers.FirstOrDefault(x => x.ToLower() == modeller.ToLower()) == null)
                    {
                        internalLdap.AddUserToEntry(modeller, groupDn);
                    }
                }
            }
            if(incomingApp.ModellerGroups != null)
            {
                foreach(var modellerGrp in incomingApp.ModellerGroups)
                {
                    if(existingMembers.FirstOrDefault(x => x.ToLower() == modellerGrp.ToLower()) == null)
                    {
                        internalLdap.AddUserToEntry(modellerGrp, groupDn);
                    }
                }
            }
            foreach(var member in existingMembers)
            {
                if((incomingApp.Modellers == null || incomingApp.Modellers.FirstOrDefault(x => x.ToLower() == member.ToLower()) == null)
                    && (incomingApp.ModellerGroups == null || incomingApp.ModellerGroups.FirstOrDefault(x => x.ToLower() == member.ToLower()) == null))
                {
                    internalLdap.RemoveUserFromEntry(member, groupDn);
                }
            }
            return groupDn;
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
            existingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(Api.Client.Queries.ModellingQueries.getImportedAppServers, Variables);
            foreach(var incomingAppServer in incomingApp.AppServers)
            {
                if(await SaveAppServer(incomingAppServer, applId, incomingApp.ImportSource))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            foreach(var existingAppServer in existingAppServers)
            {
                if(incomingApp.AppServers.FirstOrDefault(x => IpAsCidr(x.Ip) == IpAsCidr(existingAppServer.Ip)) == null)
                {
                    if(await MarkDeletedAppServer(existingAppServer))
                    {
                        ++deleteCounter;
                    }
                    else
                    {
                        ++deleteFailCounter;
                    }
                }
            }
            Log.WriteDebug($"Import App Server Data for App {incomingApp.Name}", $"Imported {successCounter} app servers, {failCounter} failed. {deleteCounter} app servers marked as deleted, {deleteFailCounter} failed.");
        }

        private async Task<bool> SaveAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => IpAsCidr(x.Ip) == IpAsCidr(incomingAppServer.Ip));
                if(existingAppServer == null)
                {
                    return await NewAppServer(incomingAppServer, appID, impSource);
                }
                else if(existingAppServer.IsDeleted)
                {
                    return await ReactivateAppServer(existingAppServer);
                }
                return true;
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
                return false;
            }
        }

        private async Task<bool> NewAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                var Variables = new 
                {
                    name = incomingAppServer.Name,
                    appId = appID,
                    ip = IpAsCidr(incomingAppServer.Ip),  // todo ?
                    // subnet = incomingAppServer.Subnet,
                    importSource = impSource
                };
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAppServer, Variables);
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
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
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.setAppServerDeletedState, Variables);
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Server Data", $"App Server {appServer.Name} could not be reactivated.", exc);
                return false;
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
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.setAppServerDeletedState, Variables);
            }
            catch (Exception exc)
            {
                Log.WriteError("Import AppServer Data", $"Outdated AppServer {appServer.Name} could not be marked as deleted.", exc);
                return false;
            }
            return true;
        }

        private static string IpAsCidr(string ip)
        {
            return IPAddressRange.Parse(ip).ToCidrString();
        }
    }
}

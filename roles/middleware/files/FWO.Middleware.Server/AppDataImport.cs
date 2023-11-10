using FWO.Logging;
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
    public class AppDataImport
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private string importFile { get; set; } = "";

        private List<ModellingImportAppData> importedApps= new();
        private List<FwoOwner> existingApps = new();
        private List<ModellingAppServer> existingAppServers = new();

        private Ldap internalLdap = new();
        private string roleDn = "";
        List<GroupGetReturnParameters> allGroups = new();


        /// <summary>
        /// Constructor for App Data Import
        /// </summary>
        public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

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
                    await RunImportScript(importfilePathAndName + ".py");
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

        private async Task RunImportScript(string importScriptFile)
        {
            if(File.Exists(importScriptFile))
            {

            }
        }

        private async Task<bool> ImportSingleSource(string importfileName)
        {
            try
            {
                ReadFile(importfileName);
                importedApps = JsonSerializer.Deserialize<List<ModellingImportAppData>>(importFile) ?? throw new Exception("File could not be parsed.");
                await ImportApps();
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"File {importfileName} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private void ReadFile(string filepath)
        {
            try
            {
                // /usr/local/fworch/etc/apps-<ImportSource>.json
                importFile = File.ReadAllText(filepath).Trim();
            }
            catch (Exception fileReadException)
            {
                Log.WriteError("Read file", $"File could not be found at {filepath}.", fileReadException);
                throw;
            }
        }

        private async Task<bool> ImportApps()
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
            Log.WriteDebug("Import App Data", $"Imported {successCounter} apps, {failCounter} failed. Deactivated {deleteCounter} apps, {deleteFailCounter} failed.");
            return true;
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
                dn = incomingApp.Modellers.Count > 0 ? incomingApp.Modellers.First() : "",  // todo
                groupDn = userGroupDn,
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                importSource = incomingApp.ImportSource
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
                dn = incomingApp.Modellers.Count > 0 ? incomingApp.Modellers.First() : "",  // todo
                groupDn = userGroupDn,
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality
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
            return "ModellerGroup_" + appName;
        }

        private string CreateUserGroup(ModellingImportAppData incomingApp)
        {
            string groupDn = "";
            if(incomingApp.Modellers.Count > 0 || incomingApp.ModellerGroups.Count > 0)
            {
                string groupName = GroupName(incomingApp.ExtAppId);
                groupDn = internalLdap.AddGroup(groupName, true);
                foreach(var modeller in incomingApp.Modellers)
                {
                    internalLdap.AddUserToEntry(modeller, groupDn);
                }
                foreach(var modellerGrp in incomingApp.ModellerGroups)
                {
                    internalLdap.AddUserToEntry(modellerGrp, groupDn);
                }
                internalLdap.AddUserToEntry(groupDn, roleDn);
            }
            return groupDn;
        }

        private string UpdateUserGroup(ModellingImportAppData incomingApp, string groupDn)
        {
            List<string> existingMembers = (allGroups.FirstOrDefault(x => x.GroupDn == groupDn) ?? throw new Exception("Group could not be found.")).Members;
            foreach(var modeller in incomingApp.Modellers)
            {
                if(!existingMembers.Contains(modeller))
                {
                    internalLdap.AddUserToEntry(modeller, groupDn);
                }
            }
            foreach(var modellerGrp in incomingApp.ModellerGroups)
            {
                if(!existingMembers.Contains(modellerGrp))
                {
                    internalLdap.AddUserToEntry(modellerGrp, groupDn);
                }
            }
            foreach(var member in existingMembers)
            {
                if(!incomingApp.Modellers.Contains(member) && !incomingApp.ModellerGroups.Contains(member))
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
                if(incomingApp.AppServers.FirstOrDefault(x => x.Name == existingAppServer.Name) == null)
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
                ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => x.Name == incomingAppServer.Name);
                if(existingAppServer == null)
                {
                    return await NewAppServer(incomingAppServer, appID, impSource);
                }
                else
                {
                    return await UpdateAppServer(incomingAppServer, appID, impSource, existingAppServer.Id);
                }
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
                    ip = IPAddressRange.Parse(incomingAppServer.Ip).ToCidrString(),   // todo ?
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

        private async Task<bool> UpdateAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource, long appServerId)
        {
            try
            {
                var Variables = new 
                {
                    id = appServerId,
                    name = incomingAppServer.Name,
                    appId = appID,
                    ip = IPAddressRange.Parse(incomingAppServer.Ip).ToCidrString(),   // todo ?
                    // subnet = incomingAppServer.Subnet,
                    importSource = impSource
                };
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.updateAppServer, Variables);
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task<bool> MarkDeletedAppServer(ModellingAppServer appServer)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.markAppServerDeleted, new { id = appServer.Id });
            }
            catch (Exception exc)
            {
                Log.WriteError("Import AppServer Data", $"Outdated AppServer {appServer.Name} could not be marked as deleted.", exc);
                return false;
            }
            return true;
        }
    }
}

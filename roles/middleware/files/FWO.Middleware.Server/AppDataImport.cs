using FWO.Logging;
using NetTools;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;
using FWO.Middleware.RequestParameters;



namespace FWO.Middleware.Server
{
    public class AppDataImport
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private string importFile { get; set; }

        // private List<FwoOwner> importedApps = new();
        private List<ModellingImportAppData> importedApps= new();
        private List<FwoOwner> existingApps = new();
        // private List<ModellingAppServer> importedAppServers = new();
        private List<ModellingAppServer> existingAppServers = new();

        private Ldap internalLdap;
        private string roleDn = "";
        List<GroupGetReturnParameters> allGroups = new();

        public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            Read();
        }

        private void Read()
        {
            try
            {
                // /usr/local/fworch/etc/apps.json
                importFile = File.ReadAllText(globalConfig.ImportAppDataPath).Trim();
            }
            catch (Exception fileReadException)
            {
                Log.WriteError("Read file", $"File could not be found at {globalConfig.ImportAppDataPath}.", fileReadException);
                throw;
            }
        }

        public async Task<bool> Run()
        {
            try
            {
                //ExtractFile();
                importedApps = JsonSerializer.Deserialize<List<ModellingImportAppData>>(importFile) ?? throw new Exception("File could not be parsed.");
                await InitLdap();
                await ImportApps();
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"File could not be processed.", exc);
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

        private static string GroupName(string appName)
        {
            return "ModellerGroup_" + appName;
        }

        // private void ExtractFile()
        // {
            
        //     // Todo: move to predefined import format
        //     importedApps = new List<FwoOwner>();
        //     var lines = importFile.Split('\n');
        //     FwoOwner newApp = new();
        //     foreach(var line in lines.Skip(1))
        //     {
        //         var values = line.Split(',');
        //         string appName = values[1].Replace("\"", "");
        //         string appExtId = values[2].Replace("\"", "");
        //         string appOwnerId = values[6].Replace("\"", "");
        //         string appCriticality = values[8].Replace("\"", "");

        //         string appServerName = values[14].Replace("\"", "");
        //         string appServerSubnet = values[15].Replace("\"", "");
        //         string appServerIpAddress = values[16].Replace("\"", "");

        //         if(importedApps.FirstOrDefault(x => x.Name == appName) == null)
        //         {
        //             importedApps.Add(new FwoOwner(){ Name = appName, ExtAppId = appExtId, Dn = appOwnerId, Criticality = appCriticality });
        //         }
        //         importedAppServers.Add(new ModellingAppServer(){ Name = appServerName, Ip = appServerIpAddress, ExtAppId = appExtId, IsDeleted = false });
        //     }
        // }

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

        private async Task<bool> ImportAppServers(ModellingImportAppData incomingApp, int applId)
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
            return true;
        }

        private async Task<bool> SaveAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => x.Name == incomingAppServer.Name);
                if(existingAppServer == null)
                {
                    await NewAppServer(incomingAppServer, appID, impSource);
                }
                else
                {
                    await UpdateAppServer(incomingAppServer, appID, impSource, existingAppServer.Id);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task NewAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            var Variables = new 
            {
                name = incomingAppServer.Name,
                appId = appID,
                ip = IPAddressRange.Parse(incomingAppServer.Ip).ToCidrString(),   // todo ?
                subnet = incomingAppServer.Subnet,
                importSource = impSource
            };
            await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAppServer, Variables);
        }

        private async Task UpdateAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource, long appServerId)
        {
            var Variables = new 
            {
                id = appServerId,
                name = incomingAppServer.Name,
                appId = appID,
                ip = IPAddressRange.Parse(incomingAppServer.Ip).ToCidrString(),   // todo ?
                subnet = incomingAppServer.Subnet,
                importSource = impSource
            };
            await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.updateAppServer, Variables);
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

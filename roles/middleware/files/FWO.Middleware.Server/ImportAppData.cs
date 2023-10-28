using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using NetTools;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;


namespace FWO.Middleware.Server
{
    public class ImportAppData
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private string importFile { get; set; }

        private List<FwoOwner> importedApps = new();
        private List<FwoOwner> existingApps = new();
        private List<ModellingAppServer> importedAppServers = new();
        private List<ModellingAppServer> existingAppServers = new();


        public ImportAppData(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            Read();
        }

        private void Read()
        {
            try
            {
                // /usr/local/fworch/etc/apps.csv
                importFile = File.ReadAllText(globalConfig.ImportAppDataPath).TrimEnd();
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
                ExtractFile();
                await ImportApps();
                await ImportAppServers();
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"File could not be processed.", exc);
                return false;
            }
            return true;
        }

        private void ExtractFile()
        {
            // Todo: move to predefined import format
            importedApps = new List<FwoOwner>();
            var lines = importFile.Split('\n');
            FwoOwner newApp = new();
            foreach(var line in lines.Skip(1))
            {
                var values = line.Split(',');
                string appName = values[1].Replace("\"", "");
                string appExtId = values[2].Replace("\"", "");
                string appOwnerId = values[6].Replace("\"", "");
                string appCriticality = values[8].Replace("\"", "");

                string appServerName = values[12].Replace("\"", "");
                string appServerSubnet = values[13].Replace("\"", "");
                string appServerIpAddress = values[14].Replace("\"", "");

                if(importedApps.FirstOrDefault(x => x.Name == appName) == null)
                {
                    importedApps.Add(new FwoOwner(){ Name = appName, ExtAppId = appExtId, Dn = appOwnerId, Criticality = appCriticality });
                }
                importedAppServers.Add(new ModellingAppServer(){ Name = appServerName, Ip = appServerIpAddress, ExtAppId = appExtId, IsDeleted = false });
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
                if(await saveApp(incomingApp))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            // foreach(var existingApp in existingApps)
            // {
            //     if(importedApps.FirstOrDefault(x => x.Name == existingApp.Name) == null)
            //     {
            //         if(await deleteApp(existingApp))
            //         {
            //             ++deleteCounter;
            //         }
            //         else
            //         {
            //             ++deleteFailCounter;
            //         }
            //     }
            // }
            Log.WriteDebug("Import App Data", $"Imported {successCounter} apps, {failCounter} failed. Deleted {deleteCounter} apps, {deleteFailCounter} failed.");
            return true;
        }

        private async Task<bool> ImportAppServers()
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            existingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(Api.Client.Queries.ModellingQueries.getImportedAppServers, new { importSource = "import" });
            foreach(var incomingAppServer in importedAppServers)
            {
                if(await saveAppServer(incomingAppServer))
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
                if(importedAppServers.FirstOrDefault(x => x.Name == existingAppServer.Name) == null)
                {
                    if(await markDeletedAppServer(existingAppServer))
                    {
                        ++deleteCounter;
                    }
                    else
                    {
                        ++deleteFailCounter;
                    }
                }
            }
            Log.WriteDebug("Import App Server Data", $"Imported {successCounter} app servers, {failCounter} failed. {deleteCounter} app servers marked as deleted, {deleteFailCounter} failed.");
            return true;
        }

        private async Task<bool> saveApp(FwoOwner incomingApp)
        {
            try
            {
                FwoOwner? existingApp = existingApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId);
                if(existingApp == null)
                {
                    await newApp(incomingApp);
                }
                else
                {
                    await updateApp(incomingApp, existingApp);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"App {incomingApp.Name} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task newApp(FwoOwner incomingApp)
        {
            var Variables = new 
            { 
                name = incomingApp.Name,
                dn = incomingApp.Dn,  // todo
                groupDn = incomingApp.Dn,  // todo
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality
            };
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.newOwner, Variables)).ReturnIds;
            if (returnIds != null)
            {
                importedApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId).Id = returnIds[0].NewId;
            }
        }

        private async Task updateApp(FwoOwner incomingApp, FwoOwner existingApp)
        {
            var Variables = new 
            {
                id = existingApp.Id,
                name = incomingApp.Name,
                dn = incomingApp.Dn,  // todo
                groupDn = incomingApp.Dn,  // todo
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality
            };
            await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.updateOwner, Variables);
        }

        // private async Task<bool> deleteApp(FwoOwner app)
        // {
        //     try
        //     {
        //         await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.OwnerQueries.deleteOwner, new { id = app.Id });
        //     }
        //     catch (Exception exc)
        //     {
        //         Log.WriteError("Import App Data", $"Outdated App {app.Name} could not be deleted.", exc);
        //         return false;
        //     }
        //     return true;
        // }

        private async Task<bool> saveAppServer(ModellingAppServer incomingAppServer)
        {
            try
            {
                ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => x.Name == incomingAppServer.Name);
                if(existingAppServer == null)
                {
                    await newAppServer(incomingAppServer);
                }
                else
                {
                    await updateAppServer(incomingAppServer, existingAppServer);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task newAppServer(ModellingAppServer incomingAppServer)
        {
            int appID = importedApps.FirstOrDefault(x => x.ExtAppId == incomingAppServer.ExtAppId)?.Id ?? 0;
            var Variables = new 
            {
                name = incomingAppServer.Name,
                appId = appID,
                ip = incomingAppServer.Ip,   // todo ?
                importSource = "import"  // todo
            };
            await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAppServer, Variables);
        }

        private async Task updateAppServer(ModellingAppServer incomingAppServer, ModellingAppServer existingAppServer)
        {
            int appID = importedApps.FirstOrDefault(x => x.ExtAppId == incomingAppServer.ExtAppId)?.Id ?? 0;
            var Variables = new 
            {
                id = existingAppServer.Id,
                name = incomingAppServer.Name,
                appId = appID, // todo ?
                ip = incomingAppServer.Ip,   // todo ?
                importSource = "import"  // todo
            };
            await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.updateAppServer, Variables);
        }

        private async Task<bool> markDeletedAppServer(ModellingAppServer appServer)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.markAppServerDeleted, new { id = appServer.Id });
            }
            catch (Exception exc)
            {
                Log.WriteError("Import AppServer Data", $"Outdated AppServer {appServer.Name} could not be deleted.", exc);
                return false;
            }
            return true;
        }
    }
}

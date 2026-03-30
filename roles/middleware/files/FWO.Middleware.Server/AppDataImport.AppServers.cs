using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Services;
using FWO.Services.Modelling;

namespace FWO.Middleware.Server
{
    public partial class AppDataImport
    {
        private static HashSet<string> BuildAppServerKeys(IEnumerable<ModellingAppServer> appServers)
        {
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            foreach (ModellingAppServer appServer in appServers.Where(appServer => !appServer.IsDeleted))
            {
                string ip = string.IsNullOrWhiteSpace(appServer.Ip) ? "" : appServer.Ip.Trim().IpAsCidr();
                string ipEnd = string.IsNullOrWhiteSpace(appServer.IpEnd) ? ip : appServer.IpEnd.Trim().IpAsCidr();
                string name = string.IsNullOrWhiteSpace(appServer.Name) ? "" : appServer.Name.Trim();
                keys.Add($"{ip}|{ipEnd}|{name}");
            }

            return keys;
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
            ExistingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, Variables);
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

            foreach (var existingAppServer in ExistingAppServers.Where(e => !e.IsDeleted).ToList())
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

                ModellingAppServer? existingAppServer = ExistingAppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == incomingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == incomingAppServer.IpEnd.IpAsCidr());
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
                    await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, existingAppServer);
                }

                if (!existingAppServer.Name.Equals(incomingAppServer.Name)
                    && !await UpdateAppServerName(existingAppServer, incomingAppServer.Name))
                {
                    return false;
                }

                if (existingAppServer.CustomType == null
                    && !await UpdateAppServerType(existingAppServer))
                {
                    return false;
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
                    await ModellingHandlerBase.LogChange(new LogChangeRequest
                    {
                        ChangeType = ModellingTypes.ChangeType.Insert,
                        ObjectType = ModellingTypes.ModObjectType.AppServer,
                        ObjectId = newModAppServer.Id,
                        Text = $"New App Server: {newModAppServer.Display()}",
                        ApiConnection = apiConnection,
                        UserConfig = userConfig,
                        ApplicationId = newModAppServer.AppId,
                        DisplayMessageInUi = DefaultInit.DoNothing,
                        ChangeSource = newModAppServer.ImportSource
                    });
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
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = ModellingTypes.ChangeType.Reactivate,
                    ObjectType = ModellingTypes.ModObjectType.AppServer,
                    ObjectId = appServer.Id,
                    Text = $"Reactivate App Server: {appServer.Display()}",
                    ApiConnection = apiConnection,
                    UserConfig = userConfig,
                    ApplicationId = appServer.AppId,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    ChangeSource = appServer.ImportSource
                });
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
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = ModellingTypes.ChangeType.Update,
                    ObjectType = ModellingTypes.ModObjectType.AppServer,
                    ObjectId = appServer.Id,
                    Text = $"Update App Server Type: {appServer.Display()}",
                    ApiConnection = apiConnection,
                    UserConfig = userConfig,
                    ApplicationId = appServer.AppId,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    ChangeSource = appServer.ImportSource
                });
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
                    await ModellingHandlerBase.LogChange(new LogChangeRequest
                    {
                        ChangeType = ModellingTypes.ChangeType.Update,
                        ObjectType = ModellingTypes.ModObjectType.AppServer,
                        ObjectId = appServer.Id,
                        Text = $"Update App Server Name: {appServer.Display()}",
                        ApiConnection = apiConnection,
                        UserConfig = userConfig,
                        ApplicationId = appServer.AppId,
                        DisplayMessageInUi = DefaultInit.DoNothing,
                        ChangeSource = appServer.ImportSource
                    });
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
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = ModellingTypes.ChangeType.Update,
                    ObjectType = ModellingTypes.ModObjectType.AppServer,
                    ObjectId = appServer.Id,
                    Text = $"Deactivate App Server: {appServer.Display()}",
                    ApiConnection = apiConnection,
                    UserConfig = userConfig,
                    ApplicationId = appServer.AppId,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    ChangeSource = appServer.ImportSource
                });
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
    }
}

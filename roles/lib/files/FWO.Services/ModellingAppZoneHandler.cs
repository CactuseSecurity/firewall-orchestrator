using FWO.Api.Client;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Linq;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, FwoOwner owner) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi)
    {
        private ModellingNamingConvention NamingConvention = new();

        public async Task CreateAppZone(int appId)
        {
            if (owner is null || owner.Id != appId)
            {
                string errorMessage = $"{userConfig.GetText("app_owner_not_found")}: App-Id: {appId}";
                Exception exception = new ArgumentException(errorMessage);
                DisplayMessageInUi(exception, userConfig.GetText("app_zone_creation"), errorMessage, false);
                return;
            }

            await CreateAppZone();
        }

        public async Task<ModellingAppZone?> CreateAppZone()
        {
            ModellingAppZone appZone = new()
            {
                AppId = owner.Id,
            };

            ApplyNamingConvention(owner.ExtAppId.ToUpper(), appZone);

            List<ModellingAppServer> appServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = owner.Id });

            foreach (ModellingAppServer appServer in appServers)
            {
                appZone.AppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }

            int appZoneId = await AddAppZoneToDb(appZone);

            appZone.Id = appZoneId;

            await AddAppServersToAppZone(appZoneId, appZone.AppServers);

            return appZone;
        }

        public async Task<ModellingAppZone?> UpsertAppZone()
        {
            ModellingAppZone? appZone;

            List<ModellingAppServer> tempAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = owner.Id });
            List<ModellingAppServerWrapper> allAppServers = [];

            foreach (ModellingAppServer appServer in tempAppServers)
            {
                allAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }

            appZone = await GetExistingAppZone(owner.Id);

            if (appZone is null)
            {
                appZone = new()
                {
                    AppId = owner.Id
                };

                ApplyNamingConvention(owner.ExtAppId.ToUpper(), appZone);

                appZone.AppServers.AddRange(allAppServers);

                int newAppZoneId = await AddAppZoneToDb(appZone);                
                appZone.Id = newAppZoneId;

                await AddAppServersToAppZone(appZone.Id, appZone.AppServers);
            }
            else
            {
                List<ModellingAppServerWrapper>? removedAppServers = FindRemovedAppServers(appZone, allAppServers);

                if (removedAppServers.Count > 0)
                {
                    await RemoveAppServersFromAppZone(appZone.Id, removedAppServers);

                    appZone.AppServers.RemoveAll(_ => removedAppServers.Contains(_));
                }

                List<ModellingAppServerWrapper>? newAppServers = FindNewAppServers(appZone, allAppServers);

                if (newAppServers.Count > 0)
                {
                    await AddAppServersToAppZone(appZone.Id, newAppServers);
                    appZone.AppServers.AddRange(newAppServers);
                }
            }

            return appZone;
        }

        private static List<ModellingAppServerWrapper> FindNewAppServers(ModellingAppZone existingAppZone, List<ModellingAppServerWrapper> allAppServers)
        {
            return allAppServers.Except(existingAppZone.AppServers, new AppServerComparer()).ToList();
        }

        private static List<ModellingAppServerWrapper> FindRemovedAppServers(ModellingAppZone existingAppZone, List<ModellingAppServerWrapper> allAppServers)
        {
            return existingAppZone.AppServers.Except(allAppServers, new AppServerComparer()).ToList();
        }

        public async Task<ModellingAppZone?> CreateAppZone(ModellingConnection conn, FwoOwner owner)
        {
            if (conn is null || ( conn.SourceAppServers.Count == 0 && conn.DestinationAppServers.Count == 0 ))
                return default;

            ModellingAppZone appZone = new()
            {
                AppId = conn.AppId,
            };

            ApplyNamingConvention(owner.ExtAppId.ToUpper(), appZone);

            foreach (ModellingAppServerWrapper srcAppServer in conn.SourceAppServers)
            {
                appZone.AppServers.Add(srcAppServer);
            }

            foreach (ModellingAppServerWrapper dstAppServer in conn.DestinationAppServers)
            {
                appZone.AppServers.Add(dstAppServer);
            }

            int appZoneId = await AddAppZoneToDb(appZone);

            appZone.Id = appZoneId;

            await AddAppServersToAppZone(appZoneId, appZone.AppServers);

            return appZone;
        }

        private async Task DeleteExistingAppZone(int ownerId)
        {
            ModellingAppZone? existingAppZone = await GetExistingAppZone(ownerId);

            if (existingAppZone is not null)
            {
                try
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteNwGroup, new { id = existingAppZone.Id });

                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.AppZone, existingAppZone.Id, $"Delete App Zone: {existingAppZone.Display()}", ownerId);
                }
                catch (Exception ex)
                {
                    DisplayMessageInUi(ex, userConfig.GetText("delete_app_zone"), userConfig.GetText("E9201"), true);
                }
            }
        }

        public async Task<ModellingAppZone?> GetExistingAppZone(int appId)
        {
            try
            {
                List<ModellingAppZone>? existingAppZones = await apiConnection.SendQueryAsync<List<ModellingAppZone>>(ModellingQueries.getAppZonesByAppId, new { appId = appId });

                return existingAppZones.FirstOrDefault();
            }
            catch (Exception ex)
            {
                DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), userConfig.GetText("E9203"), true);
            }

            return default;
        }

        private void ApplyNamingConvention(string extAppId, ModellingAppZone appZone)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            appZone.ManagedIdString.NamingConvention = NamingConvention;
            appZone.ManagedIdString.SetAppPartFromExtIdAZ(extAppId);
        }

        private async Task<int> AddAppZoneToDb(ModellingAppZone appZone)
        {
            var azVars = new
            {
                appId = appZone.AppId,
                name = appZone.Name,
                idString = appZone.IdString,
                creator = "CreateAZObjects"
            };

            try
            {
                ReturnId[]? returnIds = ( await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppZone, azVars) ).ReturnIds;

                await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppZone, appZone.Id, $"New App Zone: {appZone.Display()}", null);

                if (returnIds != null && returnIds.Length > 0)
                    return returnIds[0].NewId;
            }
            catch (Exception ex)
            {
                DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), userConfig.GetText("E9202"), true);
            }

            return -1;
        }

        public async Task AddAppServersToAppZone(long appZoneId, List<ModellingAppServerWrapper> appServers)
        {
            foreach (ModellingAppServerWrapper appServer in appServers)
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Content.Id,
                    nwGroupId = appZoneId
                };

                try
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, nwobject_nwgroupVars);
                }
                catch (Exception ex)
                {
                    DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), userConfig.GetText("E9204"), true);
                }
            }
        }

        public async Task RemoveAppServersFromAppZone(long appZoneId, List<ModellingAppServerWrapper> appServers)
        {
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(appServers))
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Id,
                    nwGroupId = appZoneId
                };

                try
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwObjectFromNwGroup, nwobject_nwgroupVars);
                }
                catch (Exception ex)
                {
                    DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), userConfig.GetText("E9204"), true);
                }
            }
        }
    }
}

using FWO.Api.Client;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi)
    {
        private ModellingNamingConvention NamingConvention = new();

        public async Task<ModellingAppZone?> UpsertAppZone()
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            List<ModellingAppServer> tempAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersForOwner, new { appId = owner.Id });
            List<ModellingAppServerWrapper> allAppServers = [];

            foreach (ModellingAppServer appServer in tempAppServers.Where(a => !a.IsDeleted))
            {
                allAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }

            ModellingAppZone? appZone = await GetExistingAppZone();

            if (appZone is null)
            {
                appZone = new(owner.Id);
                ApplyNamingConvention(owner.ExtAppId?.ToUpper(), appZone);
                appZone.AppServers.AddRange(allAppServers);
                appZone.Id = await AddAppZoneToDb(appZone);                
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

        private List<ModellingAppServerWrapper> FindNewAppServers(ModellingAppZone existingAppZone, List<ModellingAppServerWrapper> allAppServers)
        {
            return allAppServers.Except(existingAppZone.AppServers, new AppServerComparer(NamingConvention)).ToList();
        }

        private List<ModellingAppServerWrapper> FindRemovedAppServers(ModellingAppZone existingAppZone, List<ModellingAppServerWrapper> allAppServers)
        {
            return existingAppZone.AppServers.Except(allAppServers, new AppServerComparer(NamingConvention)).ToList();
        }

        public async Task<ModellingAppZone?> GetExistingAppZone()
        {
            try
            {
                List<ModellingAppZone>? existingAppZones = await apiConnection.SendQueryAsync<List<ModellingAppZone>>(ModellingQueries.getAppZonesByAppId, new { appId = owner.Id });
                return existingAppZones.FirstOrDefault();
            }
            catch (Exception ex)
            {
                DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), userConfig.GetText("E9203"), true);
            }
            return default;
        }

        private void ApplyNamingConvention(string? extAppId, ModellingAppZone appZone)
        {
            appZone.ManagedIdString.NamingConvention = NamingConvention;
            if(extAppId != null)
            {
                appZone.ManagedIdString.SetAppPartFromExtIdAZ(extAppId);
            }
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

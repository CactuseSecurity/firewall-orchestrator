using FWO.Api.Client;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool>? displayMessageInUi = default) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi)
    {
        private ModellingNamingConvention NamingConvention = new();

        public async Task<ModellingAppZone?> PlanAppZoneUpsert(List<ModellingAppServerWrapper>? diffAppServers = default)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            List<ModellingAppServer> tempAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = owner.Id });
            List<ModellingAppServerWrapper> allAppServers = [];

            foreach (ModellingAppServer appServer in tempAppServers.Where(a => !a.IsDeleted))
            {
                allAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }

            ModellingAppZone? appZone = await GetExistingAppZone();

            if (appZone is null)
            {
                appZone = new(owner.Id)
                {
                    Exists = false
                };

                ApplyNamingConvention(owner.ExtAppId?.ToUpper(), appZone);
                appZone.AppServersNew = allAppServers;
                appZone.AppServers = allAppServers;
            }
            else
            {
                appZone.Exists = true;

                ApplyNamingConvention(owner.ExtAppId?.ToUpper(), appZone);

                List<ModellingAppServerWrapper>? removedAppServers = FindRemovedAppServers(appZone, allAppServers);

                if (removedAppServers.Count > 0)
                {
                    appZone.AppServersRemoved = removedAppServers;
                    appZone.AppServers.RemoveAll(_ => removedAppServers.Contains(_));
                }

                List<ModellingAppServerWrapper>? newAppServers = FindNewAppServers(appZone, allAppServers);

                if (newAppServers.Count > 0)
                {
                    appZone.AppServersNew = newAppServers;
                    appZone.AppServers.AddRange(newAppServers);
                }
            }

            if (diffAppServers is not null)
            {
                List<ModellingAppServerWrapper>? removedAppServers = FindRemovedAppServers(new ModellingAppZone() { AppServers = diffAppServers}, appZone.AppServers);

                if (removedAppServers.Count > 0)
                {
                    appZone.AppServersRemoved = removedAppServers;
                    appZone.AppServers.RemoveAll(_ => removedAppServers.Contains(_));
                }

                List<ModellingAppServerWrapper>? newAppServers = FindNewAppServers(new ModellingAppZone() { AppServers = diffAppServers }, appZone.AppServers);

                if (newAppServers.Count > 0)
                {
                    appZone.AppServersNew = newAppServers;
                    appZone.AppServers.AddRange(newAppServers);
                }
            }

            return appZone;
        }

        public async Task<ModellingAppZone?> UpsertAppZone(ModellingAppZone appZone)
        {

            if (!appZone.Exists)
            {
                appZone.Id = await AddAppZoneToDb(appZone);
                await AddAppServersToAppZone(appZone.Id, appZone.AppServers);
            }
            else
            {
                if (appZone.AppServersRemoved.Count > 0)
                {
                    await RemoveAppServersFromAppZone(appZone.Id, appZone.AppServersRemoved);
                }

                if (appZone.AppServersNew.Count > 0)
                {
                    await AddAppServersToAppZone(appZone.Id, appZone.AppServersNew);
                }
            }

            return appZone;
        }

        private List<ModellingAppServerWrapper> FindNewAppServers(ModellingAppZone existingAppZone, List<ModellingAppServerWrapper> allAppServers)
        {
            List<ModellingAppServerWrapper> newAppServers = [];

            foreach (ModellingAppServerWrapper appserver in allAppServers)
            {
                if (existingAppZone.AppServers.FirstOrDefault(a => new AppServerComparer(NamingConvention).Equals(a.Content, appserver.Content)) == null)
                {
                    newAppServers.Add(appserver);
                }
            }

            return newAppServers;
        }

        private List<ModellingAppServerWrapper> FindRemovedAppServers(ModellingAppZone existingAppZone, List<ModellingAppServerWrapper> allAppServers)
        {
            List<ModellingAppServerWrapper> deletedAppServers = [];

            foreach (ModellingAppServerWrapper exAppserver in existingAppZone.AppServers)
            {
                if (allAppServers.FirstOrDefault(a => new AppServerComparer(NamingConvention).Equals(exAppserver.Content, a.Content)) == null)
                {
                    deletedAppServers.Add(exAppserver);
                }
            }

            return deletedAppServers;
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
            if (extAppId != null)
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

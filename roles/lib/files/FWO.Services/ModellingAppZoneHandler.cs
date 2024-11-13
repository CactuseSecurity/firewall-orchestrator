using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using System.Text.Json;
using System;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi)
    {
        public ModellingNamingConvention NamingConvention = new();
        public List<ModellingAppZone> AppZones { get; set; } = [];

        public async Task CreateAppZones(string extAppId)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);

            FwoOwner? owner = owners.FirstOrDefault(_ => _.ExtAppId == extAppId);

            if (owner is null)
            {
                string errorMessage = $"{userConfig.GetText("app_owner_not_found")}: External-App-Id: {extAppId}";
                Exception exception = new ArgumentException(errorMessage);
                DisplayMessageInUi(exception, userConfig.GetText("app_zone_creation"), errorMessage, false);
                return;
            }

            await CreateAppZones(owner);
        }

        public async Task CreateAppZones(int appId)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);

            FwoOwner? owner = owners.FirstOrDefault(_ => _.Id == appId);

            if (owner is null)
            {
                DisplayMessageInUi(null, userConfig.GetText("app_zone_creation"), userConfig.GetText("app_owner_not_found"), false);
                return;
            }

            await CreateAppZones(owner);
        }

        private async Task CreateAppZones(FwoOwner owner)
        {
            await DeleteExistingAppZones(owner.Id);

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

            await AddAppServerToAppZone(appZone);
        }

        private async Task DeleteExistingAppZones(int ownerId)
        {
            (bool success, List<ModellingAppZone>? existingAppZones) = await GetExistingAppZone(ownerId);

            if (success && existingAppZones is not null && existingAppZones.Count > 0)
            {
                foreach (ModellingAppZone existingAppZone in existingAppZones)
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
        }

        private async Task<(bool, List<ModellingAppZone>?)> GetExistingAppZone(int appId)
        {
            try
            {
                List<ModellingAppZone> existingAppZones = await apiConnection.SendQueryAsync<List<ModellingAppZone>>(ModellingQueries.getAppZonesByAppId, new { appId = appId });
                
                if (existingAppZones is not null)
                    return (true, existingAppZones);

            }
            catch (Exception ex)
            {
                DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), userConfig.GetText("E9203"), true);
            }

            return (false, default);
        }

        private void ApplyNamingConvention(string extAppId, ModellingAppZone appZone)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            appZone.ManagedIdString.NamingConvention = NamingConvention;
            appZone.ManagedIdString.SetAppPartFromExtId(extAppId);
            appZone.Name = $"{NamingConvention.AppZone}{appZone.ManagedIdString.AppPart}";
        }

        private async Task<int> AddAppZoneToDb(ModellingAppZone appZone)
        {
            try
            {
                var azVars = new
                {
                    appId = appZone.AppId,
                    name = appZone.Name,
                    idString = appZone.IdString,
                    creator = "CreateAZObjects"
                };

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

        private async Task AddAppServerToAppZone(ModellingAppZone appZone)
        {
            foreach (ModellingAppServerWrapper appServer in appZone.AppServers)
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Content.Id,
                    nwGroupId = appZone.Id
                };

                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, nwobject_nwgroupVars);
            }
        }
    }
}

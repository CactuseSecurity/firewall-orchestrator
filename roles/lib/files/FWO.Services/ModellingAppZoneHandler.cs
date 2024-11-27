﻿using FWO.Api.Client;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi)
    {
        private ModellingNamingConvention NamingConvention = new();

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

            await CreateAppZone(owner);
        }

        public async Task CreateAppZones(int appId)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);

            FwoOwner? owner = owners.FirstOrDefault(_ => _.Id == appId);

            if (owner is null)
            {
                string errorMessage = $"{userConfig.GetText("app_owner_not_found")}: App-Id: {appId}";
                Exception exception = new ArgumentException(errorMessage);
                DisplayMessageInUi(exception, userConfig.GetText("app_zone_creation"), errorMessage, false);
                return;
            }

            await CreateAppZone(owner);
        }

        public async Task<ModellingAppZone>? CreateAppZone(FwoOwner owner)
        {
            //await DeleteExistingAppZones(owner.Id);

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

            await AddAppServersToAppZone(appZone);

            return appZone;
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

            //await DeleteExistingAppZones(appZone.AppId);

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

            await AddAppServersToAppZone(appZone);

            return appZone;
        }

        private async Task DeleteExistingAppZones(int? ownerId)
        {
            (bool success, List<ModellingAppZone>? existingAppZones) = await GetExistingAppZones(ownerId);

            if (success && existingAppZones is not null)
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

        public async Task<(bool, List<ModellingAppZone>?)> GetExistingAppZones(int? appId)
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

        public async Task AddAppServersToAppZone(ModellingAppZone appZone)
        {
            foreach (ModellingAppServerWrapper appServer in appZone.AppServers)
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Content.Id,
                    nwGroupId = appZone.Id
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
    }
}
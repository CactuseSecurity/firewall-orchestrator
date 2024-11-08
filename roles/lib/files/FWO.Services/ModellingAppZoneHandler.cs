using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FWO.Services
{
    public class ModellingAppZoneHandler : ModellingHandlerBase
    {
        public ModellingNamingConvention NamingConvention = new();
        public List<ModellingAppZone> AppZones { get; set; } = [];

        public ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi) :
            base(apiConnection, userConfig, displayMessageInUi)
        {

        }
        public async Task CreateAppZones(int appId)
        {
            List<ModellingAppServer> appServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = appId });
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);

            FwoOwner? owner = owners.FirstOrDefault(_ => _.Id == appId);

            if (owner is null)
            {
                string errorMessage = $"{userConfig.GetText("app_owner_not_found")}: App-Id: {appId}";
                Exception exception = new ArgumentException(errorMessage);
                DisplayMessageInUi(exception, userConfig.GetText("network_modelling"), errorMessage, false);
                return;
            }

            foreach (ModellingAppServer appServer in appServers)
            {
                ModellingAppZone appZone = new();
                appZone.AppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
                ApplyNamingConvention(owner.ExtAppId.ToUpper(), appZone);
                await AddAppZoneToDb(appZone);
            }
        }

        private void ApplyNamingConvention(string extAppId, ModellingAppZone appZone)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            appZone.ManagedIdString.NamingConvention = NamingConvention;
            appZone.ManagedIdString.SetAppPartFromExtId(extAppId);
            appZone.Name = $"{NamingConvention.AppZone}{appZone.ManagedIdString.AppPart}";
        }

        private async Task AddAppZoneToDb(ModellingAppZone appZone)
        {
            try
            {
                var azVars = new
                {
                    name = appZone.Name,
                    idString = appZone.IdString,
                    creator = "CreateAZObjects"
                };

                ReturnId[]? returnIds = ( await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppZone, azVars) ).ReturnIds;

                if (returnIds != null)
                {
                    appZone.Id = returnIds[0].NewId;

                    foreach (var appServer in appZone.AppServers)
                    {
                        var nwobject_nwgroupVars = new
                        {
                            nwObjectId = appServer.Content.Id,
                            nwGroupId = appZone.Id
                        };

                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, nwobject_nwgroupVars);

                        await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppZone, appZone.Id, $"New App Zone: {appZone.Display()}", appZone.AppId);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("app_zone_creation"), "", true);
            }
        }
    }
}

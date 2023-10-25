using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ModellingServiceHandler
    {
        public FwoOwner Application { get; set; } = new();
        public NetworkService ActService { get; set; } = new();
        public bool AddMode { get; set; } = false;
        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingServiceHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            NetworkService service, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            ActService = service;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }
        
        public async Task Save()
        {
            if(AddMode)
            {
                await AddServiceToDb();
            }
            else
            {
                await UpdateServiceInDb();
            }
        }

        private async Task AddServiceToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActService.Name,
                    appId = Application.Id,
                    port = ActService.DestinationPort,
                    portEnd = ActService.DestinationPortEnd,
                    protoId = ActService.Protocol.Id
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.newService, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActService.Id = returnIds[0].NewId;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_service"), "", true);
            }
        }

        private async Task UpdateServiceInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActService.Id,
                    name = ActService.Name,
                    appId = Application.Id,
                    port = ActService.DestinationPort,
                    portEnd = ActService.DestinationPortEnd,
                    protoId = ActService.Protocol.Id
                };
                await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.updateService, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }
    }
}

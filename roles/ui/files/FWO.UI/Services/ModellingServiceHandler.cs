using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingServiceHandler
    {
        public FwoOwner Application { get; set; } = new();
        public ModellingService ActService { get; set; } = new();
        public List<ModellingService> AvailableServices { get; set; } = new();
        public bool AddMode { get; set; } = false;
        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingServiceHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            ModellingService service, List<ModellingService> availableServices, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            ActService = service;
            AvailableServices = availableServices;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }
        
        public async Task Save()
        {
            try
            {
                if (ActService.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_service"), userConfig.GetText("U0001"), true);
                }
                if(checkService())
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
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }

        private bool checkService()
        {
            if(ActService.Name == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_service"), userConfig.GetText("Exxxx"), true);
                return false;
            }
            return true;
        }

        private async Task AddServiceToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActService.Name,
                    appId = Application.Id,
                    port = ActService.Port,
                    portEnd = ActService.PortEnd,
                    protoId = ActService.Protocol.Id
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newService, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActService.Id = returnIds[0].NewId;
                    AvailableServices.Add(ActService);
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
                    port = ActService.Port,
                    portEnd = ActService.PortEnd,
                    protoId = ActService.Protocol.Id
                };
                await ApiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateService, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }
    }
}

using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.GlobalConstants;


namespace FWO.Services
{
    public class ModellingServiceHandler : ModellingHandlerBase
    {
        public ModellingService ActService { get; set; } = new();
        private ModellingService ActServiceOrig { get; set; } = new();


        public ModellingServiceHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            ModellingService service, List<ModellingService> availableServices, List<KeyValuePair<int, int>> availableSvcElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, bool isOwner = true)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi, false, isOwner)
        {
            ActService = service;
            AvailableServices = availableServices;
            AvailableSvcElems = availableSvcElems;
            ActServiceOrig = new ModellingService(ActService);
        }
        
        public async Task<bool> Save()
        {
            try
            {
                if (ActService.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_service"), userConfig.GetText("U0001"), true);
                }
                if(CheckService())
                {
                    if(AddMode)
                    {
                        await AddServiceToDb();
                    }
                    else
                    {
                        await UpdateServiceInDb();
                    }
                    ActServiceOrig = new ModellingService(ActService);
                    return true;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
            return false;
        }

        public void Reset()
        {
            ActService = new(ActServiceOrig);
            if(!AddMode)
            {
                AvailableServices[AvailableServices.FindIndex(x => x.Id == ActService.Id)] = ActService;
            }
        }

        private bool CheckService()
        {
            if(ActService.Protocol == null || ActService.Protocol.Id == 0)
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_service"), userConfig.GetText("E5102"), true);
                return false;
            }
            if(ActService.PortEnd == null)
            {
                ActService.PortEnd = ActService.Port;
            }
            if(ActService.Protocol.Name.ToLower().StartsWith("esp"))
            {
                ActService.Port = null;
                ActService.PortEnd = null;
            }
            else if(ActService.Port == null)
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_service"), userConfig.GetText("E5102"), true);
                return false;
            }
            if(ActService.Port < 1 || ActService.Port > GlobalConst.kMaxPortNumber ||
                (ActService.PortEnd != null &&  (ActService.PortEnd < 1 || ActService.PortEnd > GlobalConst.kMaxPortNumber)))
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_service"), userConfig.GetText("E5103"), true);
                return false;
            }
            if(ActService.PortEnd != null && (ActService.PortEnd < ActService.Port))
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_service"), userConfig.GetText("E5118"), true);
                return false;
            }
            return true;
        }

        private async Task AddServiceToDb()
        {
            try
            {
                int? applicationId = ActService.IsGlobal ? null : Application.Id;
                var Variables = new
                {
                    name = ActService.Name,
                    appId = applicationId,
                    isGlobal = ActService.IsGlobal,
                    port = ActService.Port,
                    portEnd = ActService.PortEnd,
                    protoId = ActService.Protocol?.Id
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newService, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActService.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.Service, ActService.Id,
                        $"New Service: {ActService.Display()}", Application.Id);
                    AvailableServices.Add(ActService);
                    AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, ActService.Id));
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
                    port = ActService.Port,
                    portEnd = ActService.PortEnd,
                    protoId = ActService.Protocol?.Id
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateService, Variables);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.Service, ActService.Id,
                    $"Updated Service: {ActService.Display()}", Application.Id);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }
    }
}

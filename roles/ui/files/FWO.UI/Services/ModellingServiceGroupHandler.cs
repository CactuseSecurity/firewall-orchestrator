using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingServiceGroupHandler
    {
        public FwoOwner Application { get; set; } = new();
        public List<ModellingServiceGroup> ServiceGroups { get; set; } = new();
        public ModellingServiceGroup ActServiceGroup { get; set; } = new();
        public List<ModellingService> AvailableServices { get; set; } = new();
        public bool AddMode { get; set; } = false;

        public ModellingServiceHandler ServiceHandler;
        public List<ModellingService> SvcToAdd { get; set; } = new();
        public List<ModellingService> SvcToDelete { get; set; } = new();
        public bool AddServiceMode = false;
        public bool EditServiceMode = false;
        public bool DeleteServiceMode = false;
        public string deleteMessage = "";
        private ModellingService actService = new();

        private readonly ApiConnection apiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingServiceGroupHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingServiceGroup> serviceGroups, ModellingServiceGroup serviceGroup, List<ModellingService> availableServices,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            this.apiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            ServiceGroups = serviceGroups;
            ActServiceGroup = serviceGroup;
            AvailableServices = availableServices;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }

        public void ServicesToSvcGroup(List<ModellingService> services)
        {
            foreach(var svc in services)
            {
                if(ActServiceGroup.Services.FirstOrDefault(w => w.Content.Id == svc.Id) == null && !SvcToAdd.Contains(svc))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
        
        public async Task CreateService()
        {
            AddServiceMode = true;
            await HandleService(new ModellingService(){});
        }

        public async Task EditService(ModellingService service)
        {
            AddServiceMode = false;
            await HandleService(service);
        }

        public async Task HandleService(ModellingService service)
        {
            ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service, AvailableServices, AddServiceMode, DisplayMessageInUi);
            EditServiceMode = true;
        }

        public void RequestDeleteService(ModellingService service)
        {
            actService = service;
            deleteMessage = userConfig.GetText("U9003") + service.Name + "?";
            DeleteServiceMode = true;
        }

        public async Task DeleteService()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.deleteService, new { id = actService.Id })).AffectedRows > 0)
                {
                    AvailableServices.Remove(actService);
                    DeleteServiceMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service"), "", true);
            }
        }

        public async Task Save()
        {
            foreach(var svc in SvcToDelete)
            {
                ActServiceGroup.Services.Remove(ActServiceGroup.Services.FirstOrDefault(x => x.Content.Id == svc.Id));
            }
            foreach(var svc in SvcToAdd)
            {
                ActServiceGroup.Services.Add(new ModellingServiceWrapper(){ Content = svc });
            }
            if(AddMode)
            {
                await AddServiceGroupToDb();
            }
            else
            {
                await UpdateServiceGroupInDb();
            }
            Close();
        }

        public async Task AddServiceGroupToDb()
        {
            try
            {
                var svcGrpParams = new
                {
                    name = ActServiceGroup.Name,
                    appId = Application.Id,
                    comment = ActServiceGroup.Comment,
                    isGlobal = false
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newServiceGroup, svcGrpParams)).ReturnIds;
                if (returnIds != null)
                {
                    ActServiceGroup.Id = returnIds[0].NewId;
                    foreach(var service in ActServiceGroup.Services)
                    {
                        var svcParams = new
                        {
                            serviceId = service.Content.Id,
                            serviceGroupId = ActServiceGroup.Id
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToServiceGroup, svcParams);
                    }
                    ServiceGroups.Add(ActServiceGroup);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_service_group"), "", true);
            }
        }

        public async Task UpdateServiceGroupInDb()
        {
            try
            {
                var svcGrpParams = new
                {
                    id = ActServiceGroup.Id,
                    name = ActServiceGroup.Name,
                    appId = Application.Id,
                    comment = ActServiceGroup.Comment,
                    isGlobal = false
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateServiceGroup, svcGrpParams);
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromServiceGroup, svcParams);
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToServiceGroup, svcParams);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
        }

        public void Close()
        {
            SvcToAdd = new List<ModellingService>();
            SvcToDelete = new List<ModellingService>();
        }
    }
}

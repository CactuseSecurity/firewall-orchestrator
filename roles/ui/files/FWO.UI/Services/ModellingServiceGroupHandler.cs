using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingServiceGroupHandler : ModellingHandlerBase
    {
        public List<ModellingServiceGroup> ServiceGroups { get; set; } = new();
        public ModellingServiceGroup ActServiceGroup { get; set; } = new();
        public List<ModellingService> AvailableServices { get; set; } = new();

        public ModellingServiceHandler ServiceHandler;
        public List<ModellingService> SvcToAdd { get; set; } = new();
        public List<ModellingService> SvcToDelete { get; set; } = new();
        public bool AddServiceMode = false;
        public bool EditServiceMode = false;
        public bool DeleteServiceMode = false;
        public string deleteMessage = "";
        private ModellingService actService = new();


        public ModellingServiceGroupHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingServiceGroup> serviceGroups, ModellingServiceGroup serviceGroup, List<ModellingService> availableServices,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi)
        {
            ServiceGroups = serviceGroups;
            ActServiceGroup = serviceGroup;
            ActServiceGroup.AppId = application.Id;
            AvailableServices = availableServices;
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
        
        public void CreateService()
        {
            AddServiceMode = true;
            HandleService(new ModellingService(){});
        }

        public void EditService(ModellingService service)
        {
            AddServiceMode = false;
            HandleService(service);
        }

        public void HandleService(ModellingService service)
        {
            try
            {
                ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service, AvailableServices, AddServiceMode, DisplayMessageInUi);
                EditServiceMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
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
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteService, new { id = actService.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.Service, actService.Id, $"Deleted Service: {actService.Name}");
                    AvailableServices.Remove(actService);
                    DeleteServiceMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service"), "", true);
            }
        }

        public async Task<bool> Save()
        {
            try
            {
                if (ActServiceGroup.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_service_group"), userConfig.GetText("U0001"), true);
                }
                if(checkServiceGroup())
                {
                    foreach(var svc in SvcToDelete)
                    {
                        ActServiceGroup.Services.Remove(ActServiceGroup.Services.FirstOrDefault(x => x.Content.Id == svc.Id) ?? throw new Exception("Did not find app service."));
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
                    return true;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
            return false;
        }

        private bool checkServiceGroup()
        {
            if(ActServiceGroup.Name == null || ActServiceGroup.Name == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_service_group"), userConfig.GetText("E5102"), true);
                return false;
            }
            return true;
        }

        private async Task AddServiceGroupToDb()
        {
            try
            {
                var svcGrpParams = new
                {
                    name = ActServiceGroup.Name,
                    appId = Application.Id,
                    comment = ActServiceGroup.Comment,
                    isGlobal = false,
                    creator = userConfig.User.Name
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newServiceGroup, svcGrpParams)).ReturnIds;
                if (returnIds != null)
                {
                    ActServiceGroup.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ObjectType.ServiceGroup, ActServiceGroup.Id, $"New Service Group: {ActServiceGroup.Name}");
                    foreach(var service in ActServiceGroup.Services)
                    {
                        var svcParams = new
                        {
                            serviceId = service.Content.Id,
                            serviceGroupId = ActServiceGroup.Id
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToServiceGroup, svcParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.ServiceGroup, ActServiceGroup.Id, $"Added Service {service.Content.Name} to Service Group: {ActServiceGroup.Name}");
                    }
                    ServiceGroups.Add(ActServiceGroup);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_service_group"), "", true);
            }
        }

        private async Task UpdateServiceGroupInDb()
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
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ObjectType.ServiceGroup, ActServiceGroup.Id, $"Updated Service Group: {ActServiceGroup.Name}");
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromServiceGroup, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Disassign, ModellingTypes.ObjectType.ServiceGroup, ActServiceGroup.Id, $"Removed Service {service.Name} from Service Group: {ActServiceGroup.Name}");
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToServiceGroup, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.ServiceGroup, ActServiceGroup.Id, $"Added Service {service.Name} to Service Group: {ActServiceGroup.Name}");
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

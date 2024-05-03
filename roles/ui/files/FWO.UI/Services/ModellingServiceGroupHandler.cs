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
        public List<KeyValuePair<int, int>> AvailableSvcElems { get; set; } = new();
        public ModellingServiceHandler? ServiceHandler;
        public List<ModellingService> SvcToDelete { get; set; } = new();
        public bool AddServiceMode = false;
        public bool EditServiceMode = false;
        public bool DeleteServiceMode = false;
        public Func<Task> RefreshParent = DefaultInit.DoNothing;


        public ModellingServiceGroupHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingServiceGroup> serviceGroups, ModellingServiceGroup serviceGroup, List<ModellingService> availableServices,
            List<KeyValuePair<int, int>> availableSvcElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, 
            Func<Task> refreshParent, bool isOwner = true, bool readOnly = false)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi, readOnly, isOwner)
        {
            ServiceGroups = serviceGroups;
            ActServiceGroup = serviceGroup;
            ActServiceGroup.AppId = application.Id;
            AvailableServices = availableServices;
            AvailableSvcElems = availableSvcElems;
            RefreshParent = refreshParent;
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

        public async Task RefreshActServiceGroup()
        {
            try
            {
                if(ActServiceGroup.Id > 0)
                {
                    ActServiceGroup = await apiConnection.SendQueryAsync<ModellingServiceGroup>(ModellingQueries.getServiceGroupById, new { id = ActServiceGroup.Id });
                }
                await RefreshParent();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
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
                service.IsGlobal = ActServiceGroup.IsGlobal;
                ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service,
                    AvailableServices, AvailableSvcElems, AddServiceMode, DisplayMessageInUi, IsOwner);
                EditServiceMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }

        public async Task RequestDeleteService(ModellingService service)
        {
            await RequestDeleteServiceBase(service);
            DeleteServiceMode = true;
        }

        public async Task DeleteService()
        {
            try
            {
                DeleteServiceMode = await DeleteService(AvailableServices);
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
                if(CheckServiceGroup())
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
                        await RefreshParent();
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

        private bool CheckServiceGroup()
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
                int? applicationId = ActServiceGroup.IsGlobal ? null : Application.Id;
                var svcGrpParams = new
                {
                    name = ActServiceGroup.Name,
                    appId = applicationId,
                    comment = ActServiceGroup.Comment,
                    isGlobal = ActServiceGroup.IsGlobal,
                    creator = userConfig.User.Name
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newServiceGroup, svcGrpParams)).ReturnIds;
                if (returnIds != null)
                {
                    ActServiceGroup.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.ServiceGroup, ActServiceGroup.Id,
                        $"New Service Group: {ActServiceGroup.Display()}", Application.Id);
                    foreach(var service in ActServiceGroup.Services)
                    {
                        var svcParams = new
                        {
                            serviceId = service.Content.Id,
                            serviceGroupId = ActServiceGroup.Id
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToServiceGroup, svcParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.ServiceGroup, ActServiceGroup.Id,
                            $"Added Service {service.Content.Display()} to Service Group: {ActServiceGroup.Display()}", Application.Id);
                    }
                    ActServiceGroup.Creator = userConfig.User.Name;
                    ActServiceGroup.CreationDate = DateTime.Now;
                    ServiceGroups.Add(ActServiceGroup);
                    AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, ActServiceGroup.Id));
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
                    comment = ActServiceGroup.Comment,
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateServiceGroup, svcGrpParams);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.ServiceGroup, ActServiceGroup.Id,
                    $"Updated Service Group: {ActServiceGroup.Display()}", Application.Id);
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromServiceGroup, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.ServiceGroup, ActServiceGroup.Id,
                        $"Removed Service {service.Display()} from Service Group: {ActServiceGroup.Display()}", Application.Id);
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToServiceGroup, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.ServiceGroup, ActServiceGroup.Id,
                        $"Added Service {service.Display()} to Service Group: {ActServiceGroup.Display()}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
        }

        public void Close()
        {
            DeleteServiceMode = false;
            SvcToAdd = new List<ModellingService>();
            SvcToDelete = new List<ModellingService>();
        }
    }
}

using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ModellingServiceGroupHandler
    {
        public FwoOwner Application { get; set; } = new();
        public List<ServiceGroup> ServiceGroups { get; set; } = new();
        public ServiceGroup ActServiceGroup { get; set; } = new();
        public List<NetworkService> AvailableServices { get; set; } = new();
        public bool AddMode { get; set; } = false;

        public List<NetworkService> SvcToAdd { get; set; } = new();
        public List<NetworkService> SvcToDelete { get; set; } = new();

        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingServiceGroupHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ServiceGroup> serviceGroups, ServiceGroup serviceGroup, List<NetworkService> availableServices,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            ServiceGroups = serviceGroups;
            ActServiceGroup = serviceGroup;
            AvailableServices = availableServices;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }

        public void ServicesToSvcGroup(List<NetworkService> services)
        {
            foreach(var svc in services)
            {
                if(!AvailableServices.Contains(svc) && !SvcToAdd.Contains(svc))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
        
        public async Task Save()
        {
            foreach(var svc in SvcToDelete)
            {
                ActServiceGroup.NetworkServices.Remove(ActServiceGroup.NetworkServices.FirstOrDefault(x => x.Content.Id == svc.Id));
            }
            foreach(var svc in SvcToAdd)
            {
                ActServiceGroup.NetworkServices.Add(new ServiceWrapper(){ Content = svc });
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.newServiceGroup, svcGrpParams)).ReturnIds;
                if (returnIds != null)
                {
                    ActServiceGroup.Id = returnIds[0].NewId;
                    foreach(var service in ActServiceGroup.NetworkServices)
                    {
                        var svcParams = new
                        {
                            serviceId = service.Content.Id,
                            serviceGroupId = ActServiceGroup.Id
                        };
                        await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addServiceToServiceGroup, svcParams);
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
                await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.updateServiceGroup, svcGrpParams);
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeServiceFromServiceGroup, svcParams);
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new
                    {
                        serviceId = service.Id,
                        serviceGroupId = ActServiceGroup.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addServiceToServiceGroup, svcParams);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
        }

        public void Close()
        {
            SvcToAdd = new List<NetworkService>();
            SvcToDelete = new List<NetworkService>();
        }
    }
}

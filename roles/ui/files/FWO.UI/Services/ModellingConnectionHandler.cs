using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ModellingConnectionHandler
    {
        public FwoOwner Application { get; set; } = new FwoOwner();
        public NetworkConnection Conn { get; set; } = new ();
        // public List<AppRole> AppRoles { get; set; } = new();
        public List<ServiceGroup> ServiceGroups { get; set; } = new();
        public List<NetworkService> Services { get; set; } = new();
        // public List<NetworkConnection> Interfaces { get; set; } = new();

        public List<NetworkService> SvcToAdd { get; set; } = new List<NetworkService>();
        public List<NetworkService> SvcToDelete { get; set; } = new List<NetworkService>();
        public List<ServiceGroup> SvcGrpToAdd { get; set; } = new List<ServiceGroup>();
        public List<ServiceGroup> SvcGrpToDelete { get; set; } = new List<ServiceGroup>();

        // public bool DisplayInterfaces { get; set; } = false;
        // public bool ToSrcAllowed { get; set; } = true;
        // public bool ToDestAllowed { get; set; } = true;
        // public bool ToSvcAllowed { get; set; } = true;

        public bool AddSvcGrpMode = false;
        public bool EditSvcGrpMode = false;
        public bool DeleteServiceGrpMode = false;
        public ModellingServiceGroupHandler SvcGrpHandler;
        public string deleteMessage = "";

        private ServiceGroup actServiceGroup = new();

        private readonly ApiConnection apiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingConnectionHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            NetworkConnection conn, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            this.apiConnection = apiConnection;
            this.userConfig = userConfig;
            Conn = conn;
            Application = application;
            DisplayMessageInUi = displayMessageInUi;
        }

        public async Task Init()
        {
            Services = await apiConnection.SendQueryAsync<List<NetworkService>>(FWO.Api.Client.Queries.ModellingQueries.getServicesForApp, new { appId = Application.Id });
            ServiceGroups = await apiConnection.SendQueryAsync<List<ServiceGroup>>(FWO.Api.Client.Queries.ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id });
        }

        public async Task CreateServiceGroup()
        {
            AddSvcGrpMode = true;
            await HandleServiceGroup(new ServiceGroup(){});
        }

        public async Task EditServiceGroup(ServiceGroup serviceGroup)
        {
            AddSvcGrpMode = false;
            await HandleServiceGroup(serviceGroup);
        }

        public async Task HandleServiceGroup(ServiceGroup serviceGroup)
        {
            SvcGrpHandler = new ModellingServiceGroupHandler(apiConnection, userConfig, Application, ServiceGroups, serviceGroup, AddSvcGrpMode, DisplayMessageInUi);
            await SvcGrpHandler.Init();
            EditSvcGrpMode = true;
        }

        public void RequestDeleteServiceGrp(ServiceGroup serviceGroup)
        {
            actServiceGroup = serviceGroup;
            deleteMessage = userConfig.GetText("U9004") + serviceGroup.Name + "?";
            DeleteServiceGrpMode = true;
        }

        public async Task DeleteServiceGroup()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.deleteServiceGroup, new { id = actServiceGroup.Id })).AffectedRows > 0)
                {
                    ServiceGroups.Remove(actServiceGroup);
                    DeleteServiceGrpMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service_group"), "", true);
            }
        }

        public void ServiceGrpsToConn(List<ServiceGroup> serviceGrps)
        {
            foreach(var grp in serviceGrps)
            {
                if(!Conn.ServiceGroups.Contains(grp) && !SvcGrpToAdd.Contains(grp))
                {
                    SvcGrpToAdd.Add(grp);
                }
            }
        }

        public void ServicesToConn(List<NetworkService> services)
        {
            foreach(var svc in services)
            {
                if(!Conn.Services.Contains(svc) && !SvcToAdd.Contains(svc))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
        
        public void Close()
        {
            // SrcIpsToAdd = new List<NetworkObject>();
            // SrcIpsToDelete = new List<NetworkObject>();
            // DstIpsToAdd = new List<NetworkObject>();
            // DstIpsToDelete = new List<NetworkObject>();
            // SrcAppRolesToAdd = new List<AppRole>();
            // SrcAppRolesToDelete = new List<AppRole>();
            // DstAppRolesToAdd = new List<AppRole>();
            // DstAppRolesToDelete = new List<AppRole>();
            SvcToAdd = new List<NetworkService>();
            SvcToDelete = new List<NetworkService>();
            SvcGrpToAdd = new List<ServiceGroup>();
            SvcGrpToDelete = new List<ServiceGroup>();
            AddSvcGrpMode = false;
            EditSvcGrpMode = false;
            DeleteServiceGrpMode = false;
            // AddAppRoleMode = false;
            // EditAppRoleMode = false;
            // DeleteAppRoleMode = false;
            // AddServiceMode = false;
            // EditServiceMode = false;
            // DeleteServiceMode = false;
            // Display = false;
            // DisplayChanged.InvokeAsync(Display);
        }
    }
}

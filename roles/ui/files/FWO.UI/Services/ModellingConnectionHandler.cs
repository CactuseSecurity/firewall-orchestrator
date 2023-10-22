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
        // public List<NetworkService> SvcToDelete { get; set; } = new List<NetworkService>();
        public List<ServiceGroup> SvcGrpToAdd { get; set; } = new List<ServiceGroup>();
        // public List<ServiceGroup> SvcGrpToDelete { get; set; } = new List<ServiceGroup>();

        // public bool DisplayInterfaces { get; set; } = false;
        // public bool ToSrcAllowed { get; set; } = true;
        // public bool ToDestAllowed { get; set; } = true;
        // public bool ToSvcAllowed { get; set; } = true;

        private readonly ApiConnection apiConnection;

        // test
        private int ServiceIdCounter = 4;

        public ModellingConnectionHandler(ApiConnection apiConnection, FwoOwner application, NetworkConnection conn)
        {
            this.apiConnection = apiConnection;
            Conn = conn;
            Application = application;
        }

        public async Task Init()
        {
            Services = await apiConnection.SendQueryAsync<List<NetworkService>>(FWO.Api.Client.Queries.ModellingQueries.getServicesForApp, new { appId = Application.Id });
            ServiceGroups = await apiConnection.SendQueryAsync<List<ServiceGroup>>(FWO.Api.Client.Queries.ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id });
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
            // StateHasChanged();
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
            // StateHasChanged();
        }
    }
}

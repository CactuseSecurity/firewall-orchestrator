namespace FWO.Api.Data
{
    public class DnDContainer
    {
        public List<NetworkObject> IpElements { get; set; } = new();
        public List<AppRole> AppRoleElements { get; set; } = new();
        public List<ModellingService> SvcElements { get; set; } = new();
        public List<ServiceGroup> SvcGrpElements { get; set; } = new();
        public NetworkConnection ConnElement { get; set; } = null;

        public void Clear()
        {
            IpElements = new();
            AppRoleElements = new();
            SvcElements = new();
            SvcGrpElements = new();
            ConnElement = null;
        }
    }
}

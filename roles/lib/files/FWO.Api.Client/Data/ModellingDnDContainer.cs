namespace FWO.Api.Data
{
    public class ModellingDnDContainer
    {
        public List<NetworkObject> IpElements { get; set; } = new();
        public List<ModellingAppRole> AppRoleElements { get; set; } = new();
        public List<ModellingService> SvcElements { get; set; } = new();
        public List<ModellingServiceGroup> SvcGrpElements { get; set; } = new();
        public ModellingConnection ConnElement { get; set; } = null;

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

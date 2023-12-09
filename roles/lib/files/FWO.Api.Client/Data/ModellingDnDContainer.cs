namespace FWO.Api.Data
{
    public class ModellingDnDContainer
    {
        public List<ModellingAppServer> AppServerElements { get; set; } = new();
        public List<ModellingAppRole> AppRoleElements { get; set; } = new();
        public List<ModellingNwGroup> NwGroupElements { get; set; } = new();
        public List<ModellingService> SvcElements { get; set; } = new();
        public List<ModellingServiceGroup> SvcGrpElements { get; set; } = new();
        public ModellingConnection ConnElement { get; set; } = null;

        public void Clear()
        {
            AppServerElements = new();
            AppRoleElements = new();
            NwGroupElements = new();
            SvcElements = new();
            SvcGrpElements = new();
            ConnElement = null;
        }
    }
}

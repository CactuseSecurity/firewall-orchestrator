namespace FWO.Api.Data
{
    public class ModellingDnDContainer
    {
        public List<ModellingAppServer> AppServerElements { get; set; } = [];
        public List<ModellingAppRole> AppRoleElements { get; set; } = [];
        public List<ModellingNetworkArea> AreaElements { get; set; } = [];
        public List<ModellingNwGroup> NwGroupElements { get; set; } = [];
        public List<ModellingService> SvcElements { get; set; } = [];
        public List<ModellingServiceGroup> SvcGrpElements { get; set; } = [];
        public ModellingConnection? ConnElement { get; set; }

        public void Clear()
        {
            AppServerElements = [];
            AppRoleElements = [];
            AreaElements = [];
            NwGroupElements = [];
            SvcElements = [];
            SvcGrpElements = [];
            ConnElement = null;
        }
    }
}

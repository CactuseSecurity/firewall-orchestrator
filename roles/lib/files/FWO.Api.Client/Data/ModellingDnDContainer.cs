namespace FWO.Api.Data
{
    public class ModellingDnDContainer
    {
        public List<ModellingAppServer> AppServerElements { get; set; } = [];
        public List<ModellingAppRole> AppRoleElements { get; set; } = [];
        public List<ModellingNwGroup> NwGroupElements { get; set; } = [];
        public List<ModellingService> SvcElements { get; set; } = [];
        public List<ModellingServiceGroup> SvcGrpElements { get; set; } = [];
        public ModellingConnection? ConnElement { get; set; }

        public void Clear()
        {
            AppServerElements = [];
            AppRoleElements = [];
            NwGroupElements = [];
            SvcElements = [];
            SvcGrpElements = [];
            ConnElement = null;
        }
    }
}

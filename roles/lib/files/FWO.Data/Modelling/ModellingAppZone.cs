namespace FWO.Data.Modelling
{
    public class ModellingAppZone : ModellingAppRole
    {
        public bool Exists { get; set; } = false;
        public List<ModellingAppServerWrapper> AppServersNew = [];
        public List<ModellingAppServerWrapper> AppServersRemoved = [];
        public List<ModellingAppServerWrapper> AppServersUnchanged = [];

        public ModellingAppZone()
        { }

        public ModellingAppZone(int? appId)
        {
            AppId = appId;
        }

        public ModellingAppZone(ModellingAppZone appZone) : base(appZone)
        { }

        public ModellingAppZone(ModellingAppRole appRole) : base(appRole)
        { }

        public ModellingAppZone(NetworkObject nwObj, ModellingNamingConvention? namCon = null) : base(nwObj, namCon)
        { }
    }
}

namespace FWO.Data.Modelling
{
    public class ModellingAppZone : ModellingAppRole
    {
        public bool Exists { get; set; }
        public List<ModellingAppServerWrapper> AppServersNew = [];
        public List<ModellingAppServerWrapper> AppServersRemoved = [];

        public ModellingAppZone()
        { }

        public ModellingAppZone(int? appId)
        {
            AppId = appId;
        }

        public ModellingAppZone(ModellingAppZone appZone) : base(appZone)
        { }

        public ModellingAppZone(NetworkObject nwObj, ModellingNamingConvention? namCon = null) : base(nwObj, namCon)
        { }
    }
}

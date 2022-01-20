using FWO.Api.Data;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryBase
    {
        public Management superManagement = new Management();

        public AutoDiscoveryBase (Management mgm) 
        {
            superManagement = mgm;
        }

        public virtual Task<List<Management>> Run()
        {
            return superManagement.DeviceType.Name switch
            {
                "FortiManager" => new AutoDiscoveryFortiManager(superManagement).Run(),
                "CheckPoint" => new AutoDiscoveryCpMds(superManagement).Run(),
                _ => throw new NotSupportedException("SuperManager Type is not supported."),
            };
        }        
    }
}

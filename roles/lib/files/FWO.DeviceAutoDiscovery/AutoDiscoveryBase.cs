using FWO.Api.Data;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryBase
    {
        public Management superManager = new Management();

        public AutoDiscoveryBase (Management mgm) 
        {
            superManager = mgm;
        }

        public virtual Task<List<Management>> Run()
        {
            return superManager.DeviceType.Name switch
            {
                "FortiManager" => new AutoDiscoveryFortiManager(superManager).Run(),
                "CheckPoint" => new AutoDiscoveryCpMds(superManager).Run(),
                _ => throw new NotSupportedException("SuperManager Type is not supported."),
            };
        }        
    }
}

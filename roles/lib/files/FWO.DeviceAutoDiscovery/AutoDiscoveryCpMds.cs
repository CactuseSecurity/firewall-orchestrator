using FWO.Api.Data;
using FWO.Logging;
using FWO.Rest.Client;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryCpMds : AutoDiscoveryBase
    {
        public AutoDiscoveryCpMds(Management mgm) : base(mgm) { }

        public override async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = new List<Management>();
            Log.WriteAudit("Autodiscovery", $"starting discovery for {superManager.Name} (id={superManager.Id})");
            if (superManager.DeviceType.Name == "CheckPoint")
            {
                List<Adom> customAdoms = new List<Adom>();
            }
            return discoveredDevices;
        }
    }
}

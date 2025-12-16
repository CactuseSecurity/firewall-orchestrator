using FWO.Data.Modelling;

namespace FWO.Services
{
    public class AppZoneComparer(ModellingNamingConvention namingConvention) : IEqualityComparer<ModellingAppZone?>
    {
        readonly AppServerComparer appServerComparer = new(namingConvention);

        public bool Equals(ModellingAppZone? appZone1, ModellingAppZone? appZone2)
        {
            if (ReferenceEquals(appZone1, appZone2))
            {
                return true;
            }

            if (appZone1 is null || appZone2 is null || appZone1.Name != appZone2.Name || appZone1.AppServers.Count != appZone2.AppServers.Count)
            {
                return false;
            }

            return appZone1.AppServers.Except([.. appZone2.AppServers], appServerComparer).ToList().Count == 0 
                && appZone2.AppServers.Except([.. appZone1.AppServers], appServerComparer).ToList().Count == 0;
        }

        public int GetHashCode(ModellingAppZone appZone)
        {
            int hashCode = 0;
            foreach(var appSrv in appZone.AppServers)
            {
                hashCode ^= appSrv != null ? appServerComparer.GetHashCode(appSrv) : 0;
            }
            return hashCode ^ HashCode.Combine(appZone.Name);
        }
    }
}

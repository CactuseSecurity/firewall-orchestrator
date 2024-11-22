using FWO.Api.Client.Data;
using FWO.Api.Data;

namespace FWO.Services
{
    internal class AppZoneComparer : IEqualityComparer<ModellingAppZone>
    {
        public bool Equals(ModellingAppZone? x, ModellingAppZone? y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (x is null || y is null)
                return false;

            IEnumerable<ModellingAppServerWrapper>? diff = x.AppServers.Except(y.AppServers, new AppServerComparer());

            if (diff.Any())
                return false;

            return x.AppId == y.AppId &&
                x.Id == y.Id &&
                x.IdString == y.IdString;
        }

        public int GetHashCode(ModellingAppZone appZone)
        {
            if (appZone is null) return 0;
            return appZone == null ? 0 : appZone.GetHashCode();
        }
    }
}

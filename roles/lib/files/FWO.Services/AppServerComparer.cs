using FWO.Api.Data;
using System.Diagnostics.CodeAnalysis;

namespace FWO.Services
{
    class AppServerComparer : IEqualityComparer<ModellingAppServerWrapper>, IEqualityComparer<ModellingAppServer>
    {
        public bool Equals(ModellingAppServerWrapper x, ModellingAppServerWrapper y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (x is null || y is null)
                return false;

            return x.Content.Id == y.Content.Id &&
                x.Content.Ip == y.Content.Ip &&
                x.Content.IpEnd == y.Content.IpEnd;
        }
        public bool Equals(ModellingAppServer x, ModellingAppServer y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (x is null || y is null)
                return false;

            return x.Id == y.Id &&
                x.Ip == y.Ip &&
                x.IpEnd == y.IpEnd;
        }

        public int GetHashCode(ModellingAppServerWrapper appServerWrapper)
        {
            if (appServerWrapper is null) return 0;
            int hash = appServerWrapper == null ? 0 : appServerWrapper.GetHashCode();
            int hashContent = appServerWrapper.Content == null ? 0 : appServerWrapper.Content.GetHashCode();
            return hashContent ^ hashContent;
        }

        public int GetHashCode([DisallowNull] ModellingAppServer obj)
        {
            throw new NotImplementedException();
        }
    }
}

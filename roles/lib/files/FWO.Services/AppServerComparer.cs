using FWO.Api.Data;

namespace FWO.Services
{
    class AppServerComparer : IEqualityComparer<ModellingAppServerWrapper>
    {
        public bool Equals(ModellingAppServerWrapper x, ModellingAppServerWrapper y)
        {
            if (ReferenceEquals(x, y)) return true;

            if ( x is null  ||  y is null )
                return false;

            return x.Content.Id == y.Content.Id;
        }

        public int GetHashCode(ModellingAppServerWrapper appServerWrapper)
        {
            if ( appServerWrapper is null ) return 0;
            int hashProductName = appServerWrapper.Content == null ? 0 : appServerWrapper.Content.GetHashCode();
            int hashProductCode = appServerWrapper.Content.GetHashCode();
            return hashProductName ^ hashProductCode;
        }
    }
}

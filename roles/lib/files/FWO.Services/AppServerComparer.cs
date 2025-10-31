using FWO.Data.Modelling;

namespace FWO.Services
{
    public class AppServerComparer(ModellingNamingConvention namingConvention) : IEqualityComparer<ModellingAppServerWrapper>, IEqualityComparer<ModellingAppServer>
    {
        readonly ModellingNamingConvention NamingConvention = namingConvention;

        public bool Equals(ModellingAppServerWrapper? appServerWrapper1, ModellingAppServerWrapper? appServerWrapper2)
        {
            return appServerWrapper1 is not null && appServerWrapper2 is not null && Equals(appServerWrapper1.Content, appServerWrapper2.Content);
        }

        public bool Equals(ModellingAppServer? appServer1, ModellingAppServer? appServer2)
        {
            if (ReferenceEquals(appServer1, appServer2))
            {
                return true;
            }

            if (appServer1 is null || appServer2 is null)
            {
                return false;
            }

            string appServer1Name = AppServerHelper.ConstructSanitizedAppServerName(appServer1, NamingConvention);
            string appServer2Name = AppServerHelper.ConstructSanitizedAppServerName(appServer2, NamingConvention);
            return string.Equals(appServer1Name, appServer2Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ModellingAppServerWrapper appServerWrapper)
        {
            return appServerWrapper == null ? 0 : GetHashCode(appServerWrapper.Content);
        }

        public int GetHashCode(ModellingAppServer appServer)
        {
            string appServerName = AppServerHelper.ConstructSanitizedAppServerName(appServer, NamingConvention).ToLower().Trim();
            return HashCode.Combine(appServerName);
        }
    }
}

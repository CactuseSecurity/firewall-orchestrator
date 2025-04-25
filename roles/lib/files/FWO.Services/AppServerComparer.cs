using FWO.Data;
using FWO.Data.Modelling;
using System.Diagnostics.CodeAnalysis;

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

            string appServer2Name = AppServerHelper.ConstructAppServerName(appServer2, NamingConvention);
            return appServer1.Name.Trim() == appServer2Name.Trim(); // || appServer1.Name.Trim() == sanitizedAS2Name.Trim();
        }

        public int GetHashCode(ModellingAppServerWrapper appServerWrapper)
        {
            return appServerWrapper == null ? 0 : GetHashCode(appServerWrapper.Content);
        }

        public int GetHashCode(ModellingAppServer appServer)
        {
            string appServerName = AppServerHelper.ConstructAppServerName(appServer, NamingConvention).Trim();
            return HashCode.Combine(appServerName);
        }
    }
}

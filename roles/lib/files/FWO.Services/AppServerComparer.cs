using FWO.Api.Data;
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

            string appServer2Name = ConstructAppServerName(appServer2, NamingConvention);
            bool shortened = false;
            string sanitizedAS2Name = Sanitizer.SanitizeJsonFieldMand(new(appServer2Name), ref shortened);
            return appServer1.Name.Trim() == appServer2Name.Trim() || appServer1.Name.Trim() == sanitizedAS2Name.Trim();
        }

        public int GetHashCode(ModellingAppServerWrapper appServerWrapper)
        {
            if (appServerWrapper is null) return 0;
            int hash = appServerWrapper == null ? 0 : appServerWrapper.GetHashCode();
            int hashContent = appServerWrapper?.Content == null ? 0 : appServerWrapper.Content.GetHashCode();
            return hash ^ hashContent;
        }

        public int GetHashCode([DisallowNull] ModellingAppServer obj)
        {
            throw new NotImplementedException();
        }

        public static string ConstructAppServerName(ModellingAppServer appServer, ModellingNamingConvention namingConvention)
        {
            return string.IsNullOrEmpty(appServer.Name) ? namingConvention.AppServerPrefix + DisplayBase.DisplayIp(appServer.Ip, appServer.IpEnd) :
                ( char.IsLetter(appServer.Name[0]) ? appServer.Name : namingConvention.AppServerPrefix + appServer.Name );
        }
    }
}

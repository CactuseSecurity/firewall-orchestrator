using FWO.Api.Data;
using System.Diagnostics.CodeAnalysis;

namespace FWO.Services
{
    class AppServerComparer(ModellingNamingConvention namingConvention) : IEqualityComparer<ModellingAppServerWrapper>
    {
        readonly ModellingNamingConvention NamingConvention = namingConvention;

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

        public static string ConstructAppServerName(ModellingAppServer appServer, ModellingNamingConvention namingConvention)
        {
            return string.IsNullOrEmpty(appServer.Name) ? namingConvention.AppServerPrefix + DisplayBase.DisplayIp(appServer.Ip, appServer.IpEnd) :
                ( char.IsLetter(appServer.Name[0]) ? appServer.Name : namingConvention.AppServerPrefix + appServer.Name );
        }
    }
}

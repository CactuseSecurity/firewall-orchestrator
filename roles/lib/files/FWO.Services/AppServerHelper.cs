using FWO.Api.Data;
using FWO.Basics;
using System.Net;

namespace FWO.Services
{
    public class AppServerHelper
    {
        public static async Task<string> ConstructAppServerNameFromDns(ModellingAppServer appServer, ModellingNamingConvention namingConvention)
        {
            if (IPAddress.TryParse(appServer.Ip, out IPAddress? ip))
            {
                appServer.Name = await IpOperations.DnsReverseLookUp(ip);
            }
            if (string.IsNullOrEmpty(appServer.Name))
            {
                appServer.Name = ConstructAppServerName(appServer, namingConvention);
            }
            return appServer.Name;
        }

        public static string ConstructAppServerName(ModellingAppServer appServer, ModellingNamingConvention namingConvention)
        {
            return string.IsNullOrEmpty(appServer.Name) ? namingConvention.AppServerPrefix + DisplayBase.DisplayIp(appServer.Ip, appServer.IpEnd) :
                ( char.IsLetter(appServer.Name[0]) ? appServer.Name : namingConvention.AppServerPrefix + appServer.Name );
        }
    }
}

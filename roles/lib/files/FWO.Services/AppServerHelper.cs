using FWO.Api.Data;
using FWO.Basics;
using System.Net;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Services
{
    public static class AppServerHelper
    {
        public static async Task<string> ConstructAppServerNameFromDns(ModellingAppServer appServer, ModellingNamingConvention namingConvention, bool overwriteExistingNames=false)
        {
            if (IPAddress.TryParse(appServer.Ip, out IPAddress? ip))
            {
                string dnsName = await IpOperations.DnsReverseLookUp(ip);
                if(!string.IsNullOrEmpty(dnsName))
                {
                    appServer.Name = dnsName;
                    return dnsName;
                }
            }
            if (string.IsNullOrEmpty(appServer.Name) || overwriteExistingNames)
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

        public static async Task CheckAppServerNames(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            try
            {
                ModellingNamingConvention namingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(globalConfig.ModNamingConvention) ?? new();
                List<ModellingAppServer> AppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAllAppServers);
                int correctedCounter = 0;
                int failCounter = 0;
                foreach(var appServer in AppServers)
                {
                    string oldName = appServer.Name;
                    if((await ConstructAppServerNameFromDns(appServer, namingConvention, globalConfig.OverwriteExistingNames)) != oldName)
                    {
                        if (await UpdateName(apiConnection, appServer, oldName))
                        {
                            correctedCounter++;
                        }
                        else
                        {
                            failCounter++;
                        }
                    }
                }
                Log.WriteDebug($"Checked App Server Names", $"{correctedCounter} out of {AppServers.Count} App Servers have been corrected, {failCounter} failed");
            }
            catch(Exception exception)
            {
                Log.WriteError("Check App Server Names", $"Checking leads to exception:", exception);
            }
        }

        private static async Task<bool> UpdateName(ApiConnection apiConnection, ModellingAppServer appServer, string oldName)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    name = appServer.Name
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateAppServerName, Variables);
                Log.WriteDebug($"Correct App Server Name", $"Changed {oldName} to {appServer.Name}.");
                return true;
            }
            catch(Exception exception)
            {
                Log.WriteError("Correct AppServer Name", $"Leads to exception:", exception);
                return false;
            }
        }
    }
}

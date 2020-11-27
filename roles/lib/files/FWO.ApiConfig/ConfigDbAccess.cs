using FWO.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.ApiConfig.Data;

namespace FWO.ApiConfig
{
    public class ConfigDbAccess
    {
        public static string kDefaultLanguage = "DefaultLanguage";
        public static string kRulesPerFetch = "rulesPerFetch";

        Dictionary<String, String> configItems;
        static APIConnection apiConnection;
        int userid;

        public ConfigDbAccess(APIConnection apiConn, int id)
        {
            apiConnection = apiConn;
            userid = id;
            configItems = new Dictionary<String, String>();

            var Variables = new
            {
                user = userid,
            };
            ConfigItem [] confItems = (Task.Run(() => apiConnection.SendQueryAsync<ConfigItem[]>(FWO.ApiClient.Queries.ConfigQueries.getConfigItemsByUser, Variables))).Result;
            foreach (ConfigItem confItem in confItems)
            {
                configItems.Add(confItem.Key, confItem.Value);
            }
        }

        public string Get(string key)
        {
            string value;
            try
            {
                value = configItems[key];
            }
            catch
            {
                value = "";
            }
            return value;
        }

        public async Task Set(string key, string value)
        {
            var Variables = new
            {
                key = key,
                value = value,
                user = userid
            };
            try
            {
                var updpk = (await Task.Run(() => apiConnection.SendQueryAsync<object>(FWO.ApiClient.Queries.ConfigQueries.updateConfigItem, Variables)));
                if (updpk == null)
                {
                    // key not found: add new
                    await Task.Run(() => apiConnection.SendQueryAsync<object>(FWO.ApiClient.Queries.ConfigQueries.addConfigItem, Variables));
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Write Config", $"Could not write key:{key}, user:{userid}, value:{value}: to config: ", exception);
            }
        }
    }
}

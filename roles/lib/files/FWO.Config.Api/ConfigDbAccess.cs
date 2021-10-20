using FWO.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.Config.Api.Data;
using FWO.ApiClient.Queries;
using System.ComponentModel;

namespace FWO.Config.Api
{
    public class ConfigDbAccess
    {
        private readonly Dictionary<string, string> configItems = new Dictionary<string, string>();
        private readonly APIConnection apiConnection;
        private readonly UserConfig userConfig;
        private readonly int userId;

        public ConfigDbAccess(APIConnection apiConnection, UserConfig userConfig = null)
        {
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
            userId = userConfig == null ? 0 : userConfig.User.DbId;

            var Variables = new
            {
                user = userId,
            };
            // New task needed (why though?)
            ConfigItem[] confItems = Task.Run(async () => await apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, Variables)).Result;
            foreach (ConfigItem confItem in confItems)
            {
                configItems.Add(confItem.Key, confItem.Value);
            }
        }

        public ConfigValueType Get<ConfigValueType>(string key)
        {
            if (configItems.ContainsKey(key))
            {
                try
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(ConfigValueType));
                    return (ConfigValueType)converter.ConvertTo(configItems[key], typeof(ConfigValueType));
                }
                catch (Exception exception)
                {
                    throw new FormatException($"Error while fetching key \"{key}\".", exception);
                }
            }
            else
            {
                throw new KeyNotFoundException($"Element with key \"{key}\" could not be found.");
            }          
        }

        public async Task Set(string key, string value)
        {
            var Variables = new
            {
                key = key,
                value = value,
                user = userId
            };
            try
            {
                // TODO: Use one upsert query instead of seperate update and insert queries
                var updpk = await apiConnection.SendQueryAsync<object>(ConfigQueries.updateConfigItem, Variables);
                if (updpk == null)
                {
                    // key not found: add new
                    _ = await apiConnection.SendQueryAsync<object>(ConfigQueries.addConfigItem, Variables);
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Write Config", $"Could not write key:{key}, user:{userId}, value:{value}: to config: ", exception);
            }
        }
    }
}

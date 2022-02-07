using FWO.Logging;
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
        private readonly int userId;

        public ConfigDbAccess(APIConnection apiConnection, UserConfig? userConfig = null)
        {
            this.apiConnection = apiConnection;
            userId = userConfig == null ? 0 : userConfig.User.DbId;

            var Variables = new
            {
                user = userId,
            };

            ConfigItem[] confItems = apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, Variables).Result;
            // New task needed (why though?)
            // ConfigItem[] confItems = Task.Run(async () => await apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, Variables)).Result;
            foreach (ConfigItem confItem in confItems)
            {
                try
                {
                    string key = confItem.Key;
                    string value = confItem.Value ?? throw new Exception($"Error importing config item (key: {confItem.Key}) for user (id: {confItem.User}): Value is null");
                    configItems.Add(key, value);
                }
                catch (Exception ex)
                {
                    Log.WriteError("Reading Config", "Config item could not be read, skipping it.", ex);
                }
            }
        }

        public ConfigValueType Get<ConfigValueType>(string key)
        {
            if (configItems.ContainsKey(key))
            {
                try
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(ConfigValueType));
                    return (ConfigValueType?)converter.ConvertFromString(configItems[key]) 
                    ?? throw new Exception($"Config value (with key: {key}) is null or not convertible to {nameof(ConfigValueType)}.");
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
                // Update or insert config item
                var _ = await apiConnection.SendQueryAsync<object>(ConfigQueries.upsertConfigItem, Variables);
            }
            catch(Exception exception)
            {
                Log.WriteError("Write Config", $"Could not write key:{key}, user:{userId}, value:{value}: to config: ", exception);
                throw;
            }
        }
    }
}

using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace FWO.Config.Api
{
    public abstract class Config : ConfigData
    {
        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        protected ApiConnection apiConnection;

        public int UserId { get; private set; }
        public bool Initialized { get; private set; } = false;

        public event Action<Config, ConfigItem[]>? OnChange;

        protected SemaphoreSlim semaphoreSlim = new(1, 1);

        public ConfigItem[] RawConfigItems { get; set; } = [];

        protected Config() { }

        protected Config(ApiConnection apiConnection, int userId)
        {
            InitWithUserId(apiConnection, userId).Wait();
        }

        public async Task InitWithUserId(ApiConnection apiConnection, int userId)
        {
            this.apiConnection = apiConnection;
            UserId = userId;
            apiConnection.GetSubscription<ConfigItem[]>(SubscriptionExceptionHandler, SubscriptionUpdateHandler, ConfigQueries.getConfigSubscription, new { UserId });
            if (waitForFirstUpdate)
            {
                await Task.Run(async () => { while (!Initialized) { await Task.Delay(10); } });
            }
        }

        protected void SubscriptionUpdateHandler(ConfigItem[] configItems)
        {
            HandleUpdate(configItems, false);
        }

        public void SubscriptionPartialUpdateHandler(ConfigItem[] configItems)
        {
            HandleUpdate(configItems, true);
        }

        protected void HandleUpdate(ConfigItem[] configItems, bool partialUpdate)
        {
            semaphoreSlim.Wait();
            try
            {
                Log.WriteDebug("Config subscription update", "New config values received from config subscription");
                RawConfigItems = configItems;
                Update(configItems, partialUpdate);
                OnChange?.Invoke(this, configItems);
                Initialized = true;
            }
            finally { semaphoreSlim.Release(); }
        }

        protected void Update(ConfigItem[] configItems, bool partialUpdate = false)
        {
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                // Is the property storing a config value (marked by JsonPropertyName Attribute)?
                if (property.GetCustomAttribute<JsonPropertyNameAttribute>() != null)
                {
                    string key = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
                    ConfigItem? configItem = configItems.FirstOrDefault(configItem => configItem.Key == key);

                    if (configItem != null)
                    {
                        try
                        {
                            Type propertyType = property.PropertyType;
                            TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
                            property.SetValue(this, converter.ConvertFromString(configItem.Value
                            ?? throw new Exception($"Config value (with key: {configItem.Key}) is null."))
                            ?? throw new Exception($"Config value (with key: {configItem.Key}) is not convertible to {property.GetType()}."));
                        }
                        catch (Exception exception)
                        {
                            Log.WriteError("Load Config Items", $"Config item with key \"{key}\" could not be loaded. Using default value.", exception);
                        }
                    }
                    else if (!partialUpdate)
                    {
                        // If this is a global config 
                        if (UserId == 0) 
                        {
                            Log.WriteDebug("Load Global Config Items", $"Config item with key \"{key}\" could not be found. Using default value.");
                        }
						// If this is a user config item (user might not have changed the default setting)
						else if (property.GetCustomAttribute<UserConfigDataAttribute>() != null)
                        {
							Log.WriteDebug("Load Config Items", $"Config item with key \"{key}\" could not be found. User might not have customized the setting. Using default value.");
						}
                    }
                }
            }
        }

        public async Task WriteToDatabase(ConfigData editedData, ApiConnection apiConnection)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                foreach (PropertyInfo property in GetType().GetProperties())
                {
                    // Is Property storing config value?
                    if (property.GetCustomAttribute<JsonPropertyNameAttribute>() != null)
                    {
                        // Was config value changed?
                        if (!Equals(property.GetValue(this), property.GetValue(editedData)))
                        {
                            string key = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;

                            try
                            {
                                TypeConverter converter = TypeDescriptor.GetConverter(property.GetType());
                                string stringValue = converter.ConvertToString(property.GetValue(editedData)
                                                ?? throw new Exception($"Config value (with key: {key}) is null"))
                                                ?? throw new Exception($"Config value (with key: {key}) is not convertible to {property.GetType()}.");
                                // Update or insert config item
                                await apiConnection.SendQueryAsync<object>(ConfigQueries.upsertConfigItem, new ConfigItem { Key = key, Value = stringValue, User = UserId });
                            }
                            catch (Exception exception)
                            {
                                Log.WriteError("Load Config Items", $"Config item with key \"{key}\" and value: {property.GetValue(editedData)} could not be stored.", exception);
                            }
                        }
                    }
                }
            }
            finally { semaphoreSlim.Release(); }
        }

        public async Task<ConfigData> GetEditableConfig()
        {
            // await semaphoreSlim.WaitAsync();
            // try
            // { 
            return (ConfigData)CloneEditable();
            // }
            // finally { semaphoreSlim.Release(); }
        }

        protected static void SubscriptionExceptionHandler(Exception exception)
        {
            Log.WriteError("Config Subscription", "Config subscription lead to error.", exception);
        }

        protected void InvokeOnChange(Config config, ConfigItem[] configItems)
        {
            OnChange?.Invoke(config, configItems);
        }

        public abstract string GetText(string key);
    }
}

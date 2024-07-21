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

        protected Config(ApiConnection apiConnection, int userId, bool withSubscription = false)
        {
            InitWithUserId(apiConnection, userId, withSubscription).Wait();
        }

        public async Task InitWithUserId(ApiConnection apiConnection, int userId, bool withSubscription = false)
        {
            this.apiConnection = apiConnection;
            if(withSubscription) // used in Ui context
            {
                UserId = userId;
                List<string> ignoreKeys = []; // currently nothing ignored, may be used later
                apiConnection.GetSubscription<ConfigItem[]>(SubscriptionExceptionHandler, SubscriptionUpdateHandler,
                    ConfigQueries.subscribeConfigChangesByUser, new { UserId , ignoreKeys });
                await Task.Run(async () => { while (!Initialized) { await Task.Delay(10); } }); // waitForFirstUpdate
            }
            else // when only simple read is needed, e.g. during scheduled report in middleware server
            {
                ConfigItem[] configItems = await apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, new { User = UserId });
                if(configItems.Length > 0)
                {
                    Update(configItems);
                    RawConfigItems = configItems;
                }
                Initialized = true;
            }
        }

        public void SubscriptionUpdateHandler(ConfigItem[] configItems)
        {
            semaphoreSlim.Wait();
            try
            {
                Log.WriteDebug("Config subscription update", $"New {configItems.Length} config values received from config subscription");
                RawConfigItems = configItems;
                Update(configItems);
                OnChange?.Invoke(this, configItems);
                Initialized = true;
            }
            finally { semaphoreSlim.Release(); }
        }

        protected void Update(ConfigItem[] configItems)
        {
            //TODO: REMOVE
#if DEBUG
            Delegate[] invocations = OnChange?.GetInvocationList() ?? Array.Empty<Delegate>();
            List<string> methodeNames = new List<string>();
            List<object?> target = new List<object?>();

            foreach (Delegate invocation in invocations)
            {
                target.Add(invocation.Target);
                methodeNames.Add(invocation.Method.Name);
            }

            int a;
#endif

            List<string> remainingConfigItemNames = Array.ConvertAll(configItems, c => c.Key).ToList();
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
                            remainingConfigItemNames.Remove(configItem.Key);
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
                }
            }
            foreach(var name in remainingConfigItemNames.Where(n => !n.Contains("StateMatrix"))) // StateMatrix ConfigItems are handled separately
            {
                Log.WriteDebug($"Load {(UserId == 0 ? "Global " : "")}Config Items", $"Config item with key \"{name}\" could not be found. {(UserId == 0 ? "" : "User might not have customized the setting. ")}Using default value.");
            }
        }

        public async Task WriteToDatabase(ConfigData editedData, ApiConnection apiConnection)
        {
            await semaphoreSlim.WaitAsync();
            List<ConfigItem> configItemChanges = [];
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
                                // Add config item to the list of changed config items
                                configItemChanges.Add(new ConfigItem { Key = key, Value = stringValue, User = UserId });
                            }
                            catch (Exception exception)
                            {
                                Log.WriteError("Load Config Items", $"Config item with key \"{key}\" and value: {property.GetValue(editedData)} could not be stored.", exception);
                            }
                        }
                    }
                }
                // Update or insert all config item
                await apiConnection.SendQueryAsync<object>(ConfigQueries.upsertConfigItems, new { config_items = configItemChanges });
            }
            finally { semaphoreSlim.Release(); }
        }

        public async Task<ConfigData> GetEditableConfig()
        {
            await semaphoreSlim.WaitAsync();
            try
            { 
                return (ConfigData)CloneEditable();
            }
            finally { semaphoreSlim.Release(); }
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

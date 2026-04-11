using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;

namespace FWO.Config.Api
{
    public abstract class Config : ConfigData, IDisposable
    {
        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        protected ApiConnection? apiConnection;

        // Track if we own the ApiConnection and thus have to dispose it
        private bool owningApiConnection = false;

        public int UserId { get; private set; }
        public bool Initialized { get; private set; } = false;

        public event Action<Config, ConfigItem[]>? OnChange;

        protected readonly SemaphoreSlim semaphoreSlim = new(1, 1);

        // To detect redundant dispose calls
        private bool _isDisposed;
        protected bool IsDisposed => _isDisposed;

        // GraphQL Subscription handling
        private GraphQlApiSubscription<ConfigItem[]>? _configGraphQlSubscription;

        public ConfigItem[] RawConfigItems { get; set; } = [];

        protected Config() { }

        protected Config(ApiConnection apiConnection, int userId, bool withSubscription = false, bool owningApiConnection = false)
        {
            InitWithUserId(apiConnection, userId, withSubscription, owningApiConnection).GetAwaiter().GetResult();
        }

        public async Task InitWithUserId(ApiConnection apiConnection, int userId, bool withSubscription = false, bool owningApiConnection = false)
        {
            ThrowIfDisposed();
            this.apiConnection = apiConnection;
            this.owningApiConnection = owningApiConnection;

            UserId = userId;

            if (withSubscription) // used in Ui context
            {
                // Re-init (e.g. login) can happen; dispose previous subscription to avoid handler accumulation.
                _configGraphQlSubscription?.Dispose();
                _configGraphQlSubscription = null;

                List<string> ignoreKeys = []; // currently nothing ignored, may be used later
                _configGraphQlSubscription = apiConnection.GetSubscription<ConfigItem[]>(SubscriptionExceptionHandler, SubscriptionUpdateHandler,
                    ConfigQueries.subscribeConfigChangesByUser, new { UserId, ignoreKeys });

                while (!Initialized)
                {
                    await Task.Delay(10);
                }
            }
            else // when only simple read is needed, e.g. during scheduled report in middleware server
            {
                ConfigItem[] configItems = await apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, new { User = UserId });
                if (configItems.Length > 0)
                {
                    Update(configItems);
                    RawConfigItems = configItems;
                }
                Initialized = true;
            }
        }

        public void SubscriptionUpdateHandler(ConfigItem[] configItems)
        {
            if (_isDisposed) return;
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
            ThrowIfDisposed();
            List<string> remainingConfigItemNames = Array.ConvertAll(configItems, c => c.Key).ToList();
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                if (TryGetConfigKey(property, out string? key))
                {
                    ConfigItem? configItem = configItems.FirstOrDefault(item => item.Key == key);
                    ApplyConfigValue(property, key, configItem, remainingConfigItemNames);
                }
            }
            foreach (var name in remainingConfigItemNames.Where(n => !n.Contains("StateMatrix"))) // StateMatrix ConfigItems are handled separately
            {
                Log.WriteDebug($"Load {(UserId == 0 ? "Global " : "")}Config Items", $"Config item with key \"{name}\" could not be found. {(UserId == 0 ? "" : "User might not have customized the setting. ")}Using default value.");
            }
        }

        /// <summary>
        /// Tries to resolve the config key from a property marked with <see cref="JsonPropertyNameAttribute"/>.
        /// </summary>
        private static bool TryGetConfigKey(PropertyInfo property, out string? key)
        {
            key = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            return key != null;
        }

        /// <summary>
        /// Applies a config item value to the matching property and logs conversion issues.
        /// </summary>
        private void ApplyConfigValue(PropertyInfo property, string key, ConfigItem? configItem, List<string> remainingConfigItemNames)
        {
            if (configItem == null)
            {
                return;
            }

            try
            {
                remainingConfigItemNames.Remove(configItem.Key);
                property.SetValue(this, ConvertConfigValue(property, configItem));
            }
            catch (ArgumentException exception) when (property.PropertyType.IsEnum)
            {
                Log.WriteWarning("Load Config Items", $"Config item with key \"{key}\" contains unsupported value \"{configItem.Value}\". Using default value.");
                Log.WriteDebug("Load Config Items", $"Unsupported enum value ignored for key \"{key}\": {exception.Message}");
            }
            catch (Exception exception)
            {
                Log.WriteError("Load Config Items", $"Config item with key \"{key}\" could not be loaded. Using default value.", exception);
            }
        }

        private static object ConvertConfigValue(PropertyInfo property, ConfigItem configItem)
        {
            string rawValue = configItem.Value ?? throw new ArgumentNullException($"Config value (with key: {configItem.Key}) is null.");

            if (property.PropertyType.IsEnum)
            {
                object parsedEnum = Enum.Parse(property.PropertyType, rawValue, ignoreCase: true);
                if (!Enum.IsDefined(property.PropertyType, parsedEnum))
                {
                    throw new InvalidEnumArgumentException(property.Name, Convert.ToInt32(parsedEnum), property.PropertyType);
                }
                return parsedEnum;
            }

            TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
            return converter.ConvertFromString(rawValue)
                ?? throw new ArgumentException($"Config value (with key: {configItem.Key}) is not convertible to {property.PropertyType}.");
        }

        public async Task WriteToDatabase(ConfigData editedData, ApiConnection apiConnection)
        {
            ThrowIfDisposed();
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
                                TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
                                string stringValue = converter.ConvertToString(property.GetValue(editedData)
                                                ?? throw new ArgumentNullException($"Config value (with key: {key}) is null"))
                                                ?? throw new ArgumentException($"Config value (with key: {key}) is not convertible to {property.PropertyType}.");
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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            OnChange?.Invoke(config, configItems);
        }

        public abstract string GetText(string key);

        protected void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _configGraphQlSubscription?.Dispose();
                    _configGraphQlSubscription = null;
                    if (owningApiConnection)
                    {
                        apiConnection?.Dispose();
                    }
                    apiConnection = null;
                    semaphoreSlim.Dispose();
                    OnChange = null;
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

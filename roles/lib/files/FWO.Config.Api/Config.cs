using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
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

        private static readonly HashSet<string> ignoredLegacyConfigKeys =
        [
            "manageOwnerLdapGroups",
            "ownerLdapId",
            "ownerLdapGroupNames"
        ];

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
                _configGraphQlSubscription = apiConnection.GetSubscription<ConfigItem[]>(GraphqlExceptionHandler.Handle, SubscriptionUpdateHandler,
                    ConfigQueries.subscribeConfigChangesByUser, new { UserId, ignoreKeys });

                while (!Initialized)
                {
                    await Task.Delay(10);
                }
            }
            else // when only simple read is needed, e.g. during scheduled report in middleware server
            {
                ConfigItem[] configItems = await apiConnection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, new { user = UserId });
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
                ApplySubscriptionUpdate(configItems);
            }
            finally { semaphoreSlim.Release(); }
        }

        /// <summary>
        /// Merges a partial subscription update into the raw snapshot before applying it.
        /// </summary>
        public void MergeSubscriptionUpdateHandler(ConfigItem[] configItems)
        {
            if (_isDisposed) return;
            semaphoreSlim.Wait();
            try
            {
                Log.WriteDebug("Config subscription update", $"New {configItems.Length} config values received from config subscription");
                MergeRawConfigItems(configItems);
                ApplySubscriptionUpdate(configItems);
            }
            finally { semaphoreSlim.Release(); }
        }

        private void ApplySubscriptionUpdate(ConfigItem[] configItems)
        {
            Update(configItems);
            OnChange?.Invoke(this, configItems);
            Initialized = true;
        }

        protected void Update(ConfigItem[] configItems)
        {
            ThrowIfDisposed();
            List<string> remainingConfigItemNames = Array.ConvertAll(configItems, c => c.Key).ToList();
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                if (TryGetConfigKey(property, out string? key))
                {
                    string resolvedKey = key!;
                    ConfigItem? configItem = configItems.FirstOrDefault(item => item.Key == resolvedKey);
                    ApplyConfigValue(property, resolvedKey, configItem, remainingConfigItemNames);
                }
            }
            foreach (var name in remainingConfigItemNames.Where(n => !n.Contains("StateMatrix") && !ignoredLegacyConfigKeys.Contains(n))) // StateMatrix and retired settings are handled separately
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

        /// <summary>
        /// Writes the settings changed in an editable copy to the database and applies them to this config.
        /// A user config (<see cref="UserId"/> other than 0) writes only personal settings, i.e. the properties marked
        /// with <see cref="UserConfigDataAttribute"/>, as all other settings are only stored globally.
        /// </summary>
        /// <param name="editedData">Editable copy (see <see cref="GetEditableConfig"/>) holding the changed values.</param>
        /// <param name="apiConnection">API connection used for the write.</param>
        /// <param name="loadedData">Optional copy of the values <paramref name="editedData"/> was loaded with. If given,
        /// only values that differ from it are written, so a value that changed elsewhere while the copy was edited (e.g. a
        /// global value applied to a user config) is not written back. Otherwise the values are compared with this config.</param>
        public async Task WriteToDatabase(ConfigData editedData, ApiConnection apiConnection, ConfigData? loadedData = null)
        {
            ThrowIfDisposed();
            ConfigItem[] changedItems = [];
            await semaphoreSlim.WaitAsync();
            try
            {
                List<ConfigItem> configItemChanges = CollectChangedItems(editedData, loadedData ?? this);
                // Update or insert all config item
                if (configItemChanges.Count > 0)
                {
                    await apiConnection.SendQueryAsync<object>(ConfigQueries.upsertConfigItems, new { config_items = configItemChanges });
                    changedItems = [.. configItemChanges];
                    ApplyCommittedChanges(changedItems);
                }
            }
            finally { semaphoreSlim.Release(); }

            if (changedItems.Length > 0)
            {
                InvokeOnChange(this, changedItems);
            }
        }

        /// <summary>
        /// Collects the config items of the edited copy whose value differs from the comparison data.
        /// A user config only collects personal settings.
        /// </summary>
        /// <param name="editedData">Editable copy holding the changed values.</param>
        /// <param name="comparisonData">Values the edited copy is compared with.</param>
        /// <returns>The changed config items, stored for <see cref="UserId"/>.</returns>
        private List<ConfigItem> CollectChangedItems(ConfigData editedData, ConfigData comparisonData)
        {
            List<ConfigItem> configItemChanges = [];
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                if (!TryGetConfigKey(property, out string? key) || !IsStoredForUserId(property)
                    || Equals(property.GetValue(comparisonData), property.GetValue(editedData)))
                {
                    continue;
                }

                try
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
                    string stringValue = converter.ConvertToString(property.GetValue(editedData)
                                    ?? throw new ArgumentNullException($"Config value (with key: {key}) is null"))
                                    ?? throw new ArgumentException($"Config value (with key: {key}) is not convertible to {property.PropertyType}.");
                    configItemChanges.Add(new ConfigItem { Key = key!, Value = stringValue, User = UserId });
                }
                catch (Exception exception)
                {
                    Log.WriteError("Load Config Items", $"Config item with key \"{key}\" and value: {property.GetValue(editedData)} could not be stored.", exception);
                }
            }
            return configItemChanges;
        }

        /// <summary>
        /// The global config (user 0) stores every setting; a user config stores only personal settings.
        /// </summary>
        /// <param name="property">Config property to check.</param>
        /// <returns>True if the setting may be stored for <see cref="UserId"/>.</returns>
        private bool IsStoredForUserId(PropertyInfo property)
        {
            return UserId == 0 || property.GetCustomAttribute<UserConfigDataAttribute>() != null;
        }

        /// <summary>
        /// Applies successfully persisted configuration values to this in-memory config instance while the caller holds the semaphore.
        /// </summary>
        private void ApplyCommittedChanges(ConfigItem[] changedItems)
        {
            MergeRawConfigItems(changedItems);
            Update(changedItems);
        }

        /// <summary>
        /// Merges changed config items into the raw item cache while preserving existing entries.
        /// </summary>
        private void MergeRawConfigItems(ConfigItem[] changedItems)
        {
            List<ConfigItem> mergedItems = [.. RawConfigItems];
            foreach (ConfigItem changedItem in changedItems)
            {
                int index = mergedItems.FindIndex(item => item.Key == changedItem.Key);
                if (index >= 0)
                {
                    mergedItems[index] = changedItem;
                }
                else
                {
                    mergedItems.Add(changedItem);
                }
            }
            RawConfigItems = [.. mergedItems];
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

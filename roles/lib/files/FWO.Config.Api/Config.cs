using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
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
        /// Writes the values of the edited config that differ from the database to the database and applies them to this config.
        /// </summary>
        /// <remarks>
        /// The values are compared with the database rather than with this in-memory config, which can be outdated when its
        /// subscription missed updates: an edit back to the outdated value would otherwise never be written (#5601).
        /// </remarks>
        /// <param name="editedData">Config holding the edited values, usually from <see cref="GetEditableConfig"/>.</param>
        /// <param name="apiConnection">Connection of the user saving the values.</param>
        /// <exception cref="InvalidOperationException">The database did not take over every changed value.</exception>
        public async Task WriteToDatabase(ConfigData editedData, ApiConnection apiConnection)
        {
            ThrowIfDisposed();
            ConfigItem[] syncedItems = [];
            ConfigItem[] committedItems = [];
            await semaphoreSlim.WaitAsync();
            try
            {
                syncedItems = await SyncWithDatabase(apiConnection);
                List<ConfigItem> configItemChanges = CollectChangedItems(editedData);
                if (configItemChanges.Count > 0)
                {
                    ReturnId upsertResult = await apiConnection.SendQueryAsync<ReturnId>(ConfigQueries.upsertConfigItems, new { config_items = configItemChanges });
                    EnsureAllItemsWritten(upsertResult, configItemChanges);
                    committedItems = [.. configItemChanges];
                    ApplyCommittedChanges(committedItems);
                }
            }
            finally
            {
                semaphoreSlim.Release();
                ConfigItem[] appliedItems = MergeByKey(syncedItems, committedItems);
                if (appliedItems.Length > 0)
                {
                    InvokeOnChange(this, appliedItems);
                }
            }
        }

        /// <summary>
        /// Collects the config values of the edited config that differ from this config.
        /// </summary>
        private List<ConfigItem> CollectChangedItems(ConfigData editedData)
        {
            List<ConfigItem> configItemChanges = [];
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                // Is Property storing config value? Was config value changed?
                if (TryGetConfigKey(property, out string? key) && !Equals(property.GetValue(this), property.GetValue(editedData)))
                {
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
            }
            return configItemChanges;
        }

        /// <summary>
        /// Throws when the upsert left config items unwritten, e.g. when the role used may not update them. Hasura then
        /// skips the rows without an error.
        /// </summary>
        private static void EnsureAllItemsWritten(ReturnId? upsertResult, List<ConfigItem> configItemChanges)
        {
            int writtenCount = upsertResult?.AffectedRows ?? 0;
            if (writtenCount != configItemChanges.Count)
            {
                string keys = string.Join(", ", configItemChanges.Select(item => item.Key));
                throw new InvalidOperationException($"Only {writtenCount} of {configItemChanges.Count} config items were written to the database ({keys}).");
            }
        }

        /// <summary>
        /// Reads the config items of this config's user from the database and applies those that differ from this config,
        /// so that a config which missed subscription updates matches the database again. The caller holds the semaphore.
        /// </summary>
        /// <param name="connection">Connection used to read the config items.</param>
        /// <returns>The applied config items.</returns>
        private async Task<ConfigItem[]> SyncWithDatabase(ApiConnection connection)
        {
            ConfigItem[] databaseItems = await connection.SendQueryAsync<ConfigItem[]>(ConfigQueries.getConfigItemsByUser, new { user = UserId }) ?? [];
            Dictionary<string, string?> knownValues = [];
            foreach (ConfigItem knownItem in RawConfigItems)
            {
                knownValues[knownItem.Key] = knownItem.Value;
            }
            ConfigItem[] differingItems = [.. databaseItems.Where(item => !knownValues.TryGetValue(item.Key, out string? knownValue) || knownValue != item.Value)];
            if (differingItems.Length > 0)
            {
                Log.WriteWarning("Config sync", $"{(UserId == 0 ? "Global config" : $"Config of user {UserId}")} differed from the database " +
                    $"and was updated: {string.Join(", ", differingItems.Select(item => item.Key))}");
                ApplyCommittedChanges(differingItems);
            }
            return differingItems;
        }

        /// <summary>
        /// Combines config items, later items replacing earlier ones with the same key.
        /// </summary>
        private static ConfigItem[] MergeByKey(ConfigItem[] items, ConfigItem[] replacingItems)
        {
            Dictionary<string, ConfigItem> mergedItems = [];
            foreach (ConfigItem item in items.Concat(replacingItems))
            {
                mergedItems[item.Key] = item;
            }
            return [.. mergedItems.Values];
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

        /// <summary>
        /// Returns an editable copy of this config, after its values were read again from the database, so that an
        /// editor shows the stored values even when the subscription of this config missed updates.
        /// If the database cannot be read, the copy holds the values known so far.
        /// </summary>
        public async Task<ConfigData> GetEditableConfig()
        {
            ThrowIfDisposed();
            ConfigItem[] syncedItems = [];
            await semaphoreSlim.WaitAsync();
            try
            {
                syncedItems = await TrySyncWithDatabase();
                return (ConfigData)CloneEditable();
            }
            finally
            {
                semaphoreSlim.Release();
                if (syncedItems.Length > 0)
                {
                    InvokeOnChange(this, syncedItems);
                }
            }
        }

        private async Task<ConfigItem[]> TrySyncWithDatabase()
        {
            if (apiConnection == null)
            {
                return [];
            }
            try
            {
                return await SyncWithDatabase(apiConnection);
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Config sync", $"Config could not be read from the database, the values known so far are shown: {exception.Message}");
                return [];
            }
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

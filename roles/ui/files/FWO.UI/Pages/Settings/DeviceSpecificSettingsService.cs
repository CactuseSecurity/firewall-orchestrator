using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// Edit state of one field on the selected level. Tracks the user's intent (override here or
    /// inherit) separately from the value, so that untouched inherited values are never written back.
    /// </summary>
    public sealed class DeviceSpecificFieldState
    {
        public required DeviceSpecificFieldDefinition Field { get; init; }

        /// <summary>Value the level gets when the field is not overridden here.</summary>
        public required string InheritedValue { get; init; }

        /// <summary>Scope the inherited value comes from; null means the compiled default.</summary>
        public DeviceSpecificSettingsScope? InheritedSource { get; init; }

        /// <summary>Whether the field is stored on this level in the database.</summary>
        public bool WasLocal { get; init; }

        /// <summary>Whether this field applies to the node's current device path.</summary>
        public bool IsApplicable { get; init; } = true;

        /// <summary>A retained local override that does not apply to the node's current device path.</summary>
        public bool IsDormant => WasLocal && !IsApplicable;

        /// <summary>The stored override value; only meaningful when <see cref="WasLocal"/> is set.</summary>
        public string OriginalValue { get; init; } = "";

        public bool IsLocal { get; private set; }

        public string LocalValue { get; set; } = "";

        public string DisplayedValue => IsLocal ? LocalValue : InheritedValue;

        public bool IsChanged => IsLocal != WasLocal || (IsLocal && LocalValue != OriginalValue);

        /// <summary>Starts overriding the field here, seeded with the value currently in effect.</summary>
        public void Override()
        {
            if (!IsLocal)
            {
                LocalValue = WasLocal ? OriginalValue : InheritedValue;
                IsLocal = true;
            }
        }

        /// <summary>Stops overriding the field here, so it inherits again after saving.</summary>
        public void Inherit() => IsLocal = false;

        /// <summary>Drops all unsaved edits of this field.</summary>
        public void Reset()
        {
            IsLocal = WasLocal;
            LocalValue = OriginalValue;
        }

        public static DeviceSpecificFieldState Create(DeviceSpecificFieldDefinition field, bool wasLocal, string localValue,
            string inheritedValue, DeviceSpecificSettingsScope? inheritedSource, bool isApplicable = true)
        {
            DeviceSpecificFieldState state = new()
            {
                Field = field,
                WasLocal = wasLocal,
                IsApplicable = isApplicable,
                OriginalValue = wasLocal ? localValue : "",
                InheritedValue = inheritedValue,
                InheritedSource = inheritedSource
            };
            state.Reset();
            return state;
        }
    }

    /// <summary>
    /// Edit state of all fields offered on one level of the device specific settings hierarchy.
    /// </summary>
    public sealed class DeviceSpecificLevelForm(DeviceSpecificNode node, List<DeviceSpecificFieldState> fields)
    {
        public DeviceSpecificNode Node { get; } = node;

        public List<DeviceSpecificFieldState> Fields { get; } = fields;

        public bool HasChanges => Fields.Any(f => f.IsChanged);

        /// <summary>Discards all unsaved edits.</summary>
        public void Reset() => Fields.ForEach(f => f.Reset());

        /// <summary>
        /// Translates the final UI state into an explicit patch: new or changed local values become upserts,
        /// fields switched back to inherited become removals, everything untouched is left out.
        /// </summary>
        public DeviceSpecificSettingsChangeSet BuildChangeSet(DeviceSpecificSettingsScope scope)
        {
            DeviceSpecificSettingsChangeSet changes = new(scope);
            foreach (DeviceSpecificFieldState field in Fields.Where(f => f.IsChanged))
            {
                if (field.IsLocal)
                {
                    field.Field.SetOverride(changes, field.LocalValue);
                }
                else
                {
                    changes.Remove(field.Field.Key);
                }
            }
            return changes;
        }
    }

    /// <summary>
    /// Connects the device specific settings UI to <see cref="DeviceSpecificSettingsManager"/>: loads the persisted
    /// hierarchy, resolves the values of a level for editing and saves one form as one patch.
    /// </summary>
    public sealed class DeviceSpecificSettingsService(DeviceSpecificSettingsManager manager)
    {
        /// <summary>Resolved values of one level, formatted for the editors.</summary>
        private sealed record ResolvedLevel(
            Dictionary<DeviceSpecificSettingKey, string> Values,
            Dictionary<DeviceSpecificSettingKey, DeviceSpecificSettingsScope?> Sources,
            IReadOnlySet<DeviceSpecificSettingKey> DirectOverrides);

        /// <summary>Loads the tree of the given managements, linked to the device specific settings nodes persisted so far.</summary>
        public async Task<DeviceSpecificNode> LoadHierarchyAsync(IEnumerable<Management> managements)
        {
            return DeviceSpecificSettingsData.BuildHierarchy(managements, await manager.GetAllNodesAsync());
        }

        /// <summary>Loads the effective values, their sources and the local overrides of a level.</summary>
        public async Task<DeviceSpecificLevelForm> LoadFormAsync(DeviceSpecificNode node)
        {
            ResolvedLevel level = await ResolveAsync(node);
            ResolvedLevel? parentLevel = null;
            if (level.DirectOverrides.Count > 0)
            {
                parentLevel = node.Parent == null ? ResolveDefaults(node.Level) : await ResolveAsync(node.Parent);
            }

            List<DeviceSpecificFieldState> fields = [];
            IEnumerable<DeviceSpecificFieldDefinition> visibleFields = DeviceSpecificSettingsData.Fields.Where(field =>
                field.AppliesTo(node) || level.DirectOverrides.Contains(field.Key));
            foreach (DeviceSpecificFieldDefinition field in visibleFields)
            {
                bool wasLocal = level.DirectOverrides.Contains(field.Key);
                ResolvedLevel inherited = wasLocal ? parentLevel! : level;
                (string inheritedValue, DeviceSpecificSettingsScope? inheritedSource) = ValueOf(inherited, field, node.Level);
                fields.Add(DeviceSpecificFieldState.Create(field, wasLocal, WithShippedDefault(field, node, level.Values[field.Key]),
                    WithShippedDefault(field, node, inheritedValue), inheritedSource, field.AppliesTo(node)));
            }
            return new DeviceSpecificLevelForm(node, fields);
        }

        /// <summary>An empty value of a field with device type defaults stands for the default shipped for the node's device type.</summary>
        private static string WithShippedDefault(DeviceSpecificFieldDefinition field, DeviceSpecificNode node, string value) =>
            value.Length == 0 ? field.ShippedDefaultFor(node) ?? value : value;

        /// <summary>
        /// Saves the form as one patch. Before the level is written, every level above it is persisted and every
        /// persisted level on the way down is placed below its current parent in the tree - a management whose device
        /// type changed or a gateway assigned to another management is moved there. Updates the tree node's scope afterwards.
        /// </summary>
        public async Task SaveAsync(DeviceSpecificLevelForm form)
        {
            if (!form.HasChanges)
            {
                return;
            }

            DeviceSpecificNode node = form.Node;
            DeviceSpecificSettingsScope requestScope = RequestScope(node);
            DeviceSpecificSettingsChangeSet changes = form.BuildChangeSet(requestScope);
            if (node.IsPersisted || changes.Upserts.Count > 0)
            {
                foreach (DeviceSpecificNode level in node.SelfAndAncestors().Reverse())
                {
                    await PlaceBelowTreeParentAsync(level, level != node);
                }
            }

            UpdateRequestScope(requestScope, node);
            DeviceSpecificSettingsScope persisted = await manager.ApplyChangesAsync(changes);
            if (persisted.NodeId > 0)
            {
                node.Scope = persisted;
            }
        }

        /// <summary>True if the node is persisted below another node than its parent in the tree.</summary>
        public static bool IsMisplaced(DeviceSpecificNode node) =>
            node.IsPersisted && node.Parent != null && node.Scope.ParentNodeId != node.Parent.Scope.NodeId;

        /// <summary>
        /// Persists the node if requested and it is not stored yet, and moves a stored node below its tree parent
        /// if it is stored below another node. Expects the tree parent to be persisted already.
        /// </summary>
        private async Task PlaceBelowTreeParentAsync(DeviceSpecificNode node, bool persistIfMissing)
        {
            if (!node.IsPersisted)
            {
                if (persistIfMissing)
                {
                    node.Scope = await manager.EnsureNodeAsync(RequestScope(node));
                }
            }
            else if (IsMisplaced(node))
            {
                node.Scope = await manager.MoveNodeAsync(RequestScope(node), node.Parent!.Scope.NodeId);
            }
        }

        /// <summary>
        /// Resolves the values in effect at a node along its path in the tree, i.e. along the current device hierarchy
        /// and not along the stored parent links, which are outdated for a management or gateway moved since it was stored.
        /// </summary>
        private async Task<ResolvedLevel> ResolveAsync(DeviceSpecificNode node)
        {
            (GlobalDeviceSpecificSettings settings, IReadOnlySet<DeviceSpecificSettingKey> direct,
                IReadOnlyDictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource> sources) =
                await LoadLevelAsync(node.Path);

            Dictionary<DeviceSpecificSettingKey, string> values = [];
            Dictionary<DeviceSpecificSettingKey, DeviceSpecificSettingsScope?> valueSources = [];
            foreach (DeviceSpecificFieldDefinition field in DeviceSpecificSettingsData.Fields.Where(f => f.Key.IsAllowedAt(node.Level)))
            {
                values[field.Key] = field.Read(settings);
                valueSources[field.Key] = sources.TryGetValue(field.Key, out DeviceSpecificSettingValueSource? source) ? source.Scope : null;
            }

            return new ResolvedLevel(values, valueSources, direct);
        }

        private static ResolvedLevel ResolveDefaults(DeviceSpecificScopeType level)
        {
            GlobalDeviceSpecificSettings defaults = DeviceSpecificSettingsData.CreateDefaults(level);
            List<DeviceSpecificFieldDefinition> fields = [.. DeviceSpecificSettingsData.Fields.Where(f => f.Key.IsAllowedAt(level))];
            return new ResolvedLevel(
                fields.ToDictionary(f => f.Key, f => f.Read(defaults)),
                fields.ToDictionary(f => f.Key, _ => (DeviceSpecificSettingsScope?)null),
                new HashSet<DeviceSpecificSettingKey>());
        }

        private static (string Value, DeviceSpecificSettingsScope? Source) ValueOf(ResolvedLevel level, DeviceSpecificFieldDefinition field,
            DeviceSpecificScopeType fieldLevel)
        {
            return level.Values.TryGetValue(field.Key, out string? value)
                ? (value, level.Sources[field.Key])
                : (field.Read(DeviceSpecificSettingsData.CreateDefaults(fieldLevel)), null);
        }

        private async Task<(GlobalDeviceSpecificSettings, IReadOnlySet<DeviceSpecificSettingKey>,
            IReadOnlyDictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource>)> LoadLevelAsync(DeviceSpecificScopePath path)
        {
            return path.ScopeType switch
            {
                DeviceSpecificScopeType.Global => Unpack(await manager.LoadLevelAsync<GlobalDeviceSpecificSettings>(path)),
                DeviceSpecificScopeType.DeviceType => Unpack(await manager.LoadLevelAsync<DeviceTypeDeviceSpecificSettings>(path)),
                DeviceSpecificScopeType.Management => Unpack(await manager.LoadLevelAsync<ManagementDeviceSpecificSettings>(path)),
                DeviceSpecificScopeType.Gateway => Unpack(await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(path)),
                _ => throw new ArgumentOutOfRangeException(nameof(path), path.ScopeType, "A defined device specific settings scope type is required.")
            };
        }

        private static (GlobalDeviceSpecificSettings, IReadOnlySet<DeviceSpecificSettingKey>,
            IReadOnlyDictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource>) Unpack<TSettings>(DeviceSpecificSettingsLevel<TSettings> level)
            where TSettings : GlobalDeviceSpecificSettings
        {
            return (level.Settings, level.DirectOverrides, level.ValueSources);
        }

        /// <summary>
        /// The scope to hand to the manager for a tree node: the persisted scope with the node's current name,
        /// or a new scope referencing the persisted parent.
        /// </summary>
        private static DeviceSpecificSettingsScope RequestScope(DeviceSpecificNode node)
        {
            return new DeviceSpecificSettingsScope
            {
                ScopeType = node.Level,
                ObjectKey = node.Scope.ObjectKey,
                DisplayName = node.Name,
                NodeId = node.Scope.NodeId,
                ParentNodeId = node.IsPersisted ? node.Scope.ParentNodeId : NullIfUnpersisted(node.Parent),
                SortOrder = node.Scope.SortOrder
            };
        }

        /// <summary>
        /// Refreshes only the hierarchy locator after ancestors have been created or moved. The setting patch itself
        /// was captured before those asynchronous operations and is deliberately left unchanged.
        /// </summary>
        private static void UpdateRequestScope(DeviceSpecificSettingsScope requestScope, DeviceSpecificNode node)
        {
            DeviceSpecificSettingsScope currentScope = RequestScope(node);
            requestScope.NodeId = currentScope.NodeId;
            requestScope.ParentNodeId = currentScope.ParentNodeId;
            requestScope.DisplayName = currentScope.DisplayName;
            requestScope.SortOrder = currentScope.SortOrder;
        }

        private static long? NullIfUnpersisted(DeviceSpecificNode? node) => node is { IsPersisted: true } ? node.Scope.NodeId : null;
    }
}

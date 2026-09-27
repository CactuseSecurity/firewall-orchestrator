using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Provisioning;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// Edit state of one field on the selected level. Tracks the user's intent (override here or
    /// inherit) separately from the value, so that untouched inherited values are never written back.
    /// </summary>
    public sealed class ProvisioningFieldState
    {
        public required ProvisioningFieldDefinition Field { get; init; }

        /// <summary>Value the level gets when the field is not overridden here.</summary>
        public required string InheritedValue { get; init; }

        /// <summary>Scope the inherited value comes from; null means the compiled default.</summary>
        public ProvisioningSettingsScope? InheritedSource { get; init; }

        /// <summary>Whether the field is stored on this level in the database.</summary>
        public bool WasLocal { get; init; }

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

        public static ProvisioningFieldState Create(ProvisioningFieldDefinition field, bool wasLocal, string localValue,
            string inheritedValue, ProvisioningSettingsScope? inheritedSource)
        {
            ProvisioningFieldState state = new()
            {
                Field = field,
                WasLocal = wasLocal,
                OriginalValue = wasLocal ? localValue : "",
                InheritedValue = inheritedValue,
                InheritedSource = inheritedSource
            };
            state.Reset();
            return state;
        }
    }

    /// <summary>
    /// Edit state of all fields offered on one level of the provisioning hierarchy.
    /// </summary>
    public sealed class ProvisioningLevelForm(ProvisioningNode node, List<ProvisioningFieldState> fields)
    {
        public ProvisioningNode Node { get; } = node;

        public List<ProvisioningFieldState> Fields { get; } = fields;

        public bool HasChanges => Fields.Any(f => f.IsChanged);

        /// <summary>Discards all unsaved edits.</summary>
        public void Reset() => Fields.ForEach(f => f.Reset());

        /// <summary>
        /// Translates the final UI state into an explicit patch: new or changed local values become upserts,
        /// fields switched back to inherited become removals, everything untouched is left out.
        /// </summary>
        public ProvisioningSettingsChangeSet BuildChangeSet(ProvisioningSettingsScope scope)
        {
            ProvisioningSettingsChangeSet changes = new(scope);
            foreach (ProvisioningFieldState field in Fields.Where(f => f.IsChanged))
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
    /// Connects the provisioning settings UI to <see cref="ProvisioningSettingsManager"/>: loads the persisted
    /// hierarchy, resolves the values of a level for editing and saves one form as one patch.
    /// </summary>
    public sealed class ProvisioningSettingsService(ProvisioningSettingsManager manager)
    {
        /// <summary>Resolved values of one level, formatted for the editors.</summary>
        private sealed record ResolvedLevel(
            Dictionary<ProvisioningSettingKey, string> Values,
            Dictionary<ProvisioningSettingKey, ProvisioningSettingsScope?> Sources,
            IReadOnlySet<ProvisioningSettingKey> DirectOverrides);

        /// <summary>Loads the tree of the given managements, linked to the provisioning nodes persisted so far.</summary>
        public async Task<ProvisioningNode> LoadHierarchyAsync(IEnumerable<Management> managements)
        {
            return ProvisioningSettingsData.BuildHierarchy(managements, await LoadPersistedScopesAsync());
        }

        /// <summary>Loads the effective values, their sources and the local overrides of a level.</summary>
        public async Task<ProvisioningLevelForm> LoadFormAsync(ProvisioningNode node)
        {
            ResolvedLevel level = await ResolveAsync(node);
            ResolvedLevel? parentLevel = null;
            if (level.DirectOverrides.Count > 0)
            {
                parentLevel = node.Parent == null ? ResolveDefaults(node.Level) : await ResolveAsync(node.Parent);
            }

            List<ProvisioningFieldState> fields = [];
            foreach (ProvisioningFieldDefinition field in ProvisioningSettingsData.FieldsFor(node))
            {
                bool wasLocal = level.DirectOverrides.Contains(field.Key);
                ResolvedLevel inherited = wasLocal ? parentLevel! : level;
                (string inheritedValue, ProvisioningSettingsScope? inheritedSource) = ValueOf(inherited, field, node.Level);
                fields.Add(ProvisioningFieldState.Create(field, wasLocal, level.Values[field.Key], inheritedValue, inheritedSource));
            }
            return new ProvisioningLevelForm(node, fields);
        }

        /// <summary>
        /// Saves the form as one patch. Before the level is written, every level above it is persisted and every
        /// persisted level on the way down is placed below its current parent in the tree - a management whose device
        /// type changed or a gateway assigned to another management is moved there. Updates the tree node's scope afterwards.
        /// </summary>
        public async Task SaveAsync(ProvisioningLevelForm form)
        {
            if (!form.HasChanges)
            {
                return;
            }

            ProvisioningNode node = form.Node;
            if (node.IsPersisted || form.Fields.Any(f => f.IsChanged && f.IsLocal))
            {
                foreach (ProvisioningNode level in node.SelfAndAncestors().Reverse())
                {
                    await PlaceBelowTreeParentAsync(level, level != node);
                }
            }

            ProvisioningSettingsScope persisted = await manager.ApplyChangesAsync(form.BuildChangeSet(RequestScope(node)));
            if (persisted.NodeId > 0)
            {
                node.Scope = persisted;
            }
        }

        /// <summary>True if the node is persisted below another node than its parent in the tree.</summary>
        public static bool IsMisplaced(ProvisioningNode node) =>
            node.IsPersisted && node.Parent != null && node.Scope.ParentNodeId != node.Parent.Scope.NodeId;

        /// <summary>
        /// Persists the node if requested and it is not stored yet, and moves a stored node below its tree parent
        /// if it is stored below another node. Expects the tree parent to be persisted already.
        /// </summary>
        private async Task PlaceBelowTreeParentAsync(ProvisioningNode node, bool persistIfMissing)
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

        /// <summary>Collects every persisted provisioning node, starting from Global.</summary>
        private async Task<List<ProvisioningSettingsScope>> LoadPersistedScopesAsync()
        {
            ProvisioningSettingsLevel<GlobalProvisioningSettings> global =
                await manager.LoadLevelAsync<GlobalProvisioningSettings>(GlobalRequestScope());
            if (global.Scope.NodeId == 0)
            {
                return [];
            }

            List<ProvisioningSettingsScope> scopes = [global.Scope];
            List<ProvisioningSettingsScope> parents = [global.Scope];
            while (parents.Count > 0)
            {
                List<ProvisioningSettingsScope> children = [];
                foreach (ProvisioningSettingsScope parent in parents.Where(p => p.ScopeType != ProvisioningScopeType.Gateway))
                {
                    children.AddRange(await manager.GetChildrenAsync(parent.NodeId));
                }
                scopes.AddRange(children);
                parents = children;
            }
            return scopes;
        }

        /// <summary>
        /// Resolves the values in effect at a node along its path in the tree, i.e. along the current device hierarchy
        /// and not along the stored parent links, which are outdated for a management or gateway moved since it was stored.
        /// </summary>
        private async Task<ResolvedLevel> ResolveAsync(ProvisioningNode node)
        {
            List<ProvisioningSettingsScope> chain = [.. node.SelfAndAncestors().Reverse().Select(RequestScope)];
            (GlobalProvisioningSettings settings, IReadOnlySet<ProvisioningSettingKey> direct,
                IReadOnlyDictionary<ProvisioningSettingKey, ProvisioningSettingValueSource> sources) =
                await LoadLevelAlongChainAsync(chain);

            Dictionary<ProvisioningSettingKey, string> values = [];
            Dictionary<ProvisioningSettingKey, ProvisioningSettingsScope?> valueSources = [];
            foreach (ProvisioningFieldDefinition field in ProvisioningSettingsData.Fields.Where(f => f.Key.IsAllowedAt(node.Level)))
            {
                values[field.Key] = field.Read(settings);
                valueSources[field.Key] = sources.TryGetValue(field.Key, out ProvisioningSettingValueSource? source) ? source.Scope : null;
            }

            return new ResolvedLevel(values, valueSources, direct);
        }

        private static ResolvedLevel ResolveDefaults(ProvisioningScopeType level)
        {
            GlobalProvisioningSettings defaults = ProvisioningSettingsData.CreateDefaults(level);
            List<ProvisioningFieldDefinition> fields = [.. ProvisioningSettingsData.Fields.Where(f => f.Key.IsAllowedAt(level))];
            return new ResolvedLevel(
                fields.ToDictionary(f => f.Key, f => f.Read(defaults)),
                fields.ToDictionary(f => f.Key, _ => (ProvisioningSettingsScope?)null),
                new HashSet<ProvisioningSettingKey>());
        }

        private static (string Value, ProvisioningSettingsScope? Source) ValueOf(ResolvedLevel level, ProvisioningFieldDefinition field,
            ProvisioningScopeType fieldLevel)
        {
            return level.Values.TryGetValue(field.Key, out string? value)
                ? (value, level.Sources[field.Key])
                : (field.Read(ProvisioningSettingsData.CreateDefaults(fieldLevel)), null);
        }

        private async Task<(GlobalProvisioningSettings, IReadOnlySet<ProvisioningSettingKey>,
            IReadOnlyDictionary<ProvisioningSettingKey, ProvisioningSettingValueSource>)> LoadLevelAlongChainAsync(
            List<ProvisioningSettingsScope> chain)
        {
            ProvisioningScopeType level = chain[^1].ScopeType;
            return level switch
            {
                ProvisioningScopeType.Global => Unpack(await manager.LoadLevelAlongChainAsync<GlobalProvisioningSettings>(chain)),
                ProvisioningScopeType.DeviceType => Unpack(await manager.LoadLevelAlongChainAsync<DeviceTypeProvisioningSettings>(chain)),
                ProvisioningScopeType.Management => Unpack(await manager.LoadLevelAlongChainAsync<ManagementProvisioningSettings>(chain)),
                ProvisioningScopeType.Gateway => Unpack(await manager.LoadLevelAlongChainAsync<GatewayProvisioningSettings>(chain)),
                _ => throw new ArgumentOutOfRangeException(nameof(chain), level, "A defined provisioning scope type is required.")
            };
        }

        private static (GlobalProvisioningSettings, IReadOnlySet<ProvisioningSettingKey>,
            IReadOnlyDictionary<ProvisioningSettingKey, ProvisioningSettingValueSource>) Unpack<TSettings>(ProvisioningSettingsLevel<TSettings> level)
            where TSettings : GlobalProvisioningSettings
        {
            return (level.Settings, level.DirectOverrides, level.ValueSources);
        }

        /// <summary>
        /// The scope to hand to the manager for a tree node: the persisted scope with the node's current name,
        /// or a new scope referencing the persisted parent.
        /// </summary>
        private static ProvisioningSettingsScope RequestScope(ProvisioningNode node)
        {
            return new ProvisioningSettingsScope
            {
                ScopeType = node.Level,
                ObjectKey = node.Scope.ObjectKey,
                DisplayName = node.Name,
                NodeId = node.Scope.NodeId,
                ParentNodeId = node.IsPersisted ? node.Scope.ParentNodeId : NullIfUnpersisted(node.Parent),
                SortOrder = node.Scope.SortOrder
            };
        }

        private static long? NullIfUnpersisted(ProvisioningNode? node) => node is { IsPersisted: true } ? node.Scope.NodeId : null;

        private static ProvisioningSettingsScope GlobalRequestScope() => new()
        {
            ScopeType = ProvisioningScopeType.Global,
            ObjectKey = ProvisioningSettingsData.GlobalObjectKey,
            DisplayName = ProvisioningSettingsData.GlobalDisplayName
        };
    }
}

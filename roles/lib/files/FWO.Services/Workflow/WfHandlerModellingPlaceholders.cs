using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Loads what the substitution of modelling placeholders in rules to write needs, see
    /// <see cref="ModellingPlaceholderSubstitution"/>: the placeholder areas of the modelling settings, the extra
    /// configurations approved with the request task and the objects imported from the target management.
    /// </summary>
    public partial class WfHandler
    {
        private const string kPlaceholderLogTitle = "Modelling Placeholders";
        private static readonly List<ModellingExtraConfig> kNoExtraConfigs = [];
        private static readonly List<ModellingPlaceholderArea> kNoPlaceholderAreas = [];

        /// <summary>Why the rule of a new or modified rule task cannot be written automatically, by implementation task id.</summary>
        private Dictionary<long, string> placeholderNotWritableReasons = [];

        /// <summary>
        /// Data loaded once per run: the placeholder areas, the connections by id, the managements and the imported objects
        /// by target management.
        /// </summary>
        private sealed class ModellingPlaceholderCache
        {
            public List<ModellingPlaceholderArea>? Areas { get; set; }

            public Dictionary<long, ModellingConnection?> Connections { get; } = [];

            public List<Management>? Managements { get; set; }

            public Dictionary<int, Dictionary<ModellingPlaceholderKind, IReadOnlySet<string>>> ImportedObjectNames { get; } = [];
        }

        /// <summary>
        /// Substitutes the modelling placeholders of an implementation task to write by the objects the extra
        /// configurations approved with its request task name, loaded now.
        /// </summary>
        /// <param name="reqTask">Request task of the implementation task, naming the modelled connection.</param>
        /// <param name="implTask">Implementation task to write.</param>
        /// <returns>The task to write with a note on the substitution, or why it cannot be written automatically.</returns>
        public async Task<ModellingPlaceholderResult> SubstituteModellingPlaceholders(WfReqTask reqTask, WfImplTask implTask)
        {
            return await SubstituteModellingPlaceholders(reqTask, implTask, new ModellingPlaceholderCache());
        }

        /// <summary>
        /// Determines for the new and modified rules of the request tasks whether their placeholders can be substituted, so
        /// that a rule that cannot be written automatically is reported as gap before anything is written. A task whose
        /// data cannot be loaded is not writable either, as it might contain a placeholder.
        /// </summary>
        private async Task LoadPlaceholderReasons(List<WfReqTask> reqTasks)
        {
            placeholderNotWritableReasons = [];
            if (apiConnection == null)
            {
                return;
            }
            ModellingPlaceholderCache cache = new();
            foreach (WfReqTask reqTask in reqTasks)
            {
                foreach (WfImplTask implTask in reqTask.ImplementationTasks.Where(task => ModellingPlaceholderSubstitution.AppliesTo(task.TaskType)))
                {
                    string? reason = await PlaceholderNotWritableReason(reqTask, implTask, cache);
                    if (reason != null)
                    {
                        placeholderNotWritableReasons[implTask.Id] = reason;
                    }
                }
            }
        }

        private async Task<string?> PlaceholderNotWritableReason(WfReqTask reqTask, WfImplTask implTask, ModellingPlaceholderCache cache)
        {
            try
            {
                return (await SubstituteModellingPlaceholders(reqTask, implTask, cache)).NotWritableReason;
            }
            catch (Exception exception)
            {
                Log.WriteError(kPlaceholderLogTitle, $"The modelling placeholders of implementation task {implTask.Id} could not be checked.", exception);
                return "its modelling placeholders and extra configurations could not be loaded; implement it manually.";
            }
        }

        private async Task<ModellingPlaceholderResult> SubstituteModellingPlaceholders(WfReqTask reqTask, WfImplTask implTask, ModellingPlaceholderCache cache)
        {
            if (!ModellingPlaceholderSubstitution.AppliesTo(implTask.TaskType) || apiConnection == null)
            {
                return new(implTask, null, null);
            }
            cache.Areas ??= await LoadPlaceholderAreas();
            ModellingPlaceholderInput input = await LoadPlaceholderInput(reqTask, cache);
            if (input.ExtraConfigsFromRequest && input.ExtraConfigs != null && input.ExtraConfigs.Any(ModellingPlaceholderSubstitution.IsSubstitutable))
            {
                int? managementId = await ResolveImplTaskManagementId(reqTask, implTask);
                input = input with
                {
                    ManagementId = managementId,
                    ImportedObjectNames = managementId == null ? null : await LoadImportedObjectNames(managementId.Value, cache)
                };
            }
            return ModellingPlaceholderSubstitution.Apply(implTask, input);
        }

        /// <summary>
        /// The extra configurations of the modelled connection of a request task as approved: those saved with the request
        /// task. A request task created before they were saved with it falls back to the connection as it is now, which
        /// only decides whether the rule is left to a person. The connection id comes from the additional info of the
        /// request task, which the requester can write, so such a connection is only used if it belongs to an owner of the
        /// request task.
        /// </summary>
        private async Task<ModellingPlaceholderInput> LoadPlaceholderInput(WfReqTask reqTask, ModellingPlaceholderCache cache)
        {
            List<ModellingPlaceholderArea> areas = cache.Areas ?? kNoPlaceholderAreas;
            int? connectionId = reqTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId);
            if (connectionId == null)
            {
                return new(areas, null, kNoExtraConfigs);
            }
            List<ModellingExtraConfig>? requested = ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.ExtraConfigs);
            if (requested != null)
            {
                List<ModellingExtraConfig> requestedFromInterface = ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.InterfaceExtraConfigs) ?? kNoExtraConfigs;
                return new(areas, requested, requestedFromInterface);
            }
            return await LoadCurrentPlaceholderInput(reqTask, connectionId.Value, areas, cache);
        }

        /// <summary>The extra configurations of the connection as it is now, for a request task without saved ones.</summary>
        private async Task<ModellingPlaceholderInput> LoadCurrentPlaceholderInput(WfReqTask reqTask, long connectionId,
            List<ModellingPlaceholderArea> areas, ModellingPlaceholderCache cache)
        {
            ModellingConnection? connection = await LoadPlaceholderConnection(connectionId, cache);
            if (connection == null || !BelongsToReqTaskOwner(connection, reqTask))
            {
                Log.WriteWarning(kPlaceholderLogTitle, $"Connection {connectionId} of request task {reqTask.Id} does not exist or belongs to no owner of the request task.");
                return new(areas, null, kNoExtraConfigs);
            }
            List<ModellingExtraConfig> interfaceExtraConfigs = kNoExtraConfigs;
            if (connection.UsedInterfaceId is long interfaceId)
            {
                ModellingConnection? usedInterface = await LoadPlaceholderConnection(interfaceId, cache);
                interfaceExtraConfigs = usedInterface?.ExtraConfigs ?? kNoExtraConfigs;
            }
            return new(areas, connection.ExtraConfigs, interfaceExtraConfigs) { ExtraConfigsFromRequest = false };
        }

        private static bool BelongsToReqTaskOwner(ModellingConnection connection, WfReqTask reqTask)
        {
            int? appId = connection.AppId ?? connection.ProposedAppId;
            return appId != null && reqTask.Owners.Any(ownerData => ownerData.Owner.Id == appId.Value);
        }

        private async Task<ModellingConnection?> LoadPlaceholderConnection(long connectionId, ModellingPlaceholderCache cache)
        {
            if (!cache.Connections.TryGetValue(connectionId, out ModellingConnection? connection))
            {
                List<ModellingConnection> connections = await apiConnection!.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new { id = connectionId });
                connection = connections.FirstOrDefault();
                cache.Connections[connectionId] = connection;
            }
            return connection;
        }

        /// <summary>
        /// Names of the active objects imported from the target management, and from its super manager holding the global
        /// objects, that a placeholder may be substituted by, per kind and ignoring case like the firewall.
        /// </summary>
        private async Task<Dictionary<ModellingPlaceholderKind, IReadOnlySet<string>>> LoadImportedObjectNames(int managementId, ModellingPlaceholderCache cache)
        {
            if (cache.ImportedObjectNames.TryGetValue(managementId, out Dictionary<ModellingPlaceholderKind, IReadOnlySet<string>>? cached))
            {
                return cached;
            }
            cache.Managements ??= await apiConnection!.SendQueryAsync<List<Management>>(DeviceQueries.getManagementHierarchy);
            List<int> managementIds = [managementId];
            int? superManagerId = cache.Managements.FirstOrDefault(management => management.Id == managementId)?.SuperManagerId;
            if (superManagerId is int superId && superId != managementId)
            {
                managementIds.Add(superId);
            }
            List<string> objectTypes = [.. ModellingPlaceholderSubstitution.AllKinds.Select(ModellingPlaceholderSubstitution.ImportedObjectType)];
            List<NetworkObject> objects = await apiConnection!.SendQueryAsync<List<NetworkObject>>(ObjectQueries.getImportedObjectsOfTypes,
                new { mgmIds = managementIds, objTypes = objectTypes });
            Dictionary<ModellingPlaceholderKind, IReadOnlySet<string>> names = ModellingPlaceholderSubstitution.AllKinds.ToDictionary(kind => kind, kind =>
                (IReadOnlySet<string>)objects
                    .Where(obj => obj.Type.Name == ModellingPlaceholderSubstitution.ImportedObjectType(kind))
                    .Select(obj => obj.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
            cache.ImportedObjectNames[managementId] = names;
            return names;
        }

        /// <summary>
        /// The placeholder areas of the modelling settings ("Network Areas for Users" and "Network Areas for Updatable
        /// Objects") with the id strings that modelling requests name them by.
        /// </summary>
        private async Task<List<ModellingPlaceholderArea>> LoadPlaceholderAreas()
        {
            List<(CommonAreaConfig Config, ModellingPlaceholderKind Kind)> configs =
            [
                .. ParsePlaceholderAreaConfigs(userConfig.ModSpecUserAreas, ModellingPlaceholderKind.User),
                .. ParsePlaceholderAreaConfigs(userConfig.ModUpdatableObjAreas, ModellingPlaceholderKind.UpdatableObject)
            ];
            if (configs.Count == 0)
            {
                return [];
            }
            List<long> areaIds = [.. configs.Select(config => config.Config.AreaId).Distinct()];
            var where = new { id = new { _in = areaIds } };
            List<ModellingNetworkArea> areas = await apiConnection!.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getNetworkAreas,
                new { where, limit = areaIds.Count, offset = 0 });
            List<ModellingPlaceholderArea> placeholderAreas = [];
            foreach ((CommonAreaConfig config, ModellingPlaceholderKind kind) in configs)
            {
                ModellingNetworkArea? area = areas.FirstOrDefault(a => a.Id == config.AreaId);
                if (area != null && !string.IsNullOrWhiteSpace(area.IdString))
                {
                    placeholderAreas.Add(new(area.IdString, kind, config.UseInSrc, config.UseInDst));
                }
            }
            return placeholderAreas;
        }

        private static IEnumerable<(CommonAreaConfig Config, ModellingPlaceholderKind Kind)> ParsePlaceholderAreaConfigs(string configValue, ModellingPlaceholderKind kind)
        {
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return [];
            }
            List<CommonAreaConfig> configs = JsonSerializer.Deserialize<List<CommonAreaConfig>>(configValue) ?? [];
            return configs.Select(config => (config, kind));
        }
    }
}

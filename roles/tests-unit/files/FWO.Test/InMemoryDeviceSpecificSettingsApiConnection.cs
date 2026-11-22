using System.Collections;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using Newtonsoft.Json.Linq;

namespace FWO.Test;

internal sealed record DeviceSpecificSettingsApiCall(string Query, object? Variables);

/// <summary>
/// Simulates the device specific settings GraphQL operations on an in-memory node list.
/// </summary>
internal sealed class InMemoryDeviceSpecificSettingsApiConnection : SimulatedApiConnection
{
    public List<DeviceSpecificConfigNodeData> Nodes { get; } = [];

    /// <summary>Managements returned for the management query of the device specific settings page.</summary>
    public List<Management> Managements { get; } = [];

    public List<DeviceSpecificSettingsApiCall> Calls { get; } = [];

    /// <summary>Answers of the queries for the imported layers of rules and for the section links, one list per query as the api returns it.</summary>
    public FirewallRuleLayerQueryResult RuleLayers { get; set; } = new();

    /// <summary>Rules with the gateways they take effect on, returned for the query of the enforcing gateways.</summary>
    public List<Rule> EnforcingRules { get; } = [];

    /// <summary>Modelling connections returned for the query of a connection by id.</summary>
    public List<FWO.Data.Modelling.ModellingConnection> Connections { get; } = [];

    /// <summary>Network areas returned for the query of network areas, filtered by the ids of its where variable.</summary>
    public List<FWO.Data.Modelling.ModellingNetworkArea> NetworkAreas { get; } = [];

    /// <summary>Imported network objects returned for the query of imported objects of given types, filtered by management and type.</summary>
    public List<(int ManagementId, NetworkObject Object)> ImportedObjects { get; } = [];

    /// <summary>Management ids of the imported object queries, one list per query.</summary>
    public List<List<int>> ImportedObjectManagementIds { get; } = [];

    public string? FailingQuery { get; set; }

    public Exception? Failure { get; set; }

    /// <summary>Managements with the id of the getManagementById variables.</summary>
    private List<Management> ManagementsById(object? variables)
    {
        object? id = variables?.GetType().GetProperty("id")?.GetValue(variables);
        return [.. Managements.Where(management => Equals(management.Id, id))];
    }

    /// <summary>Gateways with the id of the getGatewayRulebaseLinks variables, with their package and rulebase links.</summary>
    private List<Device> GatewaysById(object? variables)
    {
        object? id = variables?.GetType().GetProperty("gwId")?.GetValue(variables);
        return [.. Managements.SelectMany(management => management.Devices).Where(device => Equals(device.Id, id))];
    }

    /// <summary>Gateways with the implementation-target gateway ID.</summary>
    private List<Device> ImplementationTaskGatewaysById(object? variables)
    {
        object? id = variables?.GetType().GetProperty("gatewayId")?.GetValue(variables);
        return [.. Managements.SelectMany(management => management.Devices).Where(device => Equals(device.Id, id))];
    }

    /// <summary>Connections with the id of the getConnectionById variables.</summary>
    private List<FWO.Data.Modelling.ModellingConnection> ConnectionsById(object? variables)
    {
        object? id = variables?.GetType().GetProperty("id")?.GetValue(variables);
        return [.. Connections.Where(connection => Convert.ToInt64(id) == connection.Id)];
    }

    /// <summary>Network areas whose id is in the id filter of the getNetworkAreas where variable.</summary>
    private List<FWO.Data.Modelling.ModellingNetworkArea> NetworkAreasByIds(object? variables)
    {
        object? where = variables?.GetType().GetProperty("where")?.GetValue(variables);
        object? idFilter = where?.GetType().GetProperty("id")?.GetValue(where);
        List<long> ids = idFilter?.GetType().GetProperty("_in")?.GetValue(idFilter) as List<long> ?? [];
        return [.. NetworkAreas.Where(area => ids.Contains(area.Id))];
    }

    /// <summary>Imported objects of the managements and types of the getImportedObjectsOfTypes variables.</summary>
    private List<NetworkObject> ImportedObjectsOfTypes(object? variables)
    {
        List<int> managementIds = RequiredProperty<List<int>>(RequiredVariables(variables), "mgmIds");
        List<string> objectTypes = RequiredProperty<List<string>>(RequiredVariables(variables), "objTypes");
        ImportedObjectManagementIds.Add(managementIds);
        return [.. ImportedObjects
            .Where(imported => managementIds.Contains(imported.ManagementId) && objectTypes.Contains(imported.Object.Type.Name))
            .Select(imported => imported.Object)];
    }

    /// <summary>Additional info of request tasks by id, for getRequestTaskAdditionalInfo.</summary>
    public Dictionary<long, string> RequestTaskAdditionalInfo { get; } = [];

    /// <summary>Owner ids of request tasks by id, for getRequestTaskAdditionalInfo.</summary>
    public Dictionary<long, List<int>> RequestTaskOwners { get; } = [];

    private List<FWO.Data.Workflow.WfReqTask> RequestTasksById(object? variables)
    {
        long id = RequiredProperty<long>(RequiredVariables(variables), "id");
        List<FwoOwnerDataHelper> owners = [.. RequestTaskOwners.GetValueOrDefault(id, []).Select(ownerId => new FwoOwnerDataHelper { Owner = new() { Id = ownerId } })];
        return RequestTaskAdditionalInfo.TryGetValue(id, out string? additionalInfo)
            ? [new() { Id = id, AdditionalInfo = additionalInfo, Owners = owners }]
            : [];
    }

    /// <summary>Optional test hook that can pause a query after it has been recorded and before it is applied.</summary>
    public Func<string, Task>? BeforeQueryAsync { get; set; }

    public int UpsertCallCount => Calls.Count(call => call.Query == DeviceSpecificSettingsQueries.upsertNode);

    public int PatchCallCount => Calls.Count(call => call.Query == DeviceSpecificSettingsQueries.applyPatch);

    public int DeleteCallCount => Calls.Count(call => call.Query == DeviceSpecificSettingsQueries.deleteOverrides);

    public IReadOnlyList<string> LastPatchUpsertKeys { get; private set; } = [];

    public IReadOnlyList<string> LastPatchRemoveKeys { get; private set; } = [];

    public IReadOnlyList<string> LastDeletedKeys { get; private set; } = [];

    /// <summary>Adds a persisted device specific settings node without values.</summary>
    public DeviceSpecificConfigNodeData AddNode(
        long id,
        DeviceSpecificScopeType scopeType,
        string objectKey,
        long? parentId = null,
        string? displayName = null)
    {
        DeviceSpecificConfigNodeData node = new()
        {
            Id = id,
            NodeType = DeviceSpecificSettingsScopeFactory.ToNodeType(scopeType),
            ObjectKey = objectKey,
            ParentId = parentId,
            DisplayName = displayName ?? objectKey
        };
        Nodes.Add(node);
        return node;
    }

    public void SetValue<TValue>(
        long nodeId,
        DeviceSpecificSettingKey<TValue> key,
        TValue value)
    {
        JToken serialized = DeviceSpecificSettingValueSerializer.Serialize(key, value);
        DeviceSpecificConfigNodeData node = Nodes.Single(node => node.Id == nodeId);
        DeviceSpecificConfigValueData? existing = node.Values.SingleOrDefault(item => item.ConfigKey == key.DatabaseKey);
        if (existing is null)
        {
            node.Values.Add(new DeviceSpecificConfigValueData
            {
                NodeId = nodeId,
                ConfigKey = key.DatabaseKey,
                ConfigValue = serialized
            });
        }
        else
        {
            existing.ConfigValue = serialized;
        }
    }

    public JToken? ReadValue(long nodeId, DeviceSpecificSettingKey key)
    {
        return Nodes.Single(node => node.Id == nodeId)
            .Values.SingleOrDefault(value => value.ConfigKey == key.DatabaseKey)
            ?.ConfigValue;
    }

    public override async Task<T> SendQueryAsync<T>(
        string query,
        object? variables = null,
        string? operationName = null,
        QueryChunkingOptions? chunkingOptions = null)
    {
        Calls.Add(new DeviceSpecificSettingsApiCall(query, variables));
        if (query == FailingQuery)
        {
            throw Failure ?? new InvalidOperationException("Simulated API failure.");
        }

        if (BeforeQueryAsync != null)
        {
            await BeforeQueryAsync(query);
        }

        RefreshParentLinks();
        object result = query switch
        {
            _ when query == DeviceSpecificSettingsQueries.getNodeWithAncestors => GetNodeWithAncestors(variables),
            _ when query == DeviceSpecificSettingsQueries.getNodeById => GetNodeById(variables),
            _ when query == DeviceSpecificSettingsQueries.getNodesByNaturalKeys => GetNodesByNaturalKeys(variables),
            _ when query == DeviceSpecificSettingsQueries.getAllNodes => Nodes.ToList(),
            _ when query == DeviceQueries.getManagementDetailsWithoutSecrets => Managements,
            _ when query == DeviceQueries.getManagementHierarchy => Managements,
            _ when query == DeviceQueries.getManagementById => ManagementsById(variables),
            _ when query == DeviceQueries.getImplementationTaskGatewayTarget => ImplementationTaskGatewaysById(variables),
            _ when query == DeviceQueries.getGatewayRulebaseLinks => GatewaysById(variables),
            _ when query == RuleQueries.getRuleLayers => RuleLayers.Rules,
            _ when query == RuleQueries.getSectionLinks => RuleLayers.SectionLinks,
            _ when query == RuleQueries.getRuleEnforcingGateways => EnforcingRules,
            _ when query == DeviceSpecificSettingsQueries.upsertNode => UpsertNode(variables),
            _ when query == DeviceSpecificSettingsQueries.applyPatch => ApplyPatch(variables),
            _ when query == DeviceSpecificSettingsQueries.deleteOverrides => DeleteOverrides(variables),
            _ when query == ModellingQueries.getConnectionById => ConnectionsById(variables),
            _ when query == ModellingQueries.getNetworkAreas => NetworkAreasByIds(variables),
            _ when query == ObjectQueries.getImportedObjectsOfTypes => ImportedObjectsOfTypes(variables),
            _ when query == RequestQueries.getRequestTaskAdditionalInfo => RequestTasksById(variables),
            _ when query == MonitorQueries.getOpenAlerts => new List<Alert>(),
            _ when query == MonitorQueries.addAlert => new ReturnIdWrapper(),
            _ => throw new InvalidOperationException("Unexpected device specific settings query.")
        };

        return (T)result;
    }

    private List<DeviceSpecificConfigNodeData> GetNodeWithAncestors(object? variables)
    {
        string nodeType = RequiredProperty<string>(RequiredVariables(variables), "nodeType");
        string objectKey = RequiredProperty<string>(RequiredVariables(variables), "objectKey");
        return Nodes
            .Where(node => node.NodeType == nodeType && node.ObjectKey == objectKey)
            .ToList();
    }

    private List<DeviceSpecificConfigNodeData> GetNodeById(object? variables)
    {
        long nodeId = RequiredProperty<long>(RequiredVariables(variables), "nodeId");
        return Nodes.Where(node => node.Id == nodeId).ToList();
    }

    private List<DeviceSpecificConfigNodeData> GetNodesByNaturalKeys(object? variables)
    {
        object vars = RequiredVariables(variables);
        Dictionary<string, List<string>> keysByNodeType = new()
        {
            [DeviceSpecificSettingsScopeFactory.GlobalNodeType] = RequiredProperty<List<string>>(vars, "globalKeys"),
            [DeviceSpecificSettingsScopeFactory.DeviceTypeNodeType] = RequiredProperty<List<string>>(vars, "deviceTypeKeys"),
            [DeviceSpecificSettingsScopeFactory.ManagementNodeType] = RequiredProperty<List<string>>(vars, "managementKeys"),
            [DeviceSpecificSettingsScopeFactory.GatewayNodeType] = RequiredProperty<List<string>>(vars, "gatewayKeys")
        };
        return Nodes
            .Where(node => keysByNodeType.TryGetValue(node.NodeType, out List<string>? keys) && keys.Contains(node.ObjectKey))
            .ToList();
    }

    private DeviceSpecificConfigNodeData UpsertNode(object? variables)
    {
        object input = RequiredProperty<object>(RequiredVariables(variables), "node");
        string nodeType = RequiredProperty<string>(input, "node_type");
        string objectKey = RequiredProperty<string>(input, "object_key");
        DeviceSpecificConfigNodeData? node = Nodes.SingleOrDefault(item =>
            item.NodeType == nodeType && item.ObjectKey == objectKey);

        if (node is null)
        {
            node = new DeviceSpecificConfigNodeData
            {
                Id = Nodes.Count == 0 ? 1 : Nodes.Max(item => item.Id) + 1,
                NodeType = nodeType,
                ObjectKey = objectKey
            };
            Nodes.Add(node);
        }

        node.ParentId = OptionalProperty<long>(input, "parent_id");
        node.DisplayName = RequiredProperty<string>(input, "display_name");
        node.SortOrder = OptionalProperty<int>(input, "sort_order");
        RefreshParentLinks();
        return node;
    }

    private ReturnId ApplyPatch(object? variables)
    {
        object vars = RequiredVariables(variables);
        IEnumerable upserts = RequiredProperty<IEnumerable>(vars, "upserts");
        List<string> upsertKeys = [];

        foreach (object upsert in upserts)
        {
            long nodeId = RequiredProperty<long>(upsert, "node_id");
            string configKey = RequiredProperty<string>(upsert, "config_key");
            JToken configValue = RequiredProperty<JToken>(upsert, "config_value");
            DeviceSpecificConfigNodeData node = Nodes.Single(item => item.Id == nodeId);
            DeviceSpecificConfigValueData? existing = node.Values.SingleOrDefault(item => item.ConfigKey == configKey);
            if (existing is null)
            {
                node.Values.Add(new DeviceSpecificConfigValueData
                {
                    NodeId = nodeId,
                    ConfigKey = configKey,
                    ConfigValue = configValue.DeepClone()
                });
            }
            else
            {
                existing.ConfigValue = configValue.DeepClone();
            }
            upsertKeys.Add(configKey);
        }

        long removalNodeId = RequiredProperty<long>(vars, "nodeId");
        List<string> removeKeys = RequiredProperty<List<string>>(vars, "removeKeys");
        RemoveValues(removalNodeId, removeKeys);
        LastPatchUpsertKeys = upsertKeys;
        LastPatchRemoveKeys = removeKeys;
        return new ReturnId { AffectedRows = upsertKeys.Count + removeKeys.Count };
    }

    private ReturnId DeleteOverrides(object? variables)
    {
        object vars = RequiredVariables(variables);
        long nodeId = RequiredProperty<long>(vars, "nodeId");
        List<string> configKeys = RequiredProperty<List<string>>(vars, "configKeys");
        int removed = RemoveValues(nodeId, configKeys);
        LastDeletedKeys = configKeys;
        return new ReturnId { AffectedRows = removed };
    }

    private int RemoveValues(long nodeId, IReadOnlyCollection<string> configKeys)
    {
        DeviceSpecificConfigNodeData node = Nodes.Single(item => item.Id == nodeId);
        return node.Values.RemoveAll(value => configKeys.Contains(value.ConfigKey));
    }

    private void RefreshParentLinks()
    {
        foreach (DeviceSpecificConfigNodeData node in Nodes)
        {
            node.ParentNode = node.ParentId is long parentId
                ? Nodes.SingleOrDefault(parent => parent.Id == parentId)
                : null;
        }
    }

    private static object RequiredVariables(object? variables)
    {
        return variables ?? throw new InvalidOperationException("Expected query variables.");
    }

    private static T RequiredProperty<T>(object source, string propertyName)
    {
        object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Expected variable property '{propertyName}' of type '{typeof(T).Name}'.");
    }

    private static T? OptionalProperty<T>(object source, string propertyName)
        where T : struct
    {
        object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value is null ? null : (T)value;
    }
}

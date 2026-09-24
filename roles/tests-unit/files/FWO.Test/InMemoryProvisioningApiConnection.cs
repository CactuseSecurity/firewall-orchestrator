using System.Collections;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Config.Api.Provisioning;
using FWO.Data;
using FWO.Data.Provisioning;
using Newtonsoft.Json.Linq;

namespace FWO.Test;

internal sealed record ProvisioningApiCall(string Query, object? Variables);

/// <summary>
/// Simulates the provisioning GraphQL operations on an in-memory node list.
/// </summary>
internal sealed class InMemoryProvisioningApiConnection : SimulatedApiConnection
{
    public List<ProvisioningConfigNodeData> Nodes { get; } = [];

    public List<ProvisioningApiCall> Calls { get; } = [];

    public string? FailingQuery { get; set; }

    public Exception? Failure { get; set; }

    public int UpsertCallCount => Calls.Count(call => call.Query == ProvisioningQueries.upsertNode);

    public int PatchCallCount => Calls.Count(call => call.Query == ProvisioningQueries.applyPatch);

    public int DeleteCallCount => Calls.Count(call => call.Query == ProvisioningQueries.deleteOverrides);

    public IReadOnlyList<string> LastPatchUpsertKeys { get; private set; } = [];

    public IReadOnlyList<string> LastPatchRemoveKeys { get; private set; } = [];

    public IReadOnlyList<string> LastDeletedKeys { get; private set; } = [];

    /// <summary>Adds a persisted provisioning node without values.</summary>
    public ProvisioningConfigNodeData AddNode(
        long id,
        ProvisioningScopeType scopeType,
        string objectKey,
        long? parentId = null,
        string? displayName = null)
    {
        ProvisioningConfigNodeData node = new()
        {
            Id = id,
            NodeType = ProvisioningSettingsScopeFactory.ToNodeType(scopeType),
            ObjectKey = objectKey,
            ParentId = parentId,
            DisplayName = displayName ?? objectKey
        };
        Nodes.Add(node);
        return node;
    }

    public void SetValue<TValue>(
        long nodeId,
        ProvisioningSettingKey<TValue> key,
        TValue value)
    {
        JToken serialized = new ProvisioningSettingValueSerializer().Serialize(key, value);
        ProvisioningConfigNodeData node = Nodes.Single(node => node.Id == nodeId);
        ProvisioningConfigValueData? existing = node.Values.SingleOrDefault(item => item.ConfigKey == key.DatabaseKey);
        if (existing is null)
        {
            node.Values.Add(new ProvisioningConfigValueData
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

    public JToken? ReadValue(long nodeId, ProvisioningSettingKey key)
    {
        return Nodes.Single(node => node.Id == nodeId)
            .Values.SingleOrDefault(value => value.ConfigKey == key.DatabaseKey)
            ?.ConfigValue;
    }

    public override Task<T> SendQueryAsync<T>(
        string query,
        object? variables = null,
        string? operationName = null,
        QueryChunkingOptions? chunkingOptions = null)
    {
        Calls.Add(new ProvisioningApiCall(query, variables));
        if (query == FailingQuery)
        {
            throw Failure ?? new InvalidOperationException("Simulated API failure.");
        }

        RefreshParentLinks();
        object result = query switch
        {
            _ when query == ProvisioningQueries.getNodeWithAncestors => GetNodeWithAncestors(variables),
            _ when query == ProvisioningQueries.getNodeById => GetNodeById(variables),
            _ when query == ProvisioningQueries.getChildren => GetChildren(variables),
            _ when query == ProvisioningQueries.upsertNode => UpsertNode(variables),
            _ when query == ProvisioningQueries.applyPatch => ApplyPatch(variables),
            _ when query == ProvisioningQueries.deleteOverrides => DeleteOverrides(variables),
            _ => throw new InvalidOperationException("Unexpected provisioning query.")
        };

        return Task.FromResult((T)result);
    }

    private List<ProvisioningConfigNodeData> GetNodeWithAncestors(object? variables)
    {
        string nodeType = RequiredProperty<string>(RequiredVariables(variables), "nodeType");
        string objectKey = RequiredProperty<string>(RequiredVariables(variables), "objectKey");
        return Nodes
            .Where(node => node.NodeType == nodeType && node.ObjectKey == objectKey)
            .ToList();
    }

    private List<ProvisioningConfigNodeData> GetNodeById(object? variables)
    {
        long nodeId = RequiredProperty<long>(RequiredVariables(variables), "nodeId");
        return Nodes.Where(node => node.Id == nodeId).ToList();
    }

    private List<ProvisioningConfigNodeData> GetChildren(object? variables)
    {
        long parentNodeId = RequiredProperty<long>(RequiredVariables(variables), "parentNodeId");
        return Nodes.Where(node => node.ParentId == parentNodeId).ToList();
    }

    private ProvisioningConfigNodeData UpsertNode(object? variables)
    {
        object input = RequiredProperty<object>(RequiredVariables(variables), "node");
        string nodeType = RequiredProperty<string>(input, "node_type");
        string objectKey = RequiredProperty<string>(input, "object_key");
        ProvisioningConfigNodeData? node = Nodes.SingleOrDefault(item =>
            item.NodeType == nodeType && item.ObjectKey == objectKey);

        if (node is null)
        {
            node = new ProvisioningConfigNodeData
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
            ProvisioningConfigNodeData node = Nodes.Single(item => item.Id == nodeId);
            ProvisioningConfigValueData? existing = node.Values.SingleOrDefault(item => item.ConfigKey == configKey);
            if (existing is null)
            {
                node.Values.Add(new ProvisioningConfigValueData
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
        string[] removeKeys = RequiredProperty<string[]>(vars, "removeKeys");
        RemoveValues(removalNodeId, removeKeys);
        LastPatchUpsertKeys = upsertKeys;
        LastPatchRemoveKeys = removeKeys;
        return new ReturnId { AffectedRows = upsertKeys.Count + removeKeys.Length };
    }

    private ReturnId DeleteOverrides(object? variables)
    {
        object vars = RequiredVariables(variables);
        long nodeId = RequiredProperty<long>(vars, "nodeId");
        string[] configKeys = RequiredProperty<string[]>(vars, "configKeys");
        int removed = RemoveValues(nodeId, configKeys);
        LastDeletedKeys = configKeys;
        return new ReturnId { AffectedRows = removed };
    }

    private int RemoveValues(long nodeId, IReadOnlyCollection<string> configKeys)
    {
        ProvisioningConfigNodeData node = Nodes.Single(item => item.Id == nodeId);
        return node.Values.RemoveAll(value => configKeys.Contains(value.ConfigKey));
    }

    private void RefreshParentLinks()
    {
        foreach (ProvisioningConfigNodeData node in Nodes)
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

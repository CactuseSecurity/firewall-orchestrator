using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;

namespace FWO.Config.Api.Data;

/// <summary>
/// Database representation returned by the provisioning configuration GraphQL operations.
/// </summary>
public sealed class ProvisioningConfigNodeData
{
    [JsonProperty("id"), JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("node_type"), JsonPropertyName("node_type")]
    public string NodeType { get; set; } = "";

    [JsonProperty("object_key"), JsonPropertyName("object_key")]
    public string ObjectKey { get; set; } = "";

    [JsonProperty("parent_id"), JsonPropertyName("parent_id")]
    public long? ParentId { get; set; }

    [JsonProperty("display_name"), JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonProperty("sort_order"), JsonPropertyName("sort_order")]
    public int? SortOrder { get; set; }

    [JsonProperty("values"), JsonPropertyName("values")]
    public List<ProvisioningConfigValueData> Values { get; set; } = [];

    [JsonProperty("parent_node"), JsonPropertyName("parent_node")]
    public ProvisioningConfigNodeData? ParentNode { get; set; }
}

/// <summary>
/// One JSONB provisioning override returned by GraphQL.
/// </summary>
public sealed class ProvisioningConfigValueData
{
    [JsonProperty("node_id"), JsonPropertyName("node_id")]
    public long NodeId { get; set; }

    [JsonProperty("config_key"), JsonPropertyName("config_key")]
    public string ConfigKey { get; set; } = "";

    [JsonProperty("config_value"), JsonPropertyName("config_value")]
    public JToken ConfigValue { get; set; } = JValue.CreateNull();
}

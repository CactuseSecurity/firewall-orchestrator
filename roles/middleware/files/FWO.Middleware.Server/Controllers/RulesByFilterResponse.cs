using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Controllers;

#pragma warning disable CS1591
public class RulesByFilterResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("result")]
    public RuleResult Result { get; set; } = new();
}

public class RuleResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("rules")]
    public List<RuleDetail> Rules { get; set; } = [];
}

public class RuleDetail
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("manager")]
    public string Manager { get; set; } = "";

    /// <summary>
    /// Type of the rule: <c>"access"</c> or <c>"nat"</c>. Matches the values of <c>options.filter.ruleTypes</c>.
    /// </summary>
    [JsonPropertyName("ruleType")]
    public string RuleType { get; set; } = "";

    [JsonPropertyName("source")]
    public List<NetworkObjectCopy> Source { get; set; } = [];

    [JsonPropertyName("sourceShort")]
    public string SourceShort { get; set; } = "";

    [JsonPropertyName("destination")]
    public List<NetworkObjectCopy> Destination { get; set; } = [];

    [JsonPropertyName("destinationShort")]
    public string DestinationShort { get; set; } = "";

    [JsonPropertyName("service")]
    public List<ServiceObject> Service { get; set; } = [];

    [JsonPropertyName("serviceShort")]
    public string ServiceShort { get; set; } = "";

    /// <summary>
    /// Translated source, destination and service of a NAT rule; omitted for access rules. The source,
    /// destination and service of the rule itself are the original (matched) values.
    /// </summary>
    [JsonPropertyName("translation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuleTranslation? Translation { get; set; }

    [JsonPropertyName("ownerInformation")]
    public OwnerInformation OwnerInformation { get; set; } = new();

    [JsonPropertyName("additionalInformation")]
    public AdditionalInformation AdditionalInformation { get; set; } = new();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("creationDate")]
    public string CreationDate { get; set; } = "";

    [JsonPropertyName("lastHitDate")]
    public string LastHitDate { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
}

/// <summary>
/// Translated source, destination and service of a NAT rule, in the same format as the original values of the rule.
/// </summary>
public class RuleTranslation
{
    [JsonPropertyName("source")]
    public List<NetworkObjectCopy> Source { get; set; } = [];

    [JsonPropertyName("sourceShort")]
    public string SourceShort { get; set; } = "";

    [JsonPropertyName("destination")]
    public List<NetworkObjectCopy> Destination { get; set; } = [];

    [JsonPropertyName("destinationShort")]
    public string DestinationShort { get; set; } = "";

    [JsonPropertyName("service")]
    public List<ServiceObject> Service { get; set; } = [];

    [JsonPropertyName("serviceShort")]
    public string ServiceShort { get; set; } = "";
}

public class OwnerInformation
{
    [JsonPropertyName("extAppId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExtAppId { get; set; }

    [JsonPropertyName("ownerIds")]
    public List<int> OwnerIds { get; set; } = [];
}

public class AdditionalInformation
{
    [JsonPropertyName("changeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChangeId { get; set; }
}

public class NetworkObjectCopy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("ip")]
    public string? Ip { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; } = "";
}

public class ServiceObject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("portEnd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PortEnd { get; set; }
}
#pragma warning restore CS1591

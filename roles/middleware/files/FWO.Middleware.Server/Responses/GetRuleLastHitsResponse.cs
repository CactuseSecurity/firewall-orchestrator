using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the response of the rule last hit lookup.
/// </summary>
public sealed class GetRuleLastHitsResponse
{
    /// <summary>
    /// Gets or sets one page of the rules of the requested management with their last hits, ordered by the
    /// internal rule metadata id. Empty when the management is unknown or not visible to the caller, the filter
    /// excludes every rule or the offset lies behind the last rule.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<RuleLastHitResponse> Rules { get; set; } = [];
}

/// <summary>
/// Represents the last hits of one rule.
/// </summary>
public sealed class RuleLastHitResponse
{
    /// <summary>
    /// Maximum number of gateways returned per rule.
    /// </summary>
    public const int kMaxGatewaysPerRule = 1000;

    /// <summary>
    /// Gets or sets the database id of the management the rule belongs to.
    /// </summary>
    [JsonPropertyName("mgmId")]
    public int MgmId { get; set; }

    /// <summary>
    /// Gets or sets the UID of the rule, unique within its management.
    /// </summary>
    [JsonPropertyName("ruleUid")]
    public string RuleUid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the current version of the rule. Empty when the rule has no name or the caller
    /// is restricted to a tenant, whose rule visibility the endpoint cannot evaluate.
    /// </summary>
    [JsonPropertyName("ruleName")]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest hit of the rule across all gateways of the management (UTC); <c>null</c> when the
    /// rule was never hit.
    /// </summary>
    [JsonPropertyName("lastHit")]
    public DateTime? LastHit { get; set; }

    /// <summary>
    /// Gets or sets the last hit of the rule per gateway, ordered by gateway id: at most
    /// <see cref="kMaxGatewaysPerRule"/> gateways, only gateways visible to the caller and matching the gateway
    /// filters. Gateways without a hit are not listed.
    /// </summary>
    [JsonPropertyName("gateways")]
    public List<RuleGatewayLastHitResponse> Gateways { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the rule has hits on more gateways than <see cref="kMaxGatewaysPerRule"/>, so
    /// <see cref="Gateways"/> is incomplete. The remaining gateways can be read with <c>options.filter.devId</c>.
    /// </summary>
    [JsonPropertyName("gatewaysTruncated")]
    public bool GatewaysTruncated { get; set; }
}

/// <summary>
/// Represents the last hit of a rule on one gateway.
/// </summary>
public sealed class RuleGatewayLastHitResponse
{
    /// <summary>
    /// Gets or sets the database id of the gateway.
    /// </summary>
    [JsonPropertyName("devId")]
    public int DevId { get; set; }

    /// <summary>
    /// Gets or sets the name of the gateway.
    /// </summary>
    [JsonPropertyName("gatewayName")]
    public string GatewayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last hit of the rule on the gateway (UTC).
    /// </summary>
    [JsonPropertyName("lastHit")]
    public DateTime LastHit { get; set; }
}

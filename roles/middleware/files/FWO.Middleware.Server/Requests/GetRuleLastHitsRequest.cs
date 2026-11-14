using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the request of the rule last hit lookup.
/// </summary>
/// <remarks>
/// The authoritative description of every key is kept in <see cref="GetRuleLastHitsValidationSchema"/> so
/// API documentation and validation help text cannot diverge. The XML documentation below repeats it for the
/// generated OpenAPI document.
/// </remarks>
public sealed class GetRuleLastHitsRequest : IRequestWithRootAdditionalData
{
    private GetRuleLastHitsOptions options = new();

    /// <summary>
    /// Gets or sets the database id of the management whose rule last hits are returned, greater than 0.
    /// Required. A management that is not visible to the caller answers with an empty list.
    /// </summary>
    [JsonPropertyName("managementId")]
    public int? ManagementId { get; set; }

    /// <summary>
    /// Gets or sets the optional output options. Defaults to an empty object, which applies no restriction. An
    /// explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("options")]
    public GetRuleLastHitsOptions Options
    {
        get => options;
        set => options = value ?? new GetRuleLastHitsOptions();
    }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the optional output options of the rule last hit lookup.
/// </summary>
public sealed class GetRuleLastHitsOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional response filter. When omitted or <c>null</c> no response field restricts the
    /// result.
    /// </summary>
    [JsonPropertyName("filter")]
    public RuleLastHitFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of rules to return, between 1 and the configured
    /// <c>maxRestResponseObjectLimit</c> (default 10000). When omitted or <c>null</c>, a page of that size is
    /// returned. Together with <see cref="Offset"/> it pages the ordered result; the <c>X-Has-More</c> response
    /// header tells whether further rules follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the optional number of rules to skip before the first returned one. Must not be negative.
    /// When omitted or <c>null</c>, no rule is skipped.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the response filter of the rule last hit lookup. Every key is nullable; a key that is omitted or
/// <c>null</c> does not restrict the result. All supplied keys must match (AND).
/// </summary>
public sealed class RuleLastHitFilter : IRequestWithAdditionalData
{
    /// <summary>
    /// Maximum length of every text filter value.
    /// </summary>
    public const int kMaxFilterTextLength = 256;

    /// <summary>
    /// Maximum number of values in a list filter.
    /// </summary>
    public const int kMaxFilterValues = 100;

    /// <summary>
    /// Gets or sets the optional exact management id filter. A value other than the requested
    /// <c>managementId</c> matches no rule.
    /// </summary>
    [JsonPropertyName("mgmId")]
    public int? MgmId { get; set; }

    /// <summary>
    /// Gets or sets the optional rule UID filter: 1 to <see cref="kMaxFilterValues"/> UIDs of at most
    /// <see cref="kMaxFilterTextLength"/> characters each. A rule matches when its UID equals one of them.
    /// </summary>
    [JsonPropertyName("ruleUid")]
    public List<string>? RuleUid { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive rule name filter of at most <see cref="kMaxFilterTextLength"/>
    /// characters. <c>*</c> matches any character sequence and <c>?</c> a single character; text without
    /// wildcards is a contains search.
    /// </summary>
    [JsonPropertyName("ruleName")]
    public string? RuleName { get; set; }

    /// <summary>
    /// Gets or sets the optional inclusive lower bound of the aggregated last hit (ISO 8601 with offset). Rules
    /// that were never hit do not match.
    /// </summary>
    [JsonPropertyName("lastHitAfter")]
    public DateTimeOffset? LastHitAfter { get; set; }

    /// <summary>
    /// Gets or sets the optional exclusive upper bound of the aggregated last hit (ISO 8601 with offset). Rules
    /// that were never hit do not match.
    /// </summary>
    [JsonPropertyName("lastHitBefore")]
    public DateTimeOffset? LastHitBefore { get; set; }

    /// <summary>
    /// Gets or sets whether only rules without any hit (<c>true</c>) or only rules with a hit (<c>false</c>) are
    /// returned. Must not be <c>true</c> together with a last hit bound.
    /// </summary>
    [JsonPropertyName("neverHit")]
    public bool? NeverHit { get; set; }

    /// <summary>
    /// Gets or sets the optional exact gateway id filter. Selects the rules hit on that gateway and limits
    /// <c>gateways</c> to it.
    /// </summary>
    [JsonPropertyName("devId")]
    public int? DevId { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive gateway name filter of at most
    /// <see cref="kMaxFilterTextLength"/> characters, with the wildcards of <see cref="RuleName"/>. Selects the
    /// rules hit on a matching gateway and limits <c>gateways</c> to the matching ones.
    /// </summary>
    [JsonPropertyName("gatewayName")]
    public string? GatewayName { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

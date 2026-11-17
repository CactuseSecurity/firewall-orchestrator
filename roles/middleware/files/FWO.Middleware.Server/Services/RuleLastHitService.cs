using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Newtonsoft.Json;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// What the caller of the rule last hit lookup may see.
/// </summary>
/// <remarks>
/// The lookup reads the API as middleware-server, which sees every management and gateway, so the visibility
/// of the caller is enforced with this scope instead of Hasura permissions.
/// </remarks>
/// <param name="VisibleManagementIds">Managements visible to the caller; null when the caller sees all.</param>
/// <param name="VisibleDeviceIds">Gateways visible to the caller; null when the caller sees all.</param>
/// <param name="RuleNamesVisible">False for a caller restricted to a tenant: which rules of a management are
/// relevant for a tenant is decided by Hasura per session, so rule names are withheld from such callers.</param>
public sealed record RuleLastHitScope(IReadOnlySet<int>? VisibleManagementIds, IReadOnlySet<int>? VisibleDeviceIds, bool RuleNamesVisible)
{
    private const string kVisibleManagementsClaim = "x-hasura-visible-managements";
    private const string kVisibleDevicesClaim = "x-hasura-visible-devices";
    private const string kTenantIdClaim = "x-hasura-tenant-id";

    /// <summary>
    /// Scope of a caller who sees every management, gateway and rule.
    /// </summary>
    public static readonly RuleLastHitScope kUnrestricted = new(null, null, true);

    /// <summary>
    /// Derives the scope from the roles and the visibility claims of the caller's JWT.
    /// </summary>
    /// <param name="user">Authenticated caller.</param>
    /// <returns>Unrestricted for admin, auditor and reporter-viewall, otherwise the claimed visibility.</returns>
    public static RuleLastHitScope FromUser(ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Auditor) || user.IsInRole(Roles.ReporterViewAll))
        {
            return kUnrestricted;
        }

        HashSet<int> visibleManagementIds = [.. JwtClaimParser.ExtractIntClaimValues(user.Claims, kVisibleManagementsClaim)];
        HashSet<int> visibleDeviceIds = [.. JwtClaimParser.ExtractIntClaimValues(user.Claims, kVisibleDevicesClaim)];
        bool isTenant0 = JwtClaimParser.ExtractIntClaimValues(user.Claims, kTenantIdClaim).Contains(GlobalConst.kTenant0Id);
        return new RuleLastHitScope(visibleManagementIds, visibleDeviceIds, isTenant0);
    }

    /// <summary>
    /// Tells whether the caller may see the management.
    /// </summary>
    /// <param name="managementId">Database id of the management.</param>
    public bool SeesManagement(int managementId)
    {
        return VisibleManagementIds == null || VisibleManagementIds.Contains(managementId);
    }
}

/// <summary>
/// One rule metadata row as returned by the rule last hit query.
/// </summary>
public sealed class RuleLastHitRecord
{
    /// <summary>
    /// Gets or sets the database id of the rule metadata row.
    /// </summary>
    [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the database id of the management.
    /// </summary>
    [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
    public int MgmId { get; set; }

    /// <summary>
    /// Gets or sets the UID of the rule.
    /// </summary>
    [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
    public string? RuleUid { get; set; }

    /// <summary>
    /// Gets or sets the latest hit across all gateways; null when the rule was never hit.
    /// </summary>
    [JsonProperty("rule_last_hit"), JsonPropertyName("rule_last_hit")]
    public DateTime? LastHit { get; set; }

    /// <summary>
    /// Gets or sets the current version of the rule, at most one.
    /// </summary>
    [JsonProperty("rules"), JsonPropertyName("rules")]
    public List<RuleLastHitRuleRecord> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the hits per gateway.
    /// </summary>
    [JsonProperty("rule_hits"), JsonPropertyName("rule_hits")]
    public List<RuleGatewayHitRecord> Hits { get; set; } = [];
}

/// <summary>
/// The name of a rule version as returned by the rule last hit query.
/// </summary>
public sealed class RuleLastHitRuleRecord
{
    /// <summary>
    /// Gets or sets the rule name.
    /// </summary>
    [JsonProperty("rule_name"), JsonPropertyName("rule_name")]
    public string? Name { get; set; }
}

/// <summary>
/// One gateway hit as returned by the rule last hit query.
/// </summary>
public sealed class RuleGatewayHitRecord
{
    /// <summary>
    /// Gets or sets the database id of the gateway.
    /// </summary>
    [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
    public int DevId { get; set; }

    /// <summary>
    /// Gets or sets the last hit on the gateway.
    /// </summary>
    [JsonProperty("rule_last_hit"), JsonPropertyName("rule_last_hit")]
    public DateTime LastHit { get; set; }

    /// <summary>
    /// Gets or sets the gateway.
    /// </summary>
    [JsonProperty("device"), JsonPropertyName("device")]
    public RuleHitGatewayRecord? Device { get; set; }
}

/// <summary>
/// The name of a gateway as returned by the rule last hit query.
/// </summary>
public sealed class RuleHitGatewayRecord
{
    /// <summary>
    /// Gets or sets the gateway name.
    /// </summary>
    [JsonProperty("dev_name"), JsonPropertyName("dev_name")]
    public string? Name { get; set; }
}

/// <summary>
/// Reads the last hits of the rules of a management for the REST API.
/// </summary>
/// <remarks>
/// Runs with the shared internal API connection as middleware-server. Filters, order and paging are applied by
/// the GraphQL query; the caller's visibility is part of the query variables.
/// </remarks>
public sealed class RuleLastHitService
{
    private const string kTimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffff";
    private const string kLastHitColumn = "rule_last_hit";
    private const string kRemovedColumn = "removed";
    private const string kDeviceIdColumn = "dev_id";
    private const string kIsNullOperator = "_is_null";

    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    /// <param name="apiConnection">Shared internal API connection, which runs as middleware-server.</param>
    public RuleLastHitService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Returns one page of the rules of a management with their last hits, ordered by rule metadata id.
    /// </summary>
    /// <param name="managementId">Database id of the management.</param>
    /// <param name="options">Filter and paging options of the request, already validated.</param>
    /// <param name="pageSize">Page size, the requested limit or the configured maximum.</param>
    /// <param name="scope">What the caller may see.</param>
    /// <returns>The page; empty without a query when the caller's visibility excludes every rule.</returns>
    public async Task<ListPage<RuleLastHitResponse>> GetRuleLastHitsAsync(int managementId, GetRuleLastHitsOptions options,
        int pageSize, RuleLastHitScope scope)
    {
        if (!scope.SeesManagement(managementId) || AsksForHiddenRuleNames(options.Filter, scope))
        {
            List<RuleLastHitResponse> noRules = new();
            return new ListPage<RuleLastHitResponse>(noRules, false);
        }

        Dictionary<string, object> variables = BuildVariables(managementId, options, pageSize, scope);
        List<RuleLastHitRecord> records = await apiConnection.SendQueryAsync<List<RuleLastHitRecord>>(RuleQueries.getRuleLastHits, variables) ?? [];
        ListPage<RuleLastHitRecord> page = ListPage<RuleLastHitRecord>.FromLookahead(records, pageSize);
        List<RuleLastHitResponse> rules = [.. page.Items.Select(record => Map(record, scope))];
        return new ListPage<RuleLastHitResponse>(rules, page.HasMore);
    }

    /// <summary>
    /// Builds the GraphQL variables of the rule last hit query: the rule filter, the gateway filter and the
    /// lookahead paging.
    /// </summary>
    /// <param name="managementId">Database id of the management.</param>
    /// <param name="options">Filter and paging options of the request.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="scope">What the caller may see.</param>
    public static Dictionary<string, object> BuildVariables(int managementId, GetRuleLastHitsOptions options, int pageSize, RuleLastHitScope scope)
    {
        RuleLastHitFilter? filter = options.Filter;
        Dictionary<string, object> hitWhere = BuildHitWhere(filter, scope);
        Dictionary<string, object> variables = new()
        {
            ["where"] = BuildRuleWhere(managementId, filter, hitWhere),
            ["hitWhere"] = hitWhere,
            ["gatewayLimit"] = RuleLastHitResponse.kMaxGatewaysPerRule + 1
        };
        ListPaging.AddLookaheadPagingVariables(variables, pageSize, options.Offset);
        return variables;
    }

    private static bool AsksForHiddenRuleNames(RuleLastHitFilter? filter, RuleLastHitScope scope)
    {
        return !scope.RuleNamesVisible && !string.IsNullOrWhiteSpace(filter?.RuleName);
    }

    private static Dictionary<string, object> BuildHitWhere(RuleLastHitFilter? filter, RuleLastHitScope scope)
    {
        List<Dictionary<string, object>> predicates = [];
        if (scope.VisibleDeviceIds != null)
        {
            List<int> visibleDeviceIds = [.. scope.VisibleDeviceIds.Order()];
            GraphQlFilterBuilder.AddInPredicate(predicates, kDeviceIdColumn, visibleDeviceIds);
        }
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, kDeviceIdColumn, filter?.DevId);
        List<Dictionary<string, object>> devicePredicates = [];
        GraphQlFilterBuilder.AddWildcardPredicate(devicePredicates, "dev_name", filter?.GatewayName);
        if (devicePredicates.Count > 0)
        {
            predicates.Add(new Dictionary<string, object> { ["device"] = devicePredicates[0] });
        }
        return GraphQlFilterBuilder.CombinePredicates(predicates);
    }

    private static Dictionary<string, object> BuildRuleWhere(int managementId, RuleLastHitFilter? filter, Dictionary<string, object> hitWhere)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "mgm_id", managementId);
        predicates.Add(BuildComparison(kRemovedColumn, kIsNullOperator, true));
        if (filter == null)
        {
            return GraphQlFilterBuilder.CombinePredicates(predicates);
        }

        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "mgm_id", filter.MgmId);
        if (filter.RuleUid != null)
        {
            List<string> ruleUids = filter.RuleUid.Distinct().ToList();
            GraphQlFilterBuilder.AddInPredicate(predicates, "rule_uid", ruleUids);
        }
        AddRuleNamePredicate(predicates, filter.RuleName);
        AddLastHitPredicates(predicates, filter);
        if (filter.DevId != null || !string.IsNullOrWhiteSpace(filter.GatewayName))
        {
            predicates.Add(new Dictionary<string, object> { ["rule_hits"] = hitWhere });
        }
        return GraphQlFilterBuilder.CombinePredicates(predicates);
    }

    private static void AddRuleNamePredicate(List<Dictionary<string, object>> predicates, string? ruleName)
    {
        List<Dictionary<string, object>> rulePredicates = [];
        GraphQlFilterBuilder.AddWildcardPredicate(rulePredicates, "rule_name", ruleName);
        if (rulePredicates.Count > 0)
        {
            rulePredicates.Add(BuildComparison(kRemovedColumn, kIsNullOperator, true));
            predicates.Add(new Dictionary<string, object> { ["rules"] = GraphQlFilterBuilder.CombinePredicates(rulePredicates) });
        }
    }

    private static void AddLastHitPredicates(List<Dictionary<string, object>> predicates, RuleLastHitFilter filter)
    {
        if (filter.LastHitAfter is DateTimeOffset lastHitAfter)
        {
            predicates.Add(BuildComparison(kLastHitColumn, "_gte", FormatTimestamp(lastHitAfter)));
        }
        if (filter.LastHitBefore is DateTimeOffset lastHitBefore)
        {
            predicates.Add(BuildComparison(kLastHitColumn, "_lt", FormatTimestamp(lastHitBefore)));
        }
        if (filter.NeverHit is bool neverHit)
        {
            predicates.Add(BuildComparison(kLastHitColumn, kIsNullOperator, neverHit));
        }
    }

    private static Dictionary<string, object> BuildComparison(string fieldName, string comparisonOperator, object value)
    {
        return new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { [comparisonOperator] = value } };
    }

    /// <summary>
    /// Formats a bound for rule_metadata.rule_last_hit, a timestamp without time zone holding UTC.
    /// </summary>
    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString(kTimestampFormat, CultureInfo.InvariantCulture);
    }

    private static RuleLastHitResponse Map(RuleLastHitRecord record, RuleLastHitScope scope)
    {
        string ruleName = scope.RuleNamesVisible ? record.Rules.FirstOrDefault()?.Name ?? string.Empty : string.Empty;
        return new RuleLastHitResponse
        {
            MgmId = record.MgmId,
            RuleUid = record.RuleUid ?? string.Empty,
            RuleName = ruleName,
            LastHit = record.LastHit is DateTime lastHit ? ToUtc(lastHit) : null,
            Gateways = [.. record.Hits.Take(RuleLastHitResponse.kMaxGatewaysPerRule).Select(MapGateway)],
            GatewaysTruncated = record.Hits.Count > RuleLastHitResponse.kMaxGatewaysPerRule
        };
    }

    private static RuleGatewayLastHitResponse MapGateway(RuleGatewayHitRecord hit)
    {
        return new RuleGatewayLastHitResponse
        {
            DevId = hit.DevId,
            GatewayName = hit.Device?.Name ?? string.Empty,
            LastHit = ToUtc(hit.LastHit)
        };
    }

    /// <summary>
    /// Normalizes a database timestamp to UTC; a value without time zone is stored as UTC.
    /// </summary>
    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
    }
}

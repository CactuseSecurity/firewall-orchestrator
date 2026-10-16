using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.RulePositioning;
using Newtonsoft.Json;

namespace FWO.Services.RulePositioning;

public sealed class RulebaseGraphSnapshotReader : IRulebaseGraphSnapshotReader
{
    private const string kImportOperation = "getLatestSuccessfulRuleImport";
    private const string kLinksOperation = "getGatewayRulebaseLinksAtImport";
    private const string kRulebasesOperation = "getRulebasesAtImport";

    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Creates a reader backed by the normalized-data API connection.
    /// </summary>
    public RulebaseGraphSnapshotReader(ApiConnection apiConnection)
    {
        ArgumentNullException.ThrowIfNull(apiConnection);
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Loads one historically consistent structural rulebase graph snapshot.
    /// </summary>
    public async Task<RulebaseGraphReadResult> ReadAsync(
        int managementId,
        int gatewayId,
        CancellationToken cancellationToken)
    {
        if (managementId <= 0 || gatewayId <= 0)
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidInput);
        }

        QueryResult<List<ImportWire>> importResult = await QueryAsync<List<ImportWire>>(
            RulePositioningQueries.getLatestSuccessfulRuleImport,
            new { managementId, importTypeId = ImportType.RULE },
            kImportOperation,
            cancellationToken);
        if (!importResult.Succeeded)
        {
            return Failure(RulebaseGraphReadFailureCategory.QueryFailed);
        }

        List<ImportWire>? imports = importResult.Value;
        if (imports == null)
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }
        if (imports.Count == 0)
        {
            return Failure(RulebaseGraphReadFailureCategory.NoSuccessfulImport);
        }
        if (imports.Count != 1 || imports[0] == null || !IsValidImport(imports[0], managementId))
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }
        long importId = imports[0].ControlId!.Value;

        QueryResult<List<GatewayWire>> gatewayResult = await QueryAsync<List<GatewayWire>>(
            RulePositioningQueries.getGatewayRulebaseLinksAtImport,
            new { gatewayId, importId },
            kLinksOperation,
            cancellationToken);
        if (!gatewayResult.Succeeded)
        {
            return Failure(RulebaseGraphReadFailureCategory.QueryFailed);
        }

        List<GatewayWire>? gateways = gatewayResult.Value;
        if (gateways == null || gateways.Count != 1 || gateways[0] == null ||
            gateways[0].DeviceId != gatewayId || gateways[0].ManagementId != managementId)
        {
            return Failure(RulebaseGraphReadFailureCategory.ScopeMismatch);
        }

        GatewayWire gateway = gateways[0];
        if (!TryValidateLinks(gateway.Links, managementId, gatewayId, importId, out List<int> rulebaseIds))
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }

        QueryResult<List<RulebaseWire>> rulebaseResult = await QueryAsync<List<RulebaseWire>>(
            RulePositioningQueries.getRulebasesAtImport,
            new { managementId, rulebaseIds, importId },
            kRulebasesOperation,
            cancellationToken);
        if (!rulebaseResult.Succeeded)
        {
            return Failure(RulebaseGraphReadFailureCategory.QueryFailed);
        }

        if (!TryMapGraph(
                rulebaseResult.Value,
                gateway.Links!,
                rulebaseIds,
                managementId,
                gatewayId,
                importId,
                out List<Rulebase> rulebases,
                out List<RulebaseLink> links))
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }

        try
        {
            return new RulebaseGraphReadSuccess(
                new RulebaseGraphSnapshot(managementId, gatewayId, rulebases, links));
        }
        catch (ArgumentException)
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }
        catch (InvalidOperationException)
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }
    }

    /// <summary>
    /// Executes one query while preserving caller cancellation and hiding query failure details.
    /// </summary>
    private async Task<QueryResult<T>> QueryAsync<T>(
        string query,
        object variables,
        string operationName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            T value = await apiConnection.SendQueryAsync<T>(query, variables, operationName)
                .WaitAsync(cancellationToken);
            return new QueryResult<T>(true, value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new QueryResult<T>(false, default);
        }
    }

    /// <summary>
    /// Validates the selected import row against every selection predicate.
    /// </summary>
    private static bool IsValidImport(ImportWire import, int managementId) =>
        import.ControlId is > 0 &&
        import.ManagementId == managementId &&
        import.ImportTypeId == ImportType.RULE &&
        import.Successful == true &&
        import.StopTime.HasValue;

    /// <summary>
    /// Validates gateway links and derives the complete sorted endpoint set.
    /// </summary>
    private static bool TryValidateLinks(
        List<LinkWire>? links,
        int managementId,
        int gatewayId,
        long importId,
        out List<int> rulebaseIds)
    {
        rulebaseIds = [];
        if (links == null || links.Count == 0 || links.Any(link => link == null))
        {
            return false;
        }

        HashSet<long> linkIds = [];
        HashSet<int> endpoints = [];
        foreach (LinkWire link in links)
        {
            if (link.Id is not > 0 || !linkIds.Add(link.Id.Value) ||
                link.GatewayId != gatewayId || link.TargetRulebaseId is not > 0 ||
                !link.LinkType.HasValue || !link.IsInitial.HasValue ||
                !link.IsGlobal.HasValue || !link.IsSection.HasValue ||
                !IsHistoricallyValid(link.Created, link.Removed, importId))
            {
                return false;
            }
            endpoints.Add(link.TargetRulebaseId.Value);

            if (link.SourceRulebaseId.HasValue)
            {
                if (link.SourceRulebaseId <= 0)
                {
                    return false;
                }
                endpoints.Add(link.SourceRulebaseId.Value);
            }

            if (!TryValidateSourceRule(link, managementId, importId, endpoints))
            {
                return false;
            }
        }

        rulebaseIds = endpoints.Order().ToList();
        return rulebaseIds.Count > 0;
    }

    /// <summary>
    /// Validates a link's optional source-rule summary and contributes its owner endpoint.
    /// </summary>
    private static bool TryValidateSourceRule(
        LinkWire link,
        int managementId,
        long importId,
        HashSet<int> endpoints)
    {
        if (!link.SourceRuleId.HasValue)
        {
            return link.SourceRule == null;
        }

        SourceRuleWire? rule = link.SourceRule;
        if (link.SourceRuleId <= 0 || rule?.Id != link.SourceRuleId ||
            rule.ManagementId != managementId || rule.RulebaseId is not > 0 ||
            !IsHistoricallyValid(rule.Created, rule.Removed, importId) ||
            link.SourceRulebaseId.HasValue && link.SourceRulebaseId != rule.RulebaseId)
        {
            return false;
        }

        endpoints.Add(rule.RulebaseId.Value);
        return true;
    }

    /// <summary>
    /// Validates and maps the complete response without retaining wire collections.
    /// </summary>
    private static bool TryMapGraph(
        List<RulebaseWire>? wireRulebases,
        List<LinkWire> wireLinks,
        IReadOnlyCollection<int> requestedIds,
        int managementId,
        int gatewayId,
        long importId,
        out List<Rulebase> rulebases,
        out List<RulebaseLink> links)
    {
        rulebases = [];
        links = [];
        if (wireRulebases == null || wireRulebases.Count != requestedIds.Count ||
            wireRulebases.Any(rulebase => rulebase == null))
        {
            return false;
        }

        HashSet<int> returnedIds = [];
        HashSet<long> returnedRuleIds = [];
        foreach (RulebaseWire wireRulebase in wireRulebases)
        {
            if (wireRulebase.Id is not > 0 || !returnedIds.Add(wireRulebase.Id.Value) ||
                !requestedIds.Contains(wireRulebase.Id.Value) || wireRulebase.ManagementId != managementId ||
                !IsHistoricallyValid(wireRulebase.Created, wireRulebase.Removed, importId) ||
                wireRulebase.Rules == null ||
                !TryMapRules(wireRulebase, managementId, importId, returnedRuleIds, out List<Rule> rules))
            {
                return false;
            }

            rulebases.Add(new Rulebase
            {
                Id = wireRulebase.Id.Value,
                Name = wireRulebase.Name ?? "",
                Uid = wireRulebase.Uid ?? "",
                MgmtId = managementId,
                Rules = [.. rules]
            });
        }

        if (!returnedIds.SetEquals(requestedIds) ||
            wireLinks.Any(link => link.SourceRuleId.HasValue && !returnedRuleIds.Contains(link.SourceRuleId.Value)))
        {
            return false;
        }

        links = wireLinks.Select(link => new RulebaseLink
        {
            GatewayId = gatewayId,
            FromRuleId = link.SourceRuleId,
            FromRulebaseId = link.SourceRulebaseId,
            NextRulebaseId = link.TargetRulebaseId!.Value,
            LinkType = link.LinkType ?? 0,
            IsInitial = link.IsInitial ?? false,
            IsGlobal = link.IsGlobal ?? false,
            IsSection = link.IsSection ?? false
        }).ToList();
        return true;
    }

    /// <summary>
    /// Validates and maps all structural rules belonging to one rulebase.
    /// </summary>
    private static bool TryMapRules(
        RulebaseWire owner,
        int managementId,
        long importId,
        HashSet<long> returnedRuleIds,
        out List<Rule> rules)
    {
        rules = [];
        if (owner.Rules!.Any(rule => rule == null))
        {
            return false;
        }
        foreach (RuleWire wireRule in owner.Rules!)
        {
            if (wireRule.Id is not > 0 || !returnedRuleIds.Add(wireRule.Id.Value) ||
                wireRule.ManagementId != managementId || wireRule.RulebaseId != owner.Id ||
                !wireRule.NativeOrder.HasValue ||
                !IsHistoricallyValid(wireRule.Created, wireRule.Removed, importId))
            {
                return false;
            }

            rules.Add(new Rule
            {
                Id = wireRule.Id.Value,
                Uid = wireRule.Uid,
                MgmtId = managementId,
                RulebaseId = wireRule.RulebaseId!.Value,
                RuleNumNumeric = wireRule.NativeOrder.Value
            });
        }
        return true;
    }

    /// <summary>
    /// Applies the fixed-import half-open historical interval.
    /// </summary>
    private static bool IsHistoricallyValid(long? created, long? removed, long importId) =>
        created is > 0 && created <= importId && (!removed.HasValue || removed > importId);

    /// <summary>
    /// Creates one category-only reader failure.
    /// </summary>
    private static RulebaseGraphReadFailure Failure(RulebaseGraphReadFailureCategory category) => new(category);

    private sealed record QueryResult<T>(bool Succeeded, T? Value);

    private sealed class ImportWire
    {
        [JsonProperty("control_id")]
        public long? ControlId { get; set; }

        [JsonProperty("mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("import_type_id")]
        public int? ImportTypeId { get; set; }

        [JsonProperty("successful_import")]
        public bool? Successful { get; set; }

        [JsonProperty("stop_time")]
        public DateTime? StopTime { get; set; }
    }

    private sealed class GatewayWire
    {
        [JsonProperty("dev_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("rulebase_links")]
        public List<LinkWire>? Links { get; set; }
    }

    private sealed class LinkWire
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("gw_id")]
        public int? GatewayId { get; set; }

        [JsonProperty("from_rule_id")]
        public int? SourceRuleId { get; set; }

        [JsonProperty("from_rulebase_id")]
        public int? SourceRulebaseId { get; set; }

        [JsonProperty("to_rulebase_id")]
        public int? TargetRulebaseId { get; set; }

        [JsonProperty("link_type")]
        public int? LinkType { get; set; }

        [JsonProperty("is_initial")]
        public bool? IsInitial { get; set; }

        [JsonProperty("is_global")]
        public bool? IsGlobal { get; set; }

        [JsonProperty("is_section")]
        public bool? IsSection { get; set; }

        [JsonProperty("created")]
        public long? Created { get; set; }

        [JsonProperty("removed")]
        public long? Removed { get; set; }

        [JsonProperty("rule")]
        public SourceRuleWire? SourceRule { get; set; }
    }

    private sealed class SourceRuleWire
    {
        [JsonProperty("rule_id")]
        public int? Id { get; set; }

        [JsonProperty("mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("rulebase_id")]
        public int? RulebaseId { get; set; }

        [JsonProperty("rule_create")]
        public long? Created { get; set; }

        [JsonProperty("removed")]
        public long? Removed { get; set; }
    }

    private sealed class RulebaseWire
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("uid")]
        public string? Uid { get; set; }

        [JsonProperty("mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("created")]
        public long? Created { get; set; }

        [JsonProperty("removed")]
        public long? Removed { get; set; }

        [JsonProperty("rules")]
        public List<RuleWire>? Rules { get; set; }
    }

    private sealed class RuleWire
    {
        [JsonProperty("rule_id")]
        public long? Id { get; set; }

        [JsonProperty("rule_uid")]
        public string? Uid { get; set; }

        [JsonProperty("mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("rulebase_id")]
        public int? RulebaseId { get; set; }

        [JsonProperty("rule_num_numeric")]
        public double? NativeOrder { get; set; }

        [JsonProperty("rule_create")]
        public long? Created { get; set; }

        [JsonProperty("removed")]
        public long? Removed { get; set; }
    }
}

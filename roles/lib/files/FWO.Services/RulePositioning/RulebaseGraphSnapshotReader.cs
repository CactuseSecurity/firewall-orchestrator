using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.RulePositioning;
using Newtonsoft.Json;
using RulebaseGraphReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.RulebaseGraphReadValue,
    FWO.Services.RulePositioning.RulebaseGraphReadFailureCategory>;

namespace FWO.Services.RulePositioning;

/// <summary>Reads a bounded, historically consistent normalized rulebase graph.</summary>
public sealed class RulebaseGraphSnapshotReader : IRulebaseGraphSnapshotReader
{
    private const int kMaximumRulebaseLinks = 10000;
    private const int kMaximumRulebases = 5000;
    private const int kMaximumRulesPerRulebase = 100000;
    private const int kMaximumRulesPerGraph = 250000;
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
        return await RulePositioningGraphCacheScope.ReadAsync(
            managementId,
            gatewayId,
            () => ReadUncachedAsync(managementId, gatewayId, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Loads one graph without consulting the operation-scoped cache.
    /// </summary>
    private async Task<RulebaseGraphReadResult> ReadUncachedAsync(
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
        if (!TryGetImportId(importResult, managementId, out long importId,
                out RulebaseGraphReadFailureCategory importFailure))
        {
            return Failure(importFailure);
        }

        QueryResult<List<GatewayWire>> gatewayResult = await QueryAsync<List<GatewayWire>>(
            RulePositioningQueries.getGatewayRulebaseLinksAtImport,
            new { gatewayId, importId, linkLimit = kMaximumRulebaseLinks + 1 },
            kLinksOperation,
            cancellationToken);
        if (!TryGetGateway(gatewayResult, managementId, gatewayId, out GatewayWire gateway,
                out RulebaseGraphReadFailureCategory gatewayFailure))
        {
            return Failure(gatewayFailure);
        }

        if (!TryValidateLinks(gateway.Links, managementId, gatewayId, importId, out List<int> rulebaseIds))
        {
            return Failure(RulebaseGraphReadFailureCategory.InvalidGraphData);
        }

        QueryResult<List<RulebaseWire>> rulebaseResult = await QueryAsync<List<RulebaseWire>>(
            RulePositioningQueries.getRulebasesAtImport,
            new
            {
                managementId,
                rulebaseIds,
                importId,
                rulebaseLimit = kMaximumRulebases + 1,
                ruleLimit = kMaximumRulesPerRulebase + 1
            },
            kRulebasesOperation,
            cancellationToken);
        if (!TryCreateReadValue(
                rulebaseResult,
                gateway,
                new GraphMappingContext(rulebaseIds, managementId, gatewayId, importId),
                out RulebaseGraphReadValue? readValue,
                out RulebaseGraphReadFailureCategory graphFailure))
        {
            return Failure(graphFailure);
        }

        return RulebaseGraphReadResult.Success(readValue!);
    }

    /// <summary>Validates the import query result and returns its single import identifier.</summary>
    private static bool TryGetImportId(
        QueryResult<List<ImportWire>> importResult,
        int managementId,
        out long importId,
        out RulebaseGraphReadFailureCategory failure)
    {
        importId = 0;
        failure = RulebaseGraphReadFailureCategory.InvalidGraphData;
        if (!importResult.Succeeded)
        {
            failure = RulebaseGraphReadFailureCategory.QueryFailed;
            return false;
        }

        List<ImportWire>? imports = importResult.Value;
        if (imports == null)
        {
            return false;
        }
        if (imports.Count == 0)
        {
            failure = RulebaseGraphReadFailureCategory.NoSuccessfulImport;
            return false;
        }
        if (imports.Count != 1 || imports[0] == null || !IsValidImport(imports[0], managementId))
        {
            return false;
        }

        importId = imports[0].ControlId!.Value;
        return true;
    }

    /// <summary>Validates the gateway query result and its requested scope.</summary>
    private static bool TryGetGateway(
        QueryResult<List<GatewayWire>> gatewayResult,
        int managementId,
        int gatewayId,
        out GatewayWire gateway,
        out RulebaseGraphReadFailureCategory failure)
    {
        gateway = null!;
        failure = RulebaseGraphReadFailureCategory.ScopeMismatch;
        if (!gatewayResult.Succeeded)
        {
            failure = RulebaseGraphReadFailureCategory.QueryFailed;
            return false;
        }

        List<GatewayWire>? gateways = gatewayResult.Value;
        if (gateways == null || gateways.Count != 1 || gateways[0] == null ||
            gateways[0].DeviceId != gatewayId || gateways[0].ManagementId != managementId)
        {
            return false;
        }

        gateway = gateways[0];
        return true;
    }

    /// <summary>Maps a validated graph query result into its immutable read value.</summary>
    private static bool TryCreateReadValue(
        QueryResult<List<RulebaseWire>> rulebaseResult,
        GatewayWire gateway,
        GraphMappingContext context,
        out RulebaseGraphReadValue? readValue,
        out RulebaseGraphReadFailureCategory failure)
    {
        readValue = null;
        failure = RulebaseGraphReadFailureCategory.InvalidGraphData;
        if (!rulebaseResult.Succeeded)
        {
            failure = RulebaseGraphReadFailureCategory.QueryFailed;
            return false;
        }
        if (!TryMapGraph(
                rulebaseResult.Value,
                gateway.Links!,
                context,
                out List<Rulebase> rulebases,
                out List<RulebaseLink> links))
        {
            return false;
        }

        try
        {
            readValue = new RulebaseGraphReadValue(
                new RulebaseGraphSnapshot(context.ManagementId, context.GatewayId, rulebases, links),
                context.ImportId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
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
        if (!IsValidLinkCollection(links))
        {
            return false;
        }

        HashSet<long> linkIds = [];
        HashSet<int> endpoints = [];
        foreach (LinkWire link in links!)
        {
            if (!TryCollectLinkEndpoints(
                    link, linkIds, endpoints, managementId, gatewayId, importId))
            {
                return false;
            }
        }

        rulebaseIds = endpoints.Order().ToList();
        return rulebaseIds.Count is > 0 and <= kMaximumRulebases;
    }

    /// <summary>Checks collection presence and the configured graph-link limit.</summary>
    private static bool IsValidLinkCollection(List<LinkWire>? links) =>
        links is { Count: > 0 and <= kMaximumRulebaseLinks } && links.All(link => link != null);

    /// <summary>Validates one link and contributes all referenced rulebase endpoints.</summary>
    private static bool TryCollectLinkEndpoints(
        LinkWire link,
        HashSet<long> linkIds,
        HashSet<int> endpoints,
        int managementId,
        int gatewayId,
        long importId)
    {
        if (!IsValidLink(link, linkIds, gatewayId, importId))
        {
            return false;
        }
        endpoints.Add(link.TargetRulebaseId!.Value);
        if (!TryAddSourceRulebaseEndpoint(link, endpoints))
        {
            return false;
        }
        return TryValidateSourceRule(link, managementId, importId, endpoints);
    }

    /// <summary>Checks required link values, uniqueness, scope, and historical validity.</summary>
    private static bool IsValidLink(
        LinkWire link,
        HashSet<long> linkIds,
        int gatewayId,
        long importId) =>
        link.Id is > 0 && linkIds.Add(link.Id.Value) &&
        link.GatewayId == gatewayId && link.TargetRulebaseId is > 0 &&
        link.LinkType.HasValue && link.IsInitial.HasValue &&
        link.IsGlobal.HasValue && link.IsSection.HasValue &&
        IsHistoricallyValid(link.Created, link.Removed, importId);

    /// <summary>Adds the optional source-rulebase endpoint after validating its identifier.</summary>
    private static bool TryAddSourceRulebaseEndpoint(LinkWire link, HashSet<int> endpoints)
    {
        if (!link.SourceRulebaseId.HasValue)
        {
            return true;
        }
        if (link.SourceRulebaseId <= 0)
        {
            return false;
        }
        endpoints.Add(link.SourceRulebaseId.Value);
        return true;
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
        GraphMappingContext context,
        out List<Rulebase> rulebases,
        out List<RulebaseLink> links)
    {
        rulebases = [];
        links = [];
        if (!IsValidRulebaseResponseShape(wireRulebases, context.RequestedIds.Count))
        {
            return false;
        }

        HashSet<int> returnedIds = [];
        HashSet<long> returnedRuleIds = [];
        foreach (RulebaseWire wireRulebase in wireRulebases!)
        {
            if (!TryMapRulebase(wireRulebase, context, returnedIds, returnedRuleIds, out Rulebase? rulebase))
            {
                return false;
            }
            rulebases.Add(rulebase!);
        }

        if (!HasCompleteGraph(context.RequestedIds, returnedIds, returnedRuleIds, wireLinks))
        {
            return false;
        }

        links = MapLinks(wireLinks, context.GatewayId);
        return true;
    }

    /// <summary>Checks response counts and configured graph-size limits before mapping.</summary>
    private static bool IsValidRulebaseResponseShape(List<RulebaseWire>? wireRulebases, int requestedCount)
    {
        return wireRulebases != null && wireRulebases.Count == requestedCount &&
            wireRulebases.Count <= kMaximumRulebases &&
            wireRulebases.All(rulebase => rulebase != null &&
                rulebase.Rules?.Count <= kMaximumRulesPerRulebase) &&
            wireRulebases.Sum(rulebase => (long?)rulebase.Rules?.Count ?? 0) <= kMaximumRulesPerGraph;
    }

    /// <summary>Validates and maps one fixed-import rulebase.</summary>
    private static bool TryMapRulebase(
        RulebaseWire wireRulebase,
        GraphMappingContext context,
        HashSet<int> returnedIds,
        HashSet<long> returnedRuleIds,
        out Rulebase? rulebase)
    {
        rulebase = null;
        if (wireRulebase.Id is not > 0 || !returnedIds.Add(wireRulebase.Id.Value) ||
            !context.RequestedIds.Contains(wireRulebase.Id.Value) ||
            wireRulebase.ManagementId != context.ManagementId ||
            !IsHistoricallyValid(wireRulebase.Created, wireRulebase.Removed, context.ImportId) ||
            wireRulebase.Rules == null ||
            !TryMapRules(wireRulebase, context.ManagementId, context.ImportId, returnedRuleIds, out List<Rule> rules))
        {
            return false;
        }

        rulebase = new Rulebase
        {
            Id = wireRulebase.Id.Value,
            Name = wireRulebase.Name ?? "",
            Uid = wireRulebase.Uid ?? "",
            MgmtId = context.ManagementId,
            Rules = [.. rules]
        };
        return true;
    }

    /// <summary>Checks that all requested nodes and link source rules were returned.</summary>
    private static bool HasCompleteGraph(
        List<int> requestedIds,
        HashSet<int> returnedIds,
        HashSet<long> returnedRuleIds,
        List<LinkWire> wireLinks) =>
        returnedIds.SetEquals(requestedIds) &&
        wireLinks.All(link => !link.SourceRuleId.HasValue || returnedRuleIds.Contains(link.SourceRuleId.Value));

    /// <summary>Copies validated wire links into graph links.</summary>
    private static List<RulebaseLink> MapLinks(List<LinkWire> wireLinks, int gatewayId) =>
        wireLinks.Select(link => new RulebaseLink
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

    /// <summary>Values shared while mapping one fixed-import graph response.</summary>
    private sealed record GraphMappingContext(
        List<int> RequestedIds,
        int ManagementId,
        int GatewayId,
        long ImportId);

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
    private static RulebaseGraphReadResult Failure(RulebaseGraphReadFailureCategory category) =>
        RulebaseGraphReadResult.Failure(category);

    /// <summary>Captures a query value without exposing transport failure details.</summary>
    private sealed record QueryResult<T>(bool Succeeded, T? Value);

    /// <summary>GraphQL shape for one import-control row.</summary>
    private sealed class ImportWire
    {
        [JsonProperty("control_id")]
        /// <summary>Gets or sets the import-control identifier.</summary>
        public long? ControlId { get; set; }

        [JsonProperty("mgm_id")]
        /// <summary>Gets or sets the owning management identifier.</summary>
        public int? ManagementId { get; set; }

        [JsonProperty("import_type_id")]
        /// <summary>Gets or sets the import type identifier.</summary>
        public int? ImportTypeId { get; set; }

        [JsonProperty("successful_import")]
        /// <summary>Gets or sets whether the import completed successfully.</summary>
        public bool? Successful { get; set; }

        [JsonProperty("stop_time")]
        /// <summary>Gets or sets the import completion time.</summary>
        public DateTime? StopTime { get; set; }
    }

    /// <summary>GraphQL shape for a gateway and its rulebase links.</summary>
    private sealed class GatewayWire
    {
        [JsonProperty("dev_id")]
        /// <summary>Gets or sets the gateway identifier.</summary>
        public int? DeviceId { get; set; }

        [JsonProperty("mgm_id")]
        /// <summary>Gets or sets the owning management identifier.</summary>
        public int? ManagementId { get; set; }

        [JsonProperty("rulebase_links")]
        /// <summary>Gets or sets rulebase links visible at the selected import.</summary>
        public List<LinkWire>? Links { get; set; }
    }

    /// <summary>GraphQL shape for one normalized rulebase link.</summary>
    private sealed class LinkWire
    {
        [JsonProperty("id")]
        /// <summary>Gets or sets the link identifier.</summary>
        public long? Id { get; set; }

        [JsonProperty("gw_id")]
        /// <summary>Gets or sets the gateway identifier.</summary>
        public int? GatewayId { get; set; }

        [JsonProperty("from_rule_id")]
        /// <summary>Gets or sets the optional source rule identifier.</summary>
        public long? SourceRuleId { get; set; }

        [JsonProperty("from_rulebase_id")]
        /// <summary>Gets or sets the optional source rulebase identifier.</summary>
        public int? SourceRulebaseId { get; set; }

        [JsonProperty("to_rulebase_id")]
        /// <summary>Gets or sets the target rulebase identifier.</summary>
        public int? TargetRulebaseId { get; set; }

        [JsonProperty("link_type")]
        /// <summary>Gets or sets the normalized link type.</summary>
        public int? LinkType { get; set; }

        [JsonProperty("is_initial")]
        /// <summary>Gets or sets whether this selects the initial layer.</summary>
        public bool? IsInitial { get; set; }

        [JsonProperty("is_global")]
        /// <summary>Gets or sets whether this crosses into a global layer.</summary>
        public bool? IsGlobal { get; set; }

        [JsonProperty("is_section")]
        /// <summary>Gets or sets whether the target is a section.</summary>
        public bool? IsSection { get; set; }

        [JsonProperty("created")]
        /// <summary>Gets or sets the first import containing this link.</summary>
        public long? Created { get; set; }

        [JsonProperty("removed")]
        /// <summary>Gets or sets the first import no longer containing this link.</summary>
        public long? Removed { get; set; }

        [JsonProperty("rule")]
        /// <summary>Gets or sets the optional source-rule summary.</summary>
        public SourceRuleWire? SourceRule { get; set; }
    }

    /// <summary>GraphQL shape for the source rule referenced by a link.</summary>
    private sealed class SourceRuleWire
    {
        [JsonProperty("rule_id")]
        /// <summary>Gets or sets the rule identifier.</summary>
        public long? Id { get; set; }

        [JsonProperty("mgm_id")]
        /// <summary>Gets or sets the owning management identifier.</summary>
        public int? ManagementId { get; set; }

        [JsonProperty("rulebase_id")]
        /// <summary>Gets or sets the containing rulebase identifier.</summary>
        public int? RulebaseId { get; set; }

        [JsonProperty("rule_create")]
        /// <summary>Gets or sets the first import containing this rule.</summary>
        public long? Created { get; set; }

        [JsonProperty("removed")]
        /// <summary>Gets or sets the first import no longer containing this rule.</summary>
        public long? Removed { get; set; }
    }

    /// <summary>GraphQL shape for one normalized rulebase.</summary>
    private sealed class RulebaseWire
    {
        [JsonProperty("id")]
        /// <summary>Gets or sets the rulebase identifier.</summary>
        public int? Id { get; set; }

        [JsonProperty("name")]
        /// <summary>Gets or sets the imported rulebase name.</summary>
        public string? Name { get; set; }

        [JsonProperty("uid")]
        /// <summary>Gets or sets the vendor rulebase identifier.</summary>
        public string? Uid { get; set; }

        [JsonProperty("mgm_id")]
        /// <summary>Gets or sets the owning management identifier.</summary>
        public int? ManagementId { get; set; }

        [JsonProperty("created")]
        /// <summary>Gets or sets the first import containing this rulebase.</summary>
        public long? Created { get; set; }

        [JsonProperty("removed")]
        /// <summary>Gets or sets the first import no longer containing this rulebase.</summary>
        public long? Removed { get; set; }

        [JsonProperty("rules")]
        /// <summary>Gets or sets structural rules visible at the selected import.</summary>
        public List<RuleWire>? Rules { get; set; }
    }

    /// <summary>GraphQL shape for one normalized structural rule.</summary>
    private sealed class RuleWire
    {
        [JsonProperty("rule_id")]
        /// <summary>Gets or sets the rule identifier.</summary>
        public long? Id { get; set; }

        [JsonProperty("rule_uid")]
        /// <summary>Gets or sets the vendor rule identifier.</summary>
        public string? Uid { get; set; }

        [JsonProperty("mgm_id")]
        /// <summary>Gets or sets the owning management identifier.</summary>
        public int? ManagementId { get; set; }

        [JsonProperty("rulebase_id")]
        /// <summary>Gets or sets the containing rulebase identifier.</summary>
        public int? RulebaseId { get; set; }

        [JsonProperty("rule_num_numeric")]
        /// <summary>Gets or sets the native rule order.</summary>
        public double? NativeOrder { get; set; }

        [JsonProperty("rule_create")]
        /// <summary>Gets or sets the first import containing this rule.</summary>
        public long? Created { get; set; }

        [JsonProperty("removed")]
        /// <summary>Gets or sets the first import no longer containing this rule.</summary>
        public long? Removed { get; set; }
    }
}

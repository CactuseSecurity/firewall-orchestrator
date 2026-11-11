using System.Collections;
using System.Diagnostics;
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
            Invariant("invalid_scope", ("management_id", managementId), ("gateway_id", gatewayId));
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

        RulePositioningDiagnosticLog.WriteInfo(
            "graph.read",
            "success",
            Facts(
                ("import_id", importId),
                ("link_count", readValue!.Snapshot.Links.Count),
                ("rulebase_count", readValue.Snapshot.Rulebases.Count),
                ("total_rule_count", readValue.Snapshot.Rulebases.Sum(Rulebase => Rulebase.Rules.Count)),
                ("maximum_rules_per_rulebase", readValue.Snapshot.Rulebases.Count == 0
                    ? 0
                    : readValue.Snapshot.Rulebases.Max(Rulebase => Rulebase.Rules.Count))));
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
            Invariant("import_query_failed");
            return false;
        }

        List<ImportWire>? imports = importResult.Value;
        if (imports == null)
        {
            Invariant("import_result_null");
            return false;
        }
        if (imports.Count == 0)
        {
            failure = RulebaseGraphReadFailureCategory.NoSuccessfulImport;
            Invariant("no_successful_import", ("result_count", 0));
            return false;
        }
        if (imports.Count != 1)
        {
            Invariant("import_cardinality", ("result_count", imports.Count));
            return false;
        }
        if (imports[0] == null)
        {
            Invariant("import_row_null");
            return false;
        }
        if (!IsValidImport(imports[0], managementId))
        {
            Invariant(
                "import_row_invalid",
                ("import_id", imports[0].ControlId ?? 0),
                ("expected_management_id", managementId),
                ("actual_management_id", imports[0].ManagementId ?? 0),
                ("import_type_id", imports[0].ImportTypeId ?? 0),
                ("successful", imports[0].Successful ?? false),
                ("stop_time_present", imports[0].StopTime.HasValue));
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
            Invariant("gateway_query_failed");
            return false;
        }

        List<GatewayWire>? gateways = gatewayResult.Value;
        if (gateways == null)
        {
            Invariant("gateway_result_null");
            return false;
        }
        if (gateways.Count != 1)
        {
            Invariant("gateway_cardinality", ("result_count", gateways.Count));
            return false;
        }
        if (gateways[0] == null || gateways[0].DeviceId != gatewayId ||
            gateways[0].ManagementId != managementId)
        {
            Invariant(
                "gateway_scope_mismatch",
                ("requested_gateway_id", gatewayId),
                ("returned_gateway_id", gateways[0]?.DeviceId ?? 0),
                ("requested_management_id", managementId),
                ("returned_management_id", gateways[0]?.ManagementId ?? 0));
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
            Invariant("rulebase_query_failed");
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

        if (!TryValidateSnapshotIdentities(rulebases, links))
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
        catch (ArgumentException exception)
        {
            RulePositioningDiagnosticLog.WriteException("graph.snapshot.construct", exception);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            RulePositioningDiagnosticLog.WriteException("graph.snapshot.construct", exception);
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
            long started = Stopwatch.GetTimestamp();
            RulePositioningDiagnosticLog.WriteInfo(
                "graph.query",
                "start",
                Facts(("operation", operationName)));
            T value = await apiConnection.SendQueryAsync<T>(query, variables, operationName)
                .WaitAsync(cancellationToken);
            RulePositioningDiagnosticLog.WriteInfo(
                "graph.query",
                "success",
                Facts(
                    ("operation", operationName),
                    ("duration_ms", (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds),
                    ("result_count", value is ICollection collection ? collection.Count : 1)));
            return new QueryResult<T>(true, value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException(
                "graph.query",
                exception,
                Facts(("operation", operationName)));
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
        if (links == null)
        {
            Invariant("links_null");
            return false;
        }
        if (links.Count is <= 0 or > kMaximumRulebaseLinks)
        {
            Invariant(
                "link_count_out_of_range",
                ("link_count", links.Count),
                ("maximum_link_count", kMaximumRulebaseLinks));
            return false;
        }
        if (links.Any(Link => Link == null))
        {
            Invariant("link_row_null", ("violation_count", links.Count(Link => Link == null)));
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
        if (rulebaseIds.Count is <= 0 or > kMaximumRulebases)
        {
            Invariant(
                "endpoint_count_out_of_range",
                ("endpoint_count", rulebaseIds.Count),
                ("maximum_endpoint_count", kMaximumRulebases));
            return false;
        }
        RulePositioningDiagnosticLog.WriteInfo(
            "graph.links",
            "summary",
            Facts(("link_count", links.Count), ("endpoint_count", rulebaseIds.Count)));
        return true;
    }

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
        long importId)
    {
        if (link.Id is not > 0)
        {
            Invariant("link_id_invalid", ("link_id", link.Id ?? 0));
            return false;
        }
        if (!linkIds.Add(link.Id.Value))
        {
            Invariant("link_id_duplicate", ("link_id", link.Id.Value));
            return false;
        }
        if (link.GatewayId != gatewayId)
        {
            Invariant("link_gateway_mismatch", ("link_id", link.Id.Value), ("actual_gateway_id", link.GatewayId ?? 0));
            return false;
        }
        if (link.TargetRulebaseId is not > 0 || !link.LinkType.HasValue || !link.IsInitial.HasValue ||
            !link.IsGlobal.HasValue || !link.IsSection.HasValue)
        {
            Invariant(
                "link_required_field_missing",
                ("link_id", link.Id.Value),
                ("target_rulebase_id", link.TargetRulebaseId ?? 0),
                ("link_type_present", link.LinkType.HasValue),
                ("initial_present", link.IsInitial.HasValue),
                ("global_present", link.IsGlobal.HasValue),
                ("section_present", link.IsSection.HasValue));
            return false;
        }
        if (!IsHistoricallyValid(link.Created, link.Removed, importId))
        {
            Invariant("link_history_invalid", ("link_id", link.Id.Value), ("import_id", importId));
            return false;
        }
        return true;
    }

    /// <summary>Adds the optional source-rulebase endpoint after validating its identifier.</summary>
    private static bool TryAddSourceRulebaseEndpoint(LinkWire link, HashSet<int> endpoints)
    {
        if (!link.SourceRulebaseId.HasValue)
        {
            return true;
        }
        if (link.SourceRulebaseId <= 0)
        {
            Invariant("source_rulebase_id_invalid", ("link_id", link.Id ?? 0), ("source_rulebase_id", link.SourceRulebaseId ?? 0));
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
            if (link.SourceRule != null)
            {
                Invariant("source_rule_without_identity", ("link_id", link.Id ?? 0));
                return false;
            }
            return true;
        }

        SourceRuleWire? rule = link.SourceRule;
        if (link.SourceRuleId <= 0 || rule?.Id != link.SourceRuleId ||
            rule.ManagementId != managementId || rule.RulebaseId is not > 0 ||
            !IsHistoricallyValid(rule.Created, rule.Removed, importId) ||
            link.SourceRulebaseId.HasValue && link.SourceRulebaseId != rule.RulebaseId)
        {
            Invariant(
                "source_rule_invalid",
                ("link_id", link.Id ?? 0),
                ("source_rule_id", link.SourceRuleId ?? 0),
                ("returned_rule_id", rule?.Id ?? 0),
                ("returned_management_id", rule?.ManagementId ?? 0),
                ("returned_rulebase_id", rule?.RulebaseId ?? 0));
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

        if (!TryValidateCompleteGraph(context.RequestedIds, returnedIds, returnedRuleIds, wireLinks))
        {
            return false;
        }

        links = MapLinks(wireLinks, context.GatewayId);
        return true;
    }

    /// <summary>Checks response counts and configured graph-size limits before mapping.</summary>
    private static bool IsValidRulebaseResponseShape(List<RulebaseWire>? wireRulebases, int requestedCount)
    {
        if (wireRulebases == null)
        {
            Invariant("rulebase_result_null");
            return false;
        }
        if (wireRulebases.Count != requestedCount || wireRulebases.Count > kMaximumRulebases)
        {
            Invariant(
                "rulebase_response_count",
                ("requested_count", requestedCount),
                ("returned_count", wireRulebases.Count),
                ("maximum_count", kMaximumRulebases));
            return false;
        }
        if (wireRulebases.Any(Rulebase => Rulebase == null || Rulebase.Rules == null))
        {
            Invariant("rulebase_or_rules_null");
            return false;
        }
        int maximumRules = wireRulebases.Count == 0 ? 0 : wireRulebases.Max(Rulebase => Rulebase.Rules!.Count);
        long totalRules = wireRulebases.Sum(Rulebase => (long)Rulebase.Rules!.Count);
        if (maximumRules > kMaximumRulesPerRulebase || totalRules > kMaximumRulesPerGraph)
        {
            Invariant(
                "rule_count_out_of_range",
                ("total_rule_count", totalRules),
                ("maximum_rules_per_rulebase", maximumRules),
                ("total_rule_limit", kMaximumRulesPerGraph),
                ("per_rulebase_limit", kMaximumRulesPerRulebase));
            return false;
        }
        RulePositioningDiagnosticLog.WriteInfo(
            "graph.rulebases",
            "summary",
            Facts(
                ("rulebase_count", wireRulebases.Count),
                ("total_rule_count", totalRules),
                ("maximum_rules_per_rulebase", maximumRules)));
        return true;
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
        if (wireRulebase.Id is not > 0)
        {
            Invariant("rulebase_id_invalid", ("rulebase_id", wireRulebase.Id ?? 0));
            return false;
        }
        if (!returnedIds.Add(wireRulebase.Id.Value))
        {
            Invariant("rulebase_id_duplicate", ("rulebase_id", wireRulebase.Id.Value));
            return false;
        }
        if (!context.RequestedIds.Contains(wireRulebase.Id.Value))
        {
            Invariant("rulebase_not_requested", ("rulebase_id", wireRulebase.Id.Value));
            return false;
        }
        if (wireRulebase.ManagementId != context.ManagementId)
        {
            Invariant(
                "rulebase_management_mismatch",
                ("rulebase_id", wireRulebase.Id.Value),
                ("actual_management_id", wireRulebase.ManagementId ?? 0));
            return false;
        }
        if (!IsHistoricallyValid(wireRulebase.Created, wireRulebase.Removed, context.ImportId))
        {
            Invariant("rulebase_history_invalid", ("rulebase_id", wireRulebase.Id.Value), ("import_id", context.ImportId));
            return false;
        }
        if (wireRulebase.Rules == null ||
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

    /// <summary>Reports the first missing requested node or structural source rule.</summary>
    private static bool TryValidateCompleteGraph(
        List<int> requestedIds,
        HashSet<int> returnedIds,
        HashSet<long> returnedRuleIds,
        List<LinkWire> wireLinks)
    {
        List<int> missingRulebases = requestedIds.Where(id => !returnedIds.Contains(id)).ToList();
        if (missingRulebases.Count > 0)
        {
            Invariant(
                "rulebase_missing",
                ("rulebase_id", missingRulebases[0]),
                ("violation_count", missingRulebases.Count),
                ("requested_rulebase_count", requestedIds.Count),
                ("returned_rulebase_count", returnedIds.Count));
            return false;
        }

        List<long> missingSourceRules = wireLinks
            .Where(link => link.SourceRuleId.HasValue && !returnedRuleIds.Contains(link.SourceRuleId.Value))
            .Select(link => link.SourceRuleId!.Value)
            .Distinct()
            .ToList();
        if (missingSourceRules.Count > 0)
        {
            Invariant(
                "source_rule_missing",
                ("source_rule_id", missingSourceRules[0]),
                ("violation_count", missingSourceRules.Count),
                ("returned_rule_count", returnedRuleIds.Count));
            return false;
        }
        return true;
    }

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
            Invariant("rule_row_null", ("rulebase_id", owner.Id ?? 0));
            return false;
        }
        foreach (RuleWire wireRule in owner.Rules!)
        {
            if (wireRule.Id is not > 0)
            {
                Invariant("rule_id_invalid", ("rulebase_id", owner.Id ?? 0), ("rule_id", wireRule.Id ?? 0));
                return false;
            }
            if (!returnedRuleIds.Add(wireRule.Id.Value))
            {
                Invariant("rule_id_duplicate", ("rule_id", wireRule.Id.Value));
                return false;
            }
            if (wireRule.ManagementId != managementId || wireRule.RulebaseId != owner.Id)
            {
                Invariant(
                    "rule_scope_mismatch",
                    ("rule_id", wireRule.Id.Value),
                    ("actual_management_id", wireRule.ManagementId ?? 0),
                    ("actual_rulebase_id", wireRule.RulebaseId ?? 0),
                    ("expected_rulebase_id", owner.Id ?? 0));
                return false;
            }
            if (!wireRule.NativeOrder.HasValue)
            {
                Invariant("rule_native_order_null", ("rule_id", wireRule.Id.Value));
                return false;
            }
            if (!IsHistoricallyValid(wireRule.Created, wireRule.Removed, importId))
            {
                Invariant("rule_history_invalid", ("rule_id", wireRule.Id.Value), ("import_id", importId));
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

    /// <summary>Reports identity and ordering rules otherwise enforced only by the snapshot constructor.</summary>
    private static bool TryValidateSnapshotIdentities(
        IReadOnlyCollection<Rulebase> rulebases,
        IReadOnlyCollection<RulebaseLink> links)
    {
        Rulebase? blankRulebaseUid = rulebases.FirstOrDefault(Rulebase => string.IsNullOrWhiteSpace(Rulebase.Uid));
        if (blankRulebaseUid != null)
        {
            Invariant(
                "rulebase_uid_blank",
                ("rulebase_id", blankRulebaseUid.Id),
                ("uid", RulePositioningDiagnosticLog.Fingerprint(blankRulebaseUid.Uid)));
            return false;
        }
        IGrouping<string, Rulebase>? duplicateRulebaseUid = rulebases
            .GroupBy(Rulebase => Rulebase.Uid, StringComparer.Ordinal)
            .FirstOrDefault(Group => Group.Count() > 1);
        if (duplicateRulebaseUid != null)
        {
            Invariant(
                "rulebase_uid_duplicate",
                ("first_rulebase_id", duplicateRulebaseUid.First().Id),
                ("violation_count", duplicateRulebaseUid.Count()),
                ("uid", RulePositioningDiagnosticLog.Fingerprint(duplicateRulebaseUid.Key)));
            return false;
        }

        List<Rule> rules = rulebases.SelectMany(Rulebase => Rulebase.Rules ?? []).ToList();
        Rule? blankRuleUid = rules.FirstOrDefault(Rule => string.IsNullOrWhiteSpace(Rule.Uid));
        if (blankRuleUid != null)
        {
            Invariant(
                "rule_uid_blank",
                ("rule_id", blankRuleUid.Id),
                ("rulebase_id", blankRuleUid.RulebaseId),
                ("uid", RulePositioningDiagnosticLog.Fingerprint(blankRuleUid.Uid)));
            return false;
        }
        Rule? invalidOrder = rules.FirstOrDefault(Rule =>
            !double.IsFinite(Rule.RuleNumNumeric) || Rule.RuleNumNumeric < 0);
        if (invalidOrder != null)
        {
            Invariant("rule_native_order_invalid", ("rule_id", invalidOrder.Id), ("rulebase_id", invalidOrder.RulebaseId));
            return false;
        }
        IGrouping<(long RulebaseId, double NativeOrder), Rule>? duplicateOrder = rules
            .GroupBy(Rule => ((long)Rule.RulebaseId, Rule.RuleNumNumeric))
            .FirstOrDefault(Group => Group.Count() > 1);
        if (duplicateOrder != null)
        {
            Invariant(
                "rule_native_order_duplicate",
                ("rulebase_id", duplicateOrder.Key.RulebaseId),
                ("first_rule_id", duplicateOrder.First().Id),
                ("violation_count", duplicateOrder.Count()));
            return false;
        }
        IGrouping<string?, Rule>? duplicateRuleUid = rules
            .GroupBy(Rule => Rule.Uid, StringComparer.Ordinal)
            .FirstOrDefault(Group => Group.Count() > 1);
        if (duplicateRuleUid != null)
        {
            Invariant(
                "rule_uid_duplicate",
                ("first_rule_id", duplicateRuleUid.First().Id),
                ("violation_count", duplicateRuleUid.Count()),
                ("uid", RulePositioningDiagnosticLog.Fingerprint(duplicateRuleUid.Key)));
            return false;
        }
        RulebaseLink? missingSource = links.FirstOrDefault(Link =>
            !Link.IsInitial && !Link.FromRulebaseId.HasValue && !Link.FromRuleId.HasValue);
        if (missingSource != null)
        {
            Invariant("link_source_missing", ("target_rulebase_id", missingSource.NextRulebaseId));
            return false;
        }
        return true;
    }

    /// <summary>
    /// Applies the fixed-import half-open historical interval.
    /// </summary>
    private static bool IsHistoricallyValid(long? created, long? removed, long importId) =>
        created is > 0 && created <= importId && (!removed.HasValue || removed > importId);

    /// <summary>Writes one stable graph invariant and the first safe offending identity.</summary>
    private static void Invariant(string invariant, params (string Key, object? Value)[] values)
    {
        Dictionary<string, object?> facts = Facts(values);
        facts["invariant"] = invariant;
        facts.TryAdd("violation_count", 1);
        RulePositioningDiagnosticLog.WriteFailure("graph.invariant", facts);
    }

    /// <summary>Builds one bounded diagnostic fact map.</summary>
    private static Dictionary<string, object?> Facts(params (string Key, object? Value)[] values) =>
        values.ToDictionary(Value => Value.Key, Value => Value.Value);

    /// <summary>
    /// Creates one category-only reader failure.
    /// </summary>
    private static RulebaseGraphReadResult Failure(RulebaseGraphReadFailureCategory category) =>
        RulebaseGraphReadResult.Failure(category);

    /// <summary>Captures a query value without exposing transport failure details.</summary>
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
        public long? SourceRuleId { get; set; }

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
        public long? Id { get; set; }

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

using System.Diagnostics;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using NetTools;
using Newtonsoft.Json;
using CheckPointLayerGuardReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CheckPoint.CheckPointLayerGuardSnapshot,
    FWO.Services.RulePositioning.CheckPoint.CheckPointLayerGuardReadFailureCategory>;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Reads immutable Check Point inline guard semantics at one fixed import.</summary>
public sealed class CheckPointLayerGuardSnapshotReader(ApiConnection apiConnection)
    : ICheckPointLayerGuardSnapshotReader
{
    private const string kGroupObjectType = "group";
    private const string kOperation = "getCheckPointLayerGuardsAtImport";
    private static readonly IReadOnlyCollection<CheckPointLayerGuard> kNoGuards =
        new List<CheckPointLayerGuard>().AsReadOnly();

    /// <inheritdoc />
    public async Task<CheckPointLayerGuardReadResult> ReadAsync(
        RulebaseGraphSnapshot graph,
        long importId,
        CancellationToken cancellationToken)
    {
        if (graph == null || importId <= 0 || graph.ManagementId <= 0 || graph.GatewayId <= 0)
        {
            Invariant("invalid_scope", ("import_id", importId), ("management_id", graph?.ManagementId ?? 0), ("gateway_id", graph?.GatewayId ?? 0));
            return Failure(CheckPointLayerGuardReadFailureCategory.InvalidInput);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryBuildCandidates(graph, out List<GuardCandidate> candidates))
        {
            return Failure(CheckPointLayerGuardReadFailureCategory.ScopeMismatch);
        }
        if (candidates.Count == 0)
        {
            RulePositioningDiagnosticLog.WriteInfo("guard.candidates", "summary", Facts(("candidate_count", 0), ("inline_link_count", 0)));
            return Success(graph, importId, kNoGuards);
        }

        List<long> sourceRuleIds = candidates.Select(candidate => candidate.SourceRuleId).Order().ToList();
        List<RuleWire>? rules;
        try
        {
            long started = Stopwatch.GetTimestamp();
            RulePositioningDiagnosticLog.WriteInfo(
                "guard.query",
                "start",
                Facts(("requested_rule_count", sourceRuleIds.Count), ("import_id", importId)));
            rules = await apiConnection.SendQueryAsync<List<RuleWire>>(
                RulePositioningQueries.getCheckPointLayerGuardsAtImport,
                new { managementId = graph.ManagementId, sourceRuleIds, importId },
                kOperation).WaitAsync(cancellationToken);
            RulePositioningDiagnosticLog.WriteInfo(
                "guard.query",
                "success",
                Facts(
                    ("duration_ms", (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds),
                    ("returned_rule_count", rules?.Count ?? 0)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException("guard.query", exception);
            return Failure(CheckPointLayerGuardReadFailureCategory.QueryFailed);
        }

        if (!TryMapGuards(rules, candidates, graph.ManagementId, importId, out List<CheckPointLayerGuard> guards))
        {
            return Failure(CheckPointLayerGuardReadFailureCategory.InvalidGuardData);
        }
        RulePositioningDiagnosticLog.WriteInfo(
            "guard.map",
            "success",
            Facts(
                ("guard_count", guards.Count),
                ("term_count", guards.Sum(Guard => Guard.Source.Terms.Count + Guard.Destination.Terms.Count)),
                ("range_count", guards.Sum(Guard => Guard.Source.Terms.Sum(Term => Term.IncludedRanges.Count + Term.ExcludedRanges.Count) +
                    Guard.Destination.Terms.Sum(Term => Term.IncludedRanges.Count + Term.ExcludedRanges.Count))),
                ("unresolved_term_count", guards.Sum(Guard => Guard.Source.Terms.Count(Term => Term.HasUnresolvedIncludedTerms || Term.HasUnresolvedExcludedTerms) +
                    Guard.Destination.Terms.Count(Term => Term.HasUnresolvedIncludedTerms || Term.HasUnresolvedExcludedTerms)))));
        return Success(graph, importId, guards);
    }

    /// <summary>Derives one unambiguous candidate per inline source rule and target layer.</summary>
    private static bool TryBuildCandidates(
        RulebaseGraphSnapshot graph,
        out List<GuardCandidate> candidates)
    {
        candidates = [];
        List<RulebaseGraphLink> links = [.. graph.Links.Where(link => link.LinkType == RulebaseLinkTypes.Inline)];
        HashSet<long> initialTargets = [.. graph.Links.Where(link => link.IsInitial)
            .Select(link => link.TargetRulebaseId)];
        RulePositioningDiagnosticLog.WriteInfo(
            "guard.candidates",
            "summary",
            Facts(
                ("rulebase_count", graph.Rulebases.Count),
                ("total_link_count", graph.Links.Count),
                ("inline_link_count", links.Count)));
        if (!HasValidCandidateLinks(links, initialTargets))
        {
            return false;
        }
        foreach (RulebaseGraphLink link in links)
        {
            if (!TryCreateCandidate(graph, link, out GuardCandidate? candidate))
            {
                return false;
            }
            candidates.Add(candidate!);
        }
        return true;
    }

    /// <summary>Checks inline-link identity, scope, and one-to-one cardinality.</summary>
    private static bool HasValidCandidateLinks(
        List<RulebaseGraphLink> links,
        HashSet<long> initialTargets)
    {
        List<RulebaseGraphLink> invalid = links.Where(link => !link.SourceRuleId.HasValue ||
            !link.SourceRulebaseId.HasValue || link.IsInitial || link.IsSection || link.IsGlobal ||
            initialTargets.Contains(link.TargetRulebaseId)).ToList();
        IGrouping<long?, RulebaseGraphLink>? duplicateSource = links.GroupBy(link => link.SourceRuleId)
            .FirstOrDefault(group => group.Count() > 1);
        IGrouping<long, RulebaseGraphLink>? duplicateTarget = links.GroupBy(link => link.TargetRulebaseId)
            .FirstOrDefault(group => group.Count() > 1);
        if (invalid.Count > 0 || duplicateSource != null || duplicateTarget != null)
        {
            Invariant(
                invalid.Count > 0 ? "candidate_link_invalid" : duplicateSource != null ? "candidate_source_ambiguous" : "candidate_target_ambiguous",
                ("violation_count", invalid.Count > 0 ? invalid.Count : duplicateSource?.Count() ?? duplicateTarget!.Count()),
                ("source_rule_id", invalid.FirstOrDefault()?.SourceRuleId ?? duplicateSource?.Key ?? 0),
                ("target_rulebase_id", invalid.FirstOrDefault()?.TargetRulebaseId ?? duplicateTarget?.Key ?? 0));
            return false;
        }
        return true;
    }

    /// <summary>Maps one validated inline link to a guard query candidate.</summary>
    private static bool TryCreateCandidate(
        RulebaseGraphSnapshot graph,
        RulebaseGraphLink link,
        out GuardCandidate? candidate)
    {
        candidate = null;
        RulebaseGraphNode? source = graph.Rulebases.SingleOrDefault(rulebase =>
            rulebase.Id == link.SourceRulebaseId);
        RulebaseGraphNode? target = graph.Rulebases.SingleOrDefault(rulebase =>
            rulebase.Id == link.TargetRulebaseId);
        if (source == null || target == null || source.Rules.Count(rule => rule.Id == link.SourceRuleId) != 1)
        {
            Invariant(
                "candidate_endpoint_invalid",
                ("source_rule_id", link.SourceRuleId ?? 0),
                ("source_rulebase_id", link.SourceRulebaseId ?? 0),
                ("target_rulebase_id", link.TargetRulebaseId));
            return false;
        }
        candidate = new GuardCandidate(
            link.SourceRuleId!.Value,
            link.SourceRulebaseId!.Value,
            link.TargetRulebaseId);
        return true;
    }

    /// <summary>Validates exact query cardinality and maps every rule side.</summary>
    private static bool TryMapGuards(
        List<RuleWire>? rules,
        IReadOnlyCollection<GuardCandidate> candidates,
        int managementId,
        long importId,
        out List<CheckPointLayerGuard> guards)
    {
        guards = [];
        if (!HasValidGuardRules(rules, candidates.Count))
        {
            return false;
        }
        foreach (GuardCandidate candidate in candidates)
        {
            if (!TryMapGuard(rules!, candidate, managementId, importId, out CheckPointLayerGuard? guard))
            {
                return false;
            }
            guards.Add(guard!);
        }
        return true;
    }

    /// <summary>Checks guard query count, nullability, and rule identity uniqueness.</summary>
    private static bool HasValidGuardRules(List<RuleWire>? rules, int expectedCount)
    {
        if (rules == null || rules.Count != expectedCount || rules.Any(Rule => Rule == null))
        {
            Invariant("guard_rule_cardinality", ("expected_count", expectedCount), ("actual_count", rules?.Count ?? 0));
            return false;
        }
        IGrouping<long?, RuleWire>? duplicate = rules.GroupBy(Rule => Rule.Id).FirstOrDefault(Group => Group.Count() > 1);
        if (duplicate != null)
        {
            Invariant("guard_rule_duplicate", ("rule_id", duplicate.Key ?? 0), ("violation_count", duplicate.Count()));
            return false;
        }
        return true;
    }

    /// <summary>Validates and maps one queried guard rule.</summary>
    private static bool TryMapGuard(
        List<RuleWire> rules,
        GuardCandidate candidate,
        int managementId,
        long importId,
        out CheckPointLayerGuard? guard)
    {
        guard = null;
        RuleWire? rule = rules.SingleOrDefault(item => item.Id == candidate.SourceRuleId);
        if (rule == null || rule.ManagementId != managementId ||
            rule.RulebaseId != candidate.SourceRulebaseId ||
            !rule.SourceNegated.HasValue || !rule.DestinationNegated.HasValue ||
            !IsHistorical(rule.Created, rule.Removed, importId))
        {
            Invariant(
                "guard_rule_invalid",
                ("expected_rule_id", candidate.SourceRuleId),
                ("returned_rule_id", rule?.Id ?? 0),
                ("expected_management_id", managementId),
                ("actual_management_id", rule?.ManagementId ?? 0),
                ("expected_rulebase_id", candidate.SourceRulebaseId),
                ("actual_rulebase_id", rule?.RulebaseId ?? 0));
            return false;
        }
        if (!TryMapSide(
                rule.Froms, candidate.SourceRuleId, rule.SourceNegated.Value, true, importId,
                out CheckPointGuardSide? source) ||
            !TryMapSide(
                rule.Tos, candidate.SourceRuleId, rule.DestinationNegated.Value, false, importId,
                out CheckPointGuardSide? destination))
        {
            return false;
        }
        guard = new CheckPointLayerGuard(
            candidate.SourceRuleId,
            candidate.SourceRulebaseId,
            candidate.TargetLayerId,
            source!,
            destination!);
        return true;
    }

    /// <summary>Maps one side while checking duplicated negation and association history.</summary>
    private static bool TryMapSide(
        List<AssociationWire>? associations,
        long expectedRuleId,
        bool sideNegated,
        bool sourceSide,
        long importId,
        out CheckPointGuardSide? side)
    {
        side = null;
        if (associations == null || associations.Any(association => association == null) ||
            associations.GroupBy(association => association.ObjectId).Any(group => group.Count() != 1))
        {
            Invariant("guard_association_collection_invalid", ("rule_id", expectedRuleId), ("source_side", sourceSide));
            return false;
        }
        List<CheckPointGuardAddressTerm> terms = [];
        foreach (AssociationWire association in associations)
        {
            long? created = sourceSide ? association.SourceCreated : association.DestinationCreated;
            if (association.RuleId != expectedRuleId || association.ObjectId is not > 0 ||
                association.Negated != sideNegated || !IsHistorical(created, association.Removed, importId) ||
                association.Object?.Id != association.ObjectId ||
                !TryMapTerm(association.Object, importId, out CheckPointGuardAddressTerm? term))
            {
                Invariant(
                    "guard_association_invalid",
                    ("rule_id", expectedRuleId),
                    ("object_id", association.ObjectId ?? 0),
                    ("returned_rule_id", association.RuleId ?? 0),
                    ("expected_negated", sideNegated),
                    ("actual_negated", association.Negated ?? false));
                return false;
            }
            terms.Add(term!);
        }
        side = new CheckPointGuardSide(sideNegated, terms);
        return true;
    }

    /// <summary>Preserves one independent term per top-level object association.</summary>
    private static bool TryMapTerm(
        ObjectWire? topLevel,
        long importId,
        out CheckPointGuardAddressTerm? term)
    {
        term = null;
        if (!IsValidObject(topLevel, importId))
        {
            Invariant(
                "guard_object_invalid",
                ("object_id", topLevel?.Id ?? 0),
                ("object_uid", RulePositioningDiagnosticLog.Fingerprint(topLevel?.Uid)),
                ("object_type", RulePositioningDiagnosticLog.Fingerprint(topLevel?.Type?.Name)));
            return false;
        }
        List<IPAddressRange> included = [];
        List<IPAddressRange> excluded = [];
        bool unresolvedIncluded = false;
        bool unresolvedExcluded = false;
        if (HasAddress(topLevel!))
        {
            if (!TryAddDirectAddress(topLevel!, included))
            {
                return false;
            }
        }
        else if (!TryMapGroupMembers(
            topLevel!,
            importId,
            included,
            excluded,
            out unresolvedIncluded,
            out unresolvedExcluded))
        {
            return false;
        }
        term = new CheckPointGuardAddressTerm(
            included,
            excluded,
            unresolvedIncluded,
            unresolvedExcluded);
        return true;
    }

    /// <summary>Adds one direct object address to the included ranges.</summary>
    private static bool TryAddDirectAddress(ObjectWire directObject, List<IPAddressRange> included)
    {
        if (!TryParseRange(directObject.Ip, directObject.IpEnd, out IPAddressRange? range))
        {
            Invariant("guard_address_invalid", ("object_id", directObject.Id ?? 0));
            return false;
        }
        included.Add(range!);
        return true;
    }

    /// <summary>Maps flattened group members while preserving unresolved positive and negative terms.</summary>
    private static bool TryMapGroupMembers(
        ObjectWire group,
        long importId,
        List<IPAddressRange> included,
        List<IPAddressRange> excluded,
        out bool unresolvedIncluded,
        out bool unresolvedExcluded)
    {
        unresolvedIncluded = false;
        unresolvedExcluded = false;
        if (group.Flats == null || group.Flats.GroupBy(flat => flat.MemberId).Any(members => members.Count() != 1))
        {
            Invariant("guard_group_flat_collection_invalid", ("group_id", group.Id ?? 0));
            return false;
        }

        foreach (FlatWire flat in group.Flats)
        {
            if (!TryMapFlat(
                    flat, group.Id!.Value, importId, included, excluded,
                    ref unresolvedIncluded, ref unresolvedExcluded))
            {
                return false;
            }
        }

        if (included.Count == 0 && excluded.Count == 0 && !unresolvedIncluded && !unresolvedExcluded)
        {
            unresolvedIncluded = true;
        }
        return true;
    }

    /// <summary>Maps one flattened group row into address or unresolved-term state.</summary>
    private static bool TryMapFlat(
        FlatWire flat,
        long groupId,
        long importId,
        List<IPAddressRange> included,
        List<IPAddressRange> excluded,
        ref bool unresolvedIncluded,
        ref bool unresolvedExcluded)
    {
        if (!IsValidFlat(flat, groupId, importId))
        {
            Invariant(
                "guard_group_flat_invalid",
                ("group_id", groupId),
                ("member_id", flat.MemberId ?? 0),
                ("returned_group_id", flat.GroupId ?? 0));
            return false;
        }
        if (IsStructuralGroupRow(flat, groupId))
        {
            return true;
        }
        if (!HasAddress(flat.Object!))
        {
            unresolvedIncluded |= !flat.Negated!.Value;
            unresolvedExcluded |= flat.Negated.Value;
            return true;
        }
        if (!TryParseRange(flat.Object!.Ip, flat.Object.IpEnd, out IPAddressRange? range))
        {
            Invariant("guard_flat_address_invalid", ("group_id", groupId), ("member_id", flat.MemberId ?? 0));
            return false;
        }
        (flat.Negated!.Value ? excluded : included).Add(range!);
        return true;
    }

    /// <summary>Returns whether a flattened member represents group structure rather than an address leaf.</summary>
    private static bool IsStructuralGroupRow(FlatWire flat, long groupId)
    {
        return flat.MemberId == groupId || flat.Object!.Type?.Name == kGroupObjectType;
    }

    /// <summary>Validates one flattened member against its group and fixed import.</summary>
    private static bool IsValidFlat(FlatWire flat, long groupId, long importId)
    {
        return IsHistorical(flat.Created, flat.Removed, importId) &&
            flat.GroupId == groupId && flat.Negated.HasValue &&
            flat.MemberId is > 0 && flat.Object?.Id == flat.MemberId &&
            IsValidObject(flat.Object, importId);
    }

    /// <summary>Parses normalized IP/end data without crossing address families.</summary>
    private static bool TryParseRange(string? start, string? end, out IPAddressRange? range)
    {
        return IpOperations.TryParseIPRange(start?.Trim(), end?.Trim(), out range);
    }

    /// <summary>Checks whether an imported object contains address data.</summary>
    private static bool HasAddress(ObjectWire value) =>
        !string.IsNullOrWhiteSpace(value.Ip) || !string.IsNullOrWhiteSpace(value.IpEnd);

    /// <summary>Checks imported object identity and fixed-import visibility.</summary>
    private static bool IsValidObject(ObjectWire? value, long importId) =>
        value?.Id is > 0 && IsHistorical(value.Created, value.Removed, importId) &&
        (string.IsNullOrWhiteSpace(value.IpEnd) || !string.IsNullOrWhiteSpace(value.Ip));

    /// <summary>Applies the fixed-import half-open historical interval.</summary>
    private static bool IsHistorical(long? created, long? removed, long importId) =>
        created is > 0 && created <= importId && (!removed.HasValue || removed > importId);

    /// <summary>Writes one stable guard mapping invariant with safe identities only.</summary>
    private static void Invariant(string invariant, params (string Key, object? Value)[] values)
    {
        Dictionary<string, object?> facts = Facts(values);
        facts["invariant"] = invariant;
        facts.TryAdd("violation_count", 1);
        RulePositioningDiagnosticLog.WriteFailure("guard.invariant", facts);
    }

    /// <summary>Builds one bounded guard diagnostic fact map.</summary>
    private static Dictionary<string, object?> Facts(params (string Key, object? Value)[] values) =>
        values.ToDictionary(Value => Value.Key, Value => Value.Value);

    /// <summary>Creates a successful immutable guard snapshot result.</summary>
    private static CheckPointLayerGuardReadResult Success(
        RulebaseGraphSnapshot graph,
        long importId,
        IReadOnlyCollection<CheckPointLayerGuard> guards) =>
        CheckPointLayerGuardReadResult.Success(
            new CheckPointLayerGuardSnapshot(graph.ManagementId, graph.GatewayId, importId, guards));

    /// <summary>Creates one category-only guard read failure.</summary>
    private static CheckPointLayerGuardReadResult Failure(CheckPointLayerGuardReadFailureCategory category) =>
        CheckPointLayerGuardReadResult.Failure(category);

    /// <summary>Identities required to query and map one inline-layer guard.</summary>
    private sealed record GuardCandidate(long SourceRuleId, long SourceRulebaseId, long TargetLayerId);

    /// <summary>GraphQL shape for one guard rule.</summary>
    private sealed class RuleWire
    {
        [JsonProperty("rule_id")] public long? Id { get; set; }
        [JsonProperty("mgm_id")] public int? ManagementId { get; set; }
        [JsonProperty("rulebase_id")] public long? RulebaseId { get; set; }
        [JsonProperty("rule_create")] public long? Created { get; set; }
        [JsonProperty("removed")] public long? Removed { get; set; }
        [JsonProperty("rule_src_neg")] public bool? SourceNegated { get; set; }
        [JsonProperty("rule_dst_neg")] public bool? DestinationNegated { get; set; }
        [JsonProperty("rule_froms")] public List<AssociationWire>? Froms { get; set; }
        [JsonProperty("rule_tos")] public List<AssociationWire>? Tos { get; set; }
    }

    /// <summary>GraphQL shape for one rule-to-object association.</summary>
    private sealed class AssociationWire
    {
        [JsonProperty("rule_id")] public long? RuleId { get; set; }
        [JsonProperty("obj_id")] public long? ObjectId { get; set; }
        [JsonProperty("negated")] public bool? Negated { get; set; }
        [JsonProperty("rf_create")] public long? SourceCreated { get; set; }
        [JsonProperty("rt_create")] public long? DestinationCreated { get; set; }
        [JsonProperty("removed")] public long? Removed { get; set; }
        [JsonProperty("object")] public ObjectWire? Object { get; set; }
    }

    /// <summary>GraphQL shape for one imported network object.</summary>
    private sealed class ObjectWire
    {
        [JsonProperty("obj_id")] public long? Id { get; set; }
        [JsonProperty("obj_uid")] public string? Uid { get; set; }
        [JsonProperty("obj_name")] public string? Name { get; set; }
        [JsonProperty("obj_ip")] public string? Ip { get; set; }
        [JsonProperty("obj_ip_end")] public string? IpEnd { get; set; }
        [JsonProperty("obj_create")] public long? Created { get; set; }
        [JsonProperty("removed")] public long? Removed { get; set; }
        [JsonProperty("type")] public ObjectTypeWire? Type { get; set; }
        [JsonProperty("objgrp_flats")] public List<FlatWire>? Flats { get; set; }
    }

    /// <summary>GraphQL shape for one flattened network-group member.</summary>
    private sealed class FlatWire
    {
        [JsonProperty("objgrp_flat_id")] public long? GroupId { get; set; }
        [JsonProperty("objgrp_flat_member_id")] public long? MemberId { get; set; }
        [JsonProperty("negated")] public bool? Negated { get; set; }
        [JsonProperty("import_created")] public long? Created { get; set; }
        [JsonProperty("removed")] public long? Removed { get; set; }
        [JsonProperty("object")] public ObjectWire? Object { get; set; }
    }

    /// <summary>GraphQL shape for an imported object type.</summary>
    private sealed class ObjectTypeWire
    {
        [JsonProperty("name")] public string? Name { get; set; }
    }
}

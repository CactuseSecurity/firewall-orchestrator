using System.Net;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using NetTools;
using Newtonsoft.Json;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Reads immutable Check Point inline guard semantics at one fixed import.</summary>
public sealed class CheckPointLayerGuardSnapshotReader(ApiConnection apiConnection)
    : ICheckPointLayerGuardSnapshotReader
{
    private const string kOperation = "getCheckPointLayerGuardsAtImport";

    /// <inheritdoc />
    public async Task<CheckPointLayerGuardReadResult> ReadAsync(
        RulebaseGraphSnapshot graph,
        long importId,
        CancellationToken cancellationToken)
    {
        if (graph == null || importId <= 0 || graph.ManagementId <= 0 || graph.GatewayId <= 0)
        {
            return Failure(CheckPointLayerGuardReadFailureCategory.InvalidInput);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryBuildCandidates(graph, out List<GuardCandidate> candidates))
        {
            return Failure(CheckPointLayerGuardReadFailureCategory.ScopeMismatch);
        }
        if (candidates.Count == 0)
        {
            return Success(graph, importId, []);
        }

        List<long> sourceRuleIds = candidates.Select(candidate => candidate.SourceRuleId).Order().ToList();
        List<RuleWire>? rules;
        try
        {
            rules = await apiConnection.SendQueryAsync<List<RuleWire>>(
                RulePositioningQueries.getCheckPointLayerGuardsAtImport,
                new { managementId = graph.ManagementId, sourceRuleIds, importId },
                kOperation).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Failure(CheckPointLayerGuardReadFailureCategory.QueryFailed);
        }

        if (!TryMapGuards(rules, candidates, graph.ManagementId, importId, out List<CheckPointLayerGuard> guards))
        {
            return Failure(CheckPointLayerGuardReadFailureCategory.InvalidGuardData);
        }
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
        if (links.Any(link => !link.SourceRuleId.HasValue || !link.SourceRulebaseId.HasValue ||
                link.IsInitial || link.IsSection || link.IsGlobal || initialTargets.Contains(link.TargetRulebaseId)) ||
            links.GroupBy(link => link.SourceRuleId).Any(group => group.Count() != 1) ||
            links.GroupBy(link => link.TargetRulebaseId).Any(group => group.Count() != 1))
        {
            return false;
        }
        foreach (RulebaseGraphLink link in links)
        {
            RulebaseGraphNode? source = graph.Rulebases.SingleOrDefault(rulebase =>
                rulebase.Id == link.SourceRulebaseId);
            RulebaseGraphNode? target = graph.Rulebases.SingleOrDefault(rulebase =>
                rulebase.Id == link.TargetRulebaseId);
            if (source == null || target == null ||
                source.Rules.Count(rule => rule.Id == link.SourceRuleId) != 1)
            {
                return false;
            }
            candidates.Add(new GuardCandidate(
                link.SourceRuleId!.Value,
                link.SourceRulebaseId!.Value,
                link.TargetRulebaseId));
        }
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
        if (rules == null || rules.Count != candidates.Count || rules.Any(rule => rule == null) ||
            rules.GroupBy(rule => rule.Id).Any(group => group.Count() != 1))
        {
            return false;
        }
        foreach (GuardCandidate candidate in candidates)
        {
            RuleWire? rule = rules.SingleOrDefault(rule => rule.Id == candidate.SourceRuleId);
            if (rule == null || rule.ManagementId != managementId ||
                rule.RulebaseId != candidate.SourceRulebaseId ||
                !rule.SourceNegated.HasValue || !rule.DestinationNegated.HasValue ||
                !IsHistorical(rule.Created, rule.Removed, importId) ||
                !TryMapSide(rule.Froms, candidate.SourceRuleId, rule.SourceNegated.Value, true, importId, out CheckPointGuardSide? source) ||
                !TryMapSide(rule.Tos, candidate.SourceRuleId, rule.DestinationNegated.Value, false, importId, out CheckPointGuardSide? destination))
            {
                return false;
            }
            guards.Add(new CheckPointLayerGuard(
                candidate.SourceRuleId,
                candidate.SourceRulebaseId,
                candidate.TargetLayerId,
                source!,
                destination!));
        }
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
            return false;
        }
        List<IPAddressRange> included = [];
        List<IPAddressRange> excluded = [];
        bool unresolvedIncluded = false;
        bool unresolvedExcluded = false;
        if (HasAddress(topLevel!))
        {
            if (!TryParseRange(topLevel!.Ip, topLevel.IpEnd, out IPAddressRange? range))
            {
                return false;
            }
            included.Add(range!);
        }
        else
        {
            if (topLevel!.Flats == null || topLevel.Flats.GroupBy(flat => flat.MemberId).Any(group => group.Count() != 1))
            {
                return false;
            }
            foreach (FlatWire flat in topLevel.Flats)
            {
                if (!IsHistorical(flat.Created, flat.Removed, importId) ||
                    flat.GroupId != topLevel.Id || !flat.Negated.HasValue ||
                    flat.MemberId is not > 0 || flat.Object?.Id != flat.MemberId ||
                    !IsValidObject(flat.Object, importId))
                {
                    return false;
                }
                if (!HasAddress(flat.Object!))
                {
                    unresolvedIncluded |= !flat.Negated.Value;
                    unresolvedExcluded |= flat.Negated.Value;
                }
                else if (!TryParseRange(flat.Object!.Ip, flat.Object.IpEnd, out IPAddressRange? range))
                {
                    return false;
                }
                else if (flat.Negated.Value)
                {
                    excluded.Add(range!);
                }
                else
                {
                    included.Add(range!);
                }
            }
            if (included.Count == 0 && excluded.Count == 0 && !unresolvedIncluded && !unresolvedExcluded)
            {
                unresolvedIncluded = true;
            }
        }
        term = new CheckPointGuardAddressTerm(
            included,
            excluded,
            unresolvedIncluded,
            unresolvedExcluded);
        return true;
    }

    /// <summary>Parses normalized IP/end data without crossing address families.</summary>
    private static bool TryParseRange(string? start, string? end, out IPAddressRange? range)
    {
        range = null;
        if (string.IsNullOrWhiteSpace(start))
        {
            return string.IsNullOrWhiteSpace(end);
        }
        if (!IPAddressRange.TryParse(start.Trim(), out IPAddressRange first))
        {
            return false;
        }
        IPAddress begin = first.Begin;
        IPAddress finish = first.End;
        if (!string.IsNullOrWhiteSpace(end))
        {
            if (!IPAddressRange.TryParse(end.Trim(), out IPAddressRange last))
            {
                return false;
            }
            finish = last.End;
        }
        if (begin.AddressFamily != finish.AddressFamily || IpOperations.CompareIpValues(begin, finish) > 0)
        {
            return false;
        }
        range = new IPAddressRange(begin, finish);
        return true;
    }

    private static bool HasAddress(ObjectWire value) =>
        !string.IsNullOrWhiteSpace(value.Ip) || !string.IsNullOrWhiteSpace(value.IpEnd);

    private static bool IsValidObject(ObjectWire? value, long importId) =>
        value?.Id is > 0 && IsHistorical(value.Created, value.Removed, importId) &&
        (string.IsNullOrWhiteSpace(value.IpEnd) || !string.IsNullOrWhiteSpace(value.Ip));

    private static bool IsHistorical(long? created, long? removed, long importId) =>
        created is > 0 && created <= importId && (!removed.HasValue || removed > importId);

    private static CheckPointLayerGuardReadSuccess Success(
        RulebaseGraphSnapshot graph,
        long importId,
        IReadOnlyCollection<CheckPointLayerGuard> guards) =>
        new(new CheckPointLayerGuardSnapshot(graph.ManagementId, graph.GatewayId, importId, guards));

    private static CheckPointLayerGuardReadFailure Failure(CheckPointLayerGuardReadFailureCategory category) => new(category);

    private sealed record GuardCandidate(long SourceRuleId, long SourceRulebaseId, long TargetLayerId);

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

    private sealed class FlatWire
    {
        [JsonProperty("objgrp_flat_id")] public long? GroupId { get; set; }
        [JsonProperty("objgrp_flat_member_id")] public long? MemberId { get; set; }
        [JsonProperty("negated")] public bool? Negated { get; set; }
        [JsonProperty("import_created")] public long? Created { get; set; }
        [JsonProperty("removed")] public long? Removed { get; set; }
        [JsonProperty("object")] public ObjectWire? Object { get; set; }
    }

    private sealed class ObjectTypeWire
    {
        [JsonProperty("name")] public string? Name { get; set; }
    }
}

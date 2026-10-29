using System.Collections.ObjectModel;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Immutable structural rulebase graph for one management and gateway.
/// </summary>
public sealed class RulebaseGraphSnapshot
{
    public int ManagementId { get; }
    public int GatewayId { get; }
    public IReadOnlyList<RulebaseGraphNode> Rulebases { get; }
    public IReadOnlyList<RulebaseGraphLink> Links { get; }

    /// <summary>
    /// Creates a graph projection from already-loaded normalized entities.
    /// </summary>
    public RulebaseGraphSnapshot(
        int managementId,
        int gatewayId,
        IEnumerable<Rulebase> rulebases,
        IEnumerable<RulebaseLink> links)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managementId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gatewayId);
        ArgumentNullException.ThrowIfNull(rulebases);
        ArgumentNullException.ThrowIfNull(links);

        ManagementId = managementId;
        GatewayId = gatewayId;

        List<Rulebase> suppliedRulebases = rulebases.ToList();
        List<RulebaseLink> suppliedLinks = links.ToList();
        RejectNullEntries(suppliedRulebases, nameof(rulebases));
        RejectNullEntries(suppliedLinks, nameof(links));

        List<RulebaseGraphNode> copiedRulebases = suppliedRulebases
            .Select(rulebase => CopyRulebase(rulebase, managementId))
            .ToList();
        ValidateUniqueIdentities(copiedRulebases);

        Dictionary<long, RulebaseGraphNode> rulebasesById = copiedRulebases.ToDictionary(rulebase => rulebase.Id);
        Dictionary<long, RulebaseGraphRule> rulesById = copiedRulebases
            .SelectMany(rulebase => rulebase.Rules)
            .ToDictionary(rule => rule.Id);
        List<RulebaseGraphLink> copiedLinks = suppliedLinks
            .Select(link => CopyLink(link, gatewayId))
            .ToList();
        ValidateLinkEndpoints(copiedLinks, rulebasesById, rulesById, nameof(links));

        Rulebases = new ReadOnlyCollection<RulebaseGraphNode>(copiedRulebases);
        Links = new ReadOnlyCollection<RulebaseGraphLink>(copiedLinks);
    }

    /// <summary>
    /// Rejects null collection entries before their properties are read.
    /// </summary>
    private static void RejectNullEntries<T>(IReadOnlyCollection<T> entries, string parameterName)
    {
        if (entries.Any(entry => entry == null))
        {
            throw new ArgumentException("A rulebase graph must not contain null entries.", parameterName);
        }
    }

    /// <summary>
    /// Copies and validates one rulebase and its structural rules.
    /// </summary>
    private static RulebaseGraphNode CopyRulebase(Rulebase rulebase, int managementId)
    {
        if (rulebase.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rulebase), "A rulebase ID must be positive.");
        }
        if (rulebase.MgmtId != managementId)
        {
            throw new ArgumentException("A rulebase belongs to another management.", nameof(rulebase));
        }
        string uid = RequireIdentity(rulebase.Uid, "rulebase UID", nameof(rulebase));
        if (rulebase.Rules == null)
        {
            throw new ArgumentException("A rulebase rules collection must not be null.", nameof(rulebase));
        }

        List<RulebaseGraphRule> rules = rulebase.Rules
            .Select(rule => CopyRule(rule, rulebase, managementId))
            .OrderBy(rule => rule.NativeOrder)
            .ToList();
        EnsureUnique(rules.Select(rule => rule.Id), "rule ID", nameof(rulebase));
        EnsureUnique(rules.Select(rule => rule.Uid), "rule UID", nameof(rulebase));
        EnsureDeterministicOrder(rules, nameof(rulebase));
        return new RulebaseGraphNode(rulebase.Id, uid, rulebase.Name ?? "", rules);
    }

    /// <summary>
    /// Copies only the rule identity and native order required for positioning.
    /// </summary>
    private static RulebaseGraphRule CopyRule(Rule rule, Rulebase owner, int managementId)
    {
        if (rule == null)
        {
            throw new ArgumentException("A rulebase must not contain null rules.", nameof(owner));
        }
        if (rule.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rule), "A rule ID must be positive.");
        }
        if (rule.MgmtId != managementId || rule.RulebaseId != owner.Id)
        {
            throw new ArgumentException("A rule belongs to another management or rulebase.", nameof(rule));
        }
        if (!double.IsFinite(rule.RuleNumNumeric) || rule.RuleNumNumeric < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rule), "A rule native order must be finite and non-negative.");
        }
        string uid = RequireIdentity(rule.Uid, "rule UID", nameof(rule));
        return new RulebaseGraphRule(rule.Id, uid, rule.RuleNumNumeric);
    }

    /// <summary>
    /// Rejects duplicate identities across the complete graph.
    /// </summary>
    private static void ValidateUniqueIdentities(IReadOnlyCollection<RulebaseGraphNode> rulebases)
    {
        EnsureUnique(rulebases.Select(rulebase => rulebase.Id), "rulebase ID", nameof(rulebases));
        EnsureUnique(rulebases.Select(rulebase => rulebase.Uid), "rulebase UID", nameof(rulebases));
        EnsureUnique(rulebases.SelectMany(rulebase => rulebase.Rules).Select(rule => rule.Id), "rule ID", nameof(rulebases));
        EnsureUnique(rulebases.SelectMany(rulebase => rulebase.Rules).Select(rule => rule.Uid), "rule UID", nameof(rulebases));
    }

    /// <summary>
    /// Requires unique native order within one rulebase after sorting.
    /// </summary>
    private static void EnsureDeterministicOrder(
        List<RulebaseGraphRule> rules,
        string parameterName)
    {
        for (int index = 1; index < rules.Count; index++)
        {
#pragma warning disable S1244 // Native order is a stored identity; distinct adjacent values must remain distinct.
            if (rules[index - 1].NativeOrder == rules[index].NativeOrder)
#pragma warning restore S1244
            {
                throw new ArgumentException("Rule native order must be unique within a rulebase.", parameterName);
            }
        }
    }

    /// <summary>
    /// Copies a link without retaining mutable navigation objects.
    /// </summary>
    private static RulebaseGraphLink CopyLink(RulebaseLink link, int gatewayId)
    {
        if (link.GatewayId != gatewayId)
        {
            throw new ArgumentException("A rulebase link belongs to another gateway.", nameof(link));
        }
        if (link.NextRulebaseId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(link), "A target rulebase ID must be positive.");
        }
        if (!link.IsInitial && !link.FromRulebaseId.HasValue && !link.FromRuleId.HasValue)
        {
            throw new ArgumentException("A non-initial link requires a source identity.", nameof(link));
        }
        return new RulebaseGraphLink(
            link.FromRulebaseId,
            link.FromRuleId,
            link.NextRulebaseId,
            link.LinkType,
            link.IsInitial,
            link.IsGlobal,
            link.IsSection);
    }

    /// <summary>
    /// Ensures every copied link endpoint belongs to the supplied graph.
    /// </summary>
    private static void ValidateLinkEndpoints(
        IReadOnlyCollection<RulebaseGraphLink> links,
        Dictionary<long, RulebaseGraphNode> rulebasesById,
        Dictionary<long, RulebaseGraphRule> rulesById,
        string parameterName)
    {
        foreach (RulebaseGraphLink link in links)
        {
            if (!rulebasesById.ContainsKey(link.TargetRulebaseId) ||
                link.SourceRulebaseId.HasValue && !rulebasesById.ContainsKey(link.SourceRulebaseId.Value) ||
                link.SourceRuleId.HasValue && !rulesById.ContainsKey(link.SourceRuleId.Value))
            {
                throw new ArgumentException("A rulebase link endpoint is missing from the graph.", parameterName);
            }
            if (link.SourceRulebaseId.HasValue && link.SourceRuleId.HasValue &&
                !rulebasesById[link.SourceRulebaseId.Value].Rules.Any(rule => rule.Id == link.SourceRuleId.Value))
            {
                throw new ArgumentException("A rule link source does not belong to its source rulebase.", parameterName);
            }
        }
    }

    /// <summary>
    /// Rejects duplicate identifiers without applying normalization.
    /// </summary>
    private static void EnsureUnique<T>(IEnumerable<T> identities, string identityKind, string parameterName)
        where T : notnull
    {
        HashSet<T> seen = [];
        if (identities.Any(identity => !seen.Add(identity)))
        {
            throw new ArgumentException($"A rulebase graph contains a duplicate {identityKind}.", parameterName);
        }
    }

    /// <summary>
    /// Returns one nonblank ordinal identity.
    /// </summary>
    private static string RequireIdentity(string? value, string identityKind, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"A {identityKind} must not be empty.", parameterName);
        }
        return value;
    }
}

/// <summary>
/// Immutable normalized rulebase node and its ordered structural rules.
/// </summary>
public sealed class RulebaseGraphNode
{
    public long Id { get; }
    public string Uid { get; }
    public string Name { get; }
    public IReadOnlyList<RulebaseGraphRule> Rules { get; }

    internal RulebaseGraphNode(
        long id,
        string uid,
        string name,
        IEnumerable<RulebaseGraphRule> rules)
    {
        Id = id;
        Uid = uid;
        Name = name;
        Rules = new ReadOnlyCollection<RulebaseGraphRule>(rules.ToList());
    }
}

/// <summary>
/// Immutable structural rule identity and native order.
/// </summary>
public sealed class RulebaseGraphRule
{
    public long Id { get; }
    public string Uid { get; }
    public double NativeOrder { get; }

    internal RulebaseGraphRule(long id, string uid, double nativeOrder)
    {
        Id = id;
        Uid = uid;
        NativeOrder = nativeOrder;
    }
}

/// <summary>
/// Immutable normalized relationship between rulebases or from a rule to an inline rulebase.
/// </summary>
public sealed class RulebaseGraphLink
{
    public long? SourceRulebaseId { get; }
    public long? SourceRuleId { get; }
    public long TargetRulebaseId { get; }
    public int LinkType { get; }
    public bool IsInitial { get; }
    public bool IsGlobal { get; }
    public bool IsSection { get; }

    internal RulebaseGraphLink(
        long? sourceRulebaseId,
        long? sourceRuleId,
        long targetRulebaseId,
        int linkType,
        bool isInitial,
        bool isGlobal,
        bool isSection)
    {
        SourceRulebaseId = sourceRulebaseId;
        SourceRuleId = sourceRuleId;
        TargetRulebaseId = targetRulebaseId;
        LinkType = linkType;
        IsInitial = isInitial;
        IsGlobal = isGlobal;
        IsSection = isSection;
    }
}

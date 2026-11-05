namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>Normalized service scope imported from one Check Point LayerGuard.</summary>
public sealed class CheckPointGuardServiceScope
{
    /// <summary>Gets whether the imported terms are excluded instead of included.</summary>
    public bool IsNegated { get; }

    /// <summary>Gets the resolved service terms.</summary>
    public IReadOnlyList<PositioningService> Terms { get; }

    /// <summary>Gets whether at least one service term could not be resolved.</summary>
    public bool HasUnresolvedTerms { get; }

    /// <summary>Creates a defensively copied LayerGuard service scope.</summary>
    public CheckPointGuardServiceScope(
        bool isNegated,
        IEnumerable<PositioningService> terms,
        bool hasUnresolvedTerms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        PositioningService[] copied = terms.ToArray();
        if (copied.Any(term => term is null))
        {
            throw new ArgumentException("Service terms must not contain null entries.", nameof(terms));
        }
        IsNegated = isNegated;
        Terms = Array.AsReadOnly(copied);
        HasUnresolvedTerms = hasUnresolvedTerms;
    }
}

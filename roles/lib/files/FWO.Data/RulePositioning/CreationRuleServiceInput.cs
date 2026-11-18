namespace FWO.Data.RulePositioning;

/// <summary>Immutable service requirements of a rule-creation request.</summary>
public sealed class CreationRuleServiceInput
{
    /// <summary>Gets the requested service definitions.</summary>
    public IReadOnlyList<PositioningService> Services { get; }

    /// <summary>Creates a defensively copied, non-empty service input.</summary>
    public CreationRuleServiceInput(IEnumerable<PositioningService> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        PositioningService[] copied = services.ToArray();
        if (copied.Length == 0)
        {
            throw new ArgumentException("At least one requested service is required.", nameof(services));
        }
        if (copied.Any(service => service is null))
        {
            throw new ArgumentException("Requested services must not contain null entries.", nameof(services));
        }
        Services = Array.AsReadOnly(copied);
    }
}

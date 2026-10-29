namespace FWO.Data.RulePositioning;

/// <summary>
/// Stable categories returned when rule-position calculation cannot succeed.
/// </summary>
public enum RulePositioningFailureCategory
{
    InvalidInput,
    UnsupportedTask,
    ConfigurationInvalid,
    PositioningMethodNotImplemented,
    StrategyRegistrationAmbiguous,
    CalculationFailed
}

/// <summary>
/// Base type for explicit rule-position calculation outcomes.
/// </summary>
public abstract class RulePositioningResult
{
    private protected RulePositioningResult()
    { }
}

/// <summary>
/// Successful rule-position calculation outcome.
/// </summary>
public sealed class RulePositioningSuccess : RulePositioningResult
{
    public RulePosition Position { get; }

    /// <summary>
    /// Creates a successful result containing a calculated position.
    /// </summary>
    public RulePositioningSuccess(RulePosition position)
    {
        Position = position ?? throw new ArgumentNullException(nameof(position));
    }
}

/// <summary>
/// Failed rule-position calculation outcome with a safe, category-specific diagnostic.
/// </summary>
public sealed class RulePositioningFailure : RulePositioningResult
{
    public RulePositioningFailureCategory Category { get; }
    public string Diagnostic { get; }

    /// <summary>
    /// Creates a failed result without accepting potentially sensitive caller content.
    /// </summary>
    public RulePositioningFailure(RulePositioningFailureCategory category)
    {
        Category = category;
        Diagnostic = DiagnosticFor(category);
    }

    /// <summary>
    /// Maps each stable failure category to a safe caller-facing diagnostic.
    /// </summary>
    private static string DiagnosticFor(RulePositioningFailureCategory category)
    {
        return category switch
        {
            RulePositioningFailureCategory.InvalidInput => "The rule-positioning input is invalid.",
            RulePositioningFailureCategory.UnsupportedTask => "The task is not supported for rule positioning.",
            RulePositioningFailureCategory.ConfigurationInvalid => "The rule-positioning configuration is invalid.",
            RulePositioningFailureCategory.PositioningMethodNotImplemented =>
                "No rule-positioning method is implemented for this target and algorithm.",
            RulePositioningFailureCategory.StrategyRegistrationAmbiguous =>
                "More than one rule-positioning method matches this target and algorithm.",
            RulePositioningFailureCategory.CalculationFailed => "The rule position could not be calculated.",
            _ => "The rule position could not be calculated."
        };
    }
}

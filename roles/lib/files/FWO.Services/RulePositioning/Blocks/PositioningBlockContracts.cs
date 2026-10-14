namespace FWO.Services.RulePositioning.Blocks;

/// <summary>
/// Identifies the semantic value produced by a positioning block.
/// </summary>
public readonly record struct PositioningBlockRole
{
    public string Name { get; }

    /// <summary>
    /// Creates a stable block-role identifier.
    /// </summary>
    public PositioningBlockRole(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A positioning-block role must not be empty.", nameof(name));
        }
        Name = name;
    }

    public override string ToString() => Name;
}

/// <summary>
/// A side-effect-free node in an in-memory positioning recipe.
/// </summary>
public interface IPositioningBlock<TContext, TOutput>
{
    string Kind { get; }
    PositioningBlockRole OutputRole { get; }
    IReadOnlyList<IPositioningBlock<TContext, TOutput>> Children { get; }
    IReadOnlyList<PositioningBlockRole> RequiredChildRoles { get; }

    /// <summary>
    /// Evaluates this block after all child values have been produced successfully.
    /// </summary>
    Task<PositioningBlockResult<TOutput>> EvaluateAsync(
        TContext context,
        IReadOnlyList<TOutput> childValues,
        CancellationToken cancellationToken);
}

/// <summary>
/// Stable categories for failures inside a positioning recipe.
/// </summary>
public enum PositioningBlockFailureCategory
{
    InvalidRecipe,
    InvalidSnapshot,
    ScopeNotFound,
    ScopeAmbiguous,
    NoInsertionPoint,
    UnexpectedExecutionFailure
}

/// <summary>
/// Base type for an explicit block evaluation outcome.
/// </summary>
public abstract class PositioningBlockResult<TOutput>
{
    private protected PositioningBlockResult()
    { }
}

/// <summary>
/// Successful block evaluation outcome.
/// </summary>
public sealed class PositioningBlockSuccess<TOutput> : PositioningBlockResult<TOutput>
{
    public TOutput Value { get; }

    /// <summary>
    /// Creates a successful block result.
    /// </summary>
    public PositioningBlockSuccess(TOutput value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}

/// <summary>
/// Failed block evaluation outcome with a safe diagnostic and its originating block kind.
/// </summary>
public sealed class PositioningBlockFailure<TOutput> : PositioningBlockResult<TOutput>
{
    public PositioningBlockFailureCategory Category { get; }
    public string OriginatingBlockKind { get; }
    public string Diagnostic { get; }

    /// <summary>
    /// Creates a typed failure without accepting caller or exception content.
    /// </summary>
    public PositioningBlockFailure(
        PositioningBlockFailureCategory category,
        string originatingBlockKind)
    {
        if (string.IsNullOrWhiteSpace(originatingBlockKind))
        {
            throw new ArgumentException("An originating block kind is required.", nameof(originatingBlockKind));
        }
        Category = category;
        OriginatingBlockKind = originatingBlockKind;
        Diagnostic = DiagnosticFor(category);
    }

    /// <summary>
    /// Returns a category-specific diagnostic that never contains evaluated data.
    /// </summary>
    private static string DiagnosticFor(PositioningBlockFailureCategory category)
    {
        return category switch
        {
            PositioningBlockFailureCategory.InvalidRecipe => "The positioning recipe is invalid.",
            PositioningBlockFailureCategory.InvalidSnapshot => "The policy snapshot is invalid for this calculation.",
            PositioningBlockFailureCategory.ScopeNotFound => "The requested positioning scope was not found.",
            PositioningBlockFailureCategory.ScopeAmbiguous => "The requested positioning scope is ambiguous.",
            PositioningBlockFailureCategory.NoInsertionPoint => "No valid insertion point exists in the requested scope.",
            PositioningBlockFailureCategory.UnexpectedExecutionFailure => "The positioning block could not be evaluated.",
            _ => "The positioning block could not be evaluated."
        };
    }
}

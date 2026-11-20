namespace FWO.Basics;

/// <summary>
/// Fixed failure categories for effective request-state validation and persistence.
/// </summary>
public enum EffectiveRequestStateFailureCategory
{
    InvalidTargetTask,
    DuplicateTask,
    MissingReference,
    CrossTicketReference,
    LaterReference,
    WrongReferenceType,
    InvalidRequestedElement,
    UnidentifiableReference,
    NestedGroupReference,
    PersistenceFailure,
    ReadbackFailure,
    ReadbackMismatch
}

/// <summary>
/// Reports a fail-closed effective request-state validation or persistence failure.
/// </summary>
public sealed class EffectiveRequestStateMaterializationException : InvalidOperationException
{
    /// <summary>
    /// Gets the fixed failure category without exposing request payload data.
    /// </summary>
    public EffectiveRequestStateFailureCategory Category { get; }

    /// <summary>
    /// Creates a categorized materialization failure.
    /// </summary>
    /// <param name="category">The fixed failure category.</param>
    public EffectiveRequestStateMaterializationException(EffectiveRequestStateFailureCategory category)
        : base($"Effective request state materialization failed: {category}.")
    {
        Category = category;
    }

    /// <summary>
    /// Creates a categorized materialization failure caused by an underlying operation.
    /// </summary>
    /// <param name="category">The fixed failure category.</param>
    /// <param name="innerException">The underlying failure.</param>
    public EffectiveRequestStateMaterializationException(EffectiveRequestStateFailureCategory category,
        Exception innerException)
        : base($"Effective request state materialization failed: {category}.", innerException)
    {
        Category = category;
    }
}

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the supported request validation field shapes.
/// </summary>
public enum RequestValidationFieldKind
{
    /// <summary>
    /// Represents a JSON object.
    /// </summary>
    Object,

    /// <summary>
    /// Represents a JSON string.
    /// </summary>
    String,

    /// <summary>
    /// Represents a JSON number bound to an integer.
    /// </summary>
    Int,

    /// <summary>
    /// Represents a JSON boolean.
    /// </summary>
    Bool,

    /// <summary>
    /// Represents a JSON array.
    /// </summary>
    List
}

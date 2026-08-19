namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Describes one accepted request field.
/// </summary>
public sealed record RequestValidationField(
    string JsonName,
    RequestValidationFieldKind Kind,
    bool Required,
    RequestValidationSchema? NestedSchema);

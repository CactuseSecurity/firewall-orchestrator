namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Collects request validation errors by canonical field path.
/// </summary>
public sealed class RequestValidationErrors
{
    private readonly Dictionary<string, List<string>> errors = [];

    /// <summary>
    /// Gets a value indicating whether the collection contains any validation errors.
    /// </summary>
    public bool HasErrors => errors.Count > 0;

    /// <summary>
    /// Adds a validation error for the supplied field path.
    /// </summary>
    public void Add(string fieldPath, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!errors.TryGetValue(fieldPath, out List<string>? fieldErrors))
        {
            fieldErrors = [];
            errors[fieldPath] = fieldErrors;
        }

        fieldErrors.Add(message);
    }

    /// <summary>
    /// Adds an error for an unknown request field.
    /// </summary>
    public void AddUnknownField(string fieldPath)
    {
        Add(fieldPath, $"Unknown field '{fieldPath}'.");
    }

    /// <summary>
    /// Adds an error for a missing required request field.
    /// </summary>
    public void AddMissingRequiredField(string fieldPath)
    {
        Add(fieldPath, $"Required field '{fieldPath}' is missing.");
    }

    /// <summary>
    /// Converts the collection to the dictionary shape used by <see cref="Microsoft.AspNetCore.Mvc.ValidationProblemDetails"/>.
    /// </summary>
    public Dictionary<string, string[]> ToDictionary()
    {
        return errors.ToDictionary(error => error.Key, error => error.Value.ToArray());
    }
}

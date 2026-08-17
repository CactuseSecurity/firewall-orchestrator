namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Builds canonical field paths for request validation errors.
/// </summary>
public static class RequestFieldPath
{
    /// <summary>
    /// Represents the whole request body.
    /// </summary>
    public const string Root = "$";

    /// <summary>
    /// Appends a child field to the supplied parent path.
    /// </summary>
    public static string Child(string parentPath, string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (string.IsNullOrWhiteSpace(parentPath) || parentPath == Root)
        {
            return fieldName;
        }

        return $"{parentPath}.{fieldName}";
    }

    /// <summary>
    /// Appends a list index to the supplied parent path.
    /// </summary>
    public static string Indexed(string parentPath, int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentPath);

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Request validation indexes must not be negative.");
        }

        return $"{parentPath}[{index}]";
    }
}

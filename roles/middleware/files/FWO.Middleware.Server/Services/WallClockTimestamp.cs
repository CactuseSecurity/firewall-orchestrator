namespace FWO.Middleware.Server.Services;

/// <summary>
/// Compares timestamps read from timezone-naive database columns against filter values supplied by a
/// REST caller.
/// </summary>
/// <remarks>
/// A timezone-naive column carries the wall clock of the installation and no offset, so a direct
/// comparison would depend on which spelling the caller happened to use: DateTime equality compares
/// ticks and ignores the kind, while the request deserializer leaves a trailing Z unshifted but
/// converts an explicit offset to local time. Both sides are therefore reduced to the same wall clock
/// before they are compared.
/// </remarks>
public static class WallClockTimestamp
{
    /// <summary>
    /// Determines whether a stored timestamp matches an optional filter value.
    /// </summary>
    /// <param name="value">Timestamp as read from the database.</param>
    /// <param name="expected">Filter value as bound from the request; null applies no restriction.</param>
    /// <returns>True when no filter value is given or both describe the same wall clock.</returns>
    public static bool Matches(DateTime? value, DateTime? expected)
    {
        return expected == null || NormalizeStored(value) == NormalizeFilter(expected.Value);
    }

    /// <summary>
    /// Reduces a filter timestamp to the wall clock the stored column uses.
    /// </summary>
    /// <param name="expected">Timestamp as bound from the request.</param>
    /// <returns>The same point in time expressed as an unspecified-kind local wall clock.</returns>
    public static DateTime NormalizeFilter(DateTime expected)
    {
        return expected.Kind switch
        {
            // A trailing Z keeps UTC ticks, so it has to be moved onto the local clock the column stores.
            DateTimeKind.Utc => DateTime.SpecifyKind(expected.ToLocalTime(), DateTimeKind.Unspecified),
            // An explicit offset was already converted to local time while binding.
            DateTimeKind.Local => DateTime.SpecifyKind(expected, DateTimeKind.Unspecified),
            // No offset given: taken as the wall clock of the installation, like the stored value.
            _ => expected
        };
    }

    /// <summary>
    /// Drops the kind of a stored timestamp so it cannot depend on how the row was deserialized.
    /// </summary>
    /// <param name="value">Timestamp as read from the database.</param>
    /// <returns>The same wall clock with an unspecified kind, or null.</returns>
    public static DateTime? NormalizeStored(DateTime? value)
    {
        return value == null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
    }
}

namespace FWO.Data
{
    /// <summary>
    /// Controls how configured custom field keys are matched against the field names of a rule.
    /// </summary>
    public enum CustomFieldKeyMatching
    {
        /// <summary>
        /// Field names must match the configured key exactly.
        /// </summary>
        CaseSensitive,

        /// <summary>
        /// Field names match the configured key regardless of casing, for vendors that export
        /// the same field with inconsistent casing.
        /// </summary>
        IgnoreCase
    }
}

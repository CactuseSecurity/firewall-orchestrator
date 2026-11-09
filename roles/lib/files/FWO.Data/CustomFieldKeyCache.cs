namespace FWO.Data
{
    /// <summary>
    /// Caches the normalized custom field keys of a configured key setting for callers that resolve
    /// custom fields per rule, re-normalizing only after the configured setting changed.
    /// </summary>
    public sealed class CustomFieldKeyCache
    {
        private static readonly List<string> NoKeys = [];

        private NormalizedKeys normalizedKeys = new("", NoKeys);

        /// <summary>
        /// Returns the normalized keys for the given setting.
        /// </summary>
        /// <param name="customFieldKeys">Configured key setting.</param>
        /// <returns>The ordered custom field keys, empty when nothing usable is configured.</returns>
        public IReadOnlyList<string> GetKeys(string? customFieldKeys)
        {
            string keySetting = customFieldKeys ?? "";
            // the reference read/write keeps a concurrent config change from handing out a torn cache entry
            NormalizedKeys cachedKeys = normalizedKeys;
            if (cachedKeys.Setting != keySetting)
            {
                cachedKeys = new NormalizedKeys(keySetting, CustomFieldResolver.NormalizeCustomFieldKeys(keySetting));
                normalizedKeys = cachedKeys;
            }
            return cachedKeys.Keys;
        }

        private sealed record NormalizedKeys(string Setting, List<string> Keys);
    }
}

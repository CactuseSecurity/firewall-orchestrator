using FWO.Logging;
using System.Text.Json;

namespace FWO.Data
{
    /// <summary>
    /// Provides helpers for reading typed values from a rule's serialized custom fields.
    /// </summary>
    public static class CustomFieldResolver
    {
        /// <summary>
        /// Normalizes a configured custom field key setting into an ordered key list.
        /// Accepts a JSON array, a JSON string or a legacy plain-text key and never reports a resolver error,
        /// so an unparsable setting can never surface rule data to the caller.
        /// </summary>
        /// <param name="customFieldKeys">Configured key setting.</param>
        /// <returns>The ordered custom field keys, empty when nothing usable is configured.</returns>
        public static List<string> NormalizeCustomFieldKeys(string? customFieldKeys)
        {
            if (string.IsNullOrWhiteSpace(customFieldKeys))
            {
                return [];
            }

            string trimmedKeys = customFieldKeys.Trim();
            if (TryReadKeyList(trimmedKeys, out List<string> keyList))
            {
                return keyList;
            }
            if (TryReadSingleKey(trimmedKeys, out string singleKey))
            {
                return [singleKey];
            }
            // legacy plain-text key, kept usable instead of failing the whole lookup
            return [trimmedKeys];
        }

        /// <summary>
        /// Extracts the first matching custom field value from <paramref name="rule"/> using the keys in <paramref name="keysJson"/>.
        /// </summary>
        /// <typeparam name="T">The expected target type of the custom field value.</typeparam>
        /// <param name="rule">The rule containing the serialized custom fields.</param>
        /// <param name="keysJson">A JSON array of candidate custom field keys, or a legacy plain-text key.</param>
        /// <param name="errorMessage">Description of an unreadable custom field value, otherwise <see langword="null"/>.</param>
        /// <returns>
        /// The deserialized custom field value when a matching key is found and can be converted to <typeparamref name="T"/>;
        /// otherwise, <see langword="default"/>.
        /// </returns>
        public static T? ExtractCustomFieldValue<T>(Rule? rule, string keysJson, out string? errorMessage)
        {
            return ExtractCustomFieldValue<T>(rule, NormalizeCustomFieldKeys(keysJson), out errorMessage);
        }

        /// <summary>
        /// Extracts the first matching custom field value from <paramref name="rule"/> using already normalized keys,
        /// so callers iterating many rules normalize the configured setting only once.
        /// </summary>
        /// <typeparam name="T">The expected target type of the custom field value.</typeparam>
        /// <param name="rule">The rule containing the serialized custom fields.</param>
        /// <param name="keys">Candidate custom field keys, checked in order.</param>
        /// <param name="errorMessage">Description of an unreadable custom field value, otherwise <see langword="null"/>.</param>
        /// <returns>
        /// The deserialized custom field value when a matching key is found and can be converted to <typeparamref name="T"/>;
        /// otherwise, <see langword="default"/>.
        /// </returns>
        public static T? ExtractCustomFieldValue<T>(Rule? rule, IReadOnlyList<string> keys, out string? errorMessage)
        {
            errorMessage = null;

            if (rule == null || string.IsNullOrWhiteSpace(rule.CustomFields) || keys.Count == 0)
            {
                return default;
            }
            Rule nonNullableRule = rule;
            Dictionary<string, JsonElement> customFields;

            try
            {
                customFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(nonNullableRule.CustomFields.Replace("'", "\"")) ?? [];
            }
            catch (JsonException e)
            {
                errorMessage = $"Error while resolving custom fields. Raw Data: {nonNullableRule.CustomFields}";
                new Logger().TryWriteError("CustomFieldResolver", $"Error while resolving rule '{rule.Uid}': {e.Message}", true);
                return default;
            }

            if (customFields.Count == 0)
            {
                return default;
            }

            foreach (var key in keys)
            {
                if (!customFields.TryGetValue(key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Null ||
                    value.ValueKind == JsonValueKind.Undefined ||
                    (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                {
                    continue;
                }

                try
                {
                    errorMessage = null;
                    return value.Deserialize<T>();
                }
                catch (Exception e)
                {
                    errorMessage = $"Error while resolving custom fields. Invalid value for key '{key}'. Raw Data: {nonNullableRule.CustomFields}";
                    new Logger().TryWriteWarning("CustomFieldResolver", $"Failed to deserialize key '{key}' for rule '{nonNullableRule.Uid}' to type {typeof(T).Name}: {e.Message}", true);
                }
            }
            return default;
        }

        /// <summary>
        /// Reads a JSON array of keys, dropping blank entries.
        /// </summary>
        /// <param name="trimmedKeys">Trimmed key setting.</param>
        /// <param name="keyList">The keys read from the array.</param>
        /// <returns>True if the setting is a readable JSON array.</returns>
        private static bool TryReadKeyList(string trimmedKeys, out List<string> keyList)
        {
            keyList = [];
            if (!trimmedKeys.StartsWith('[') || !trimmedKeys.EndsWith(']'))
            {
                return false;
            }

            try
            {
                keyList = [.. (JsonSerializer.Deserialize<List<string>>(trimmedKeys) ?? [])
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim())];
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a setting holding a single JSON string key.
        /// </summary>
        /// <param name="trimmedKeys">Trimmed key setting.</param>
        /// <param name="singleKey">The key read from the setting.</param>
        /// <returns>True if the setting is a readable, non-blank JSON string.</returns>
        private static bool TryReadSingleKey(string trimmedKeys, out string singleKey)
        {
            singleKey = "";
            if (!trimmedKeys.StartsWith('"') || !trimmedKeys.EndsWith('"'))
            {
                return false;
            }

            try
            {
                singleKey = JsonSerializer.Deserialize<string>(trimmedKeys)?.Trim() ?? "";
                return singleKey.Length > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}

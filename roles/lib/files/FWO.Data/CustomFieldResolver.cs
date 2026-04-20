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
        /// Extracts the first matching custom field value from <paramref name="rule"/> using the ordered keys in <paramref name="keysJson"/>.
        /// </summary>
        /// <typeparam name="T">The expected target type of the custom field value.</typeparam>
        /// <param name="rule">The rule containing the serialized custom fields.</param>
        /// <param name="keysJson">A JSON array of candidate custom field keys to check in order.</param>
        /// <returns>
        /// The deserialized custom field value when a matching key is found and can be converted to <typeparamref name="T"/>;
        /// otherwise, <see langword="default"/>.
        /// </returns>
        public static T? ExtractCustomFieldValue<T>(Rule? rule, string keysJson, out string? errorMessage)
        {
            errorMessage = null;

            if (rule == null || string.IsNullOrWhiteSpace(rule.CustomFields) || string.IsNullOrWhiteSpace(keysJson))
            {
                return default;
            }

            Dictionary<string, JsonElement> customFields;
            List<string> keysList;

            try
            {
                customFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rule.CustomFields.Replace("'", "\"")) ?? new Dictionary<string, JsonElement>();
                keysList = JsonSerializer.Deserialize<List<string>>(keysJson) ?? new List<string>();
            }
            catch (JsonException e)
            {
                errorMessage = $"Error while resolving custom fields. Raw Data: {rule.CustomFields}";
                new Logger().TryWriteError("CustomFieldResolver", $"Error while resolving rule '{rule.Uid}': {e.Message}", true);
                return default;
            }

            if (customFields.Count == 0 || keysList.Count == 0)
            {
                return default;
            }

            foreach (var key in keysList)
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
                    errorMessage = $"Error while resolving custom fields. Invalid value for key '{key}'. Raw Data: {rule?.CustomFields}";
                    new Logger().TryWriteWarning("CustomFieldResolver", $"Failed to deserialize key '{key}' for rule '{rule?.Uid}' to type {typeof(T).Name}: {e.Message}", true);
                    continue;
                }
            }
            return default;
        }
    }
}

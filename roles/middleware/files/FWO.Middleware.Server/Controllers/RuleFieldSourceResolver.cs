using System.Text.Json;
using FWO.Data;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Resolves response fields from database-backed or custom-field-backed rule data.
    /// </summary>
    public static class RuleFieldSourceResolver
    {
        /// <summary>
        /// Default fallback text used when a requested value cannot be resolved from the database or custom fields.
        /// </summary>
        public const string NotFoundValue = "Not Found in Database";

        /// <summary>
        /// Gets the owner information source that should be used for a rule response.
        /// </summary>
        public static FieldSource ResolveOwnerInformationSource(FieldSourceMapping? fieldSourceMapping)
        {
            return fieldSourceMapping?.OwnerInformation ?? FieldSource.Database;
        }

        /// <summary>
        /// Gets the change-id source that should be used for a rule response.
        /// </summary>
        public static FieldSource ResolveChangeIdSource(FieldSourceMapping? fieldSourceMapping)
        {
            return fieldSourceMapping?.ChangeId ?? FieldSource.CustomField;
        }

        /// <summary>
        /// Resolves the owner information display value.
        /// </summary>
        public static string ResolveOwnerInformation(Rule rule, FieldSource source, string customFieldKey, string notFoundValue)
        {
            return source switch
            {
                FieldSource.Database => rule.RuleOwner.FirstOrDefault()?.OwnerId.ToString() ?? notFoundValue,
                FieldSource.CustomField => ExtractCustomFieldValue(rule, customFieldKey) ?? notFoundValue,
                _ => notFoundValue
            };
        }

        /// <summary>
        /// Resolves the change-id display value.
        /// </summary>
        public static string ResolveChangeId(Rule rule, FieldSource source, string customFieldKey, string notFoundValue)
        {
            return source switch
            {
                FieldSource.Database => notFoundValue,
                FieldSource.CustomField => ExtractCustomFieldValue(rule, customFieldKey) ?? notFoundValue,
                _ => notFoundValue
            };
        }

        private static string? ExtractCustomFieldValue(Rule rule, string customFieldKey)
        {
            string? keysJson = NormalizeCustomFieldKeys(customFieldKey);
            if (string.IsNullOrWhiteSpace(keysJson))
            {
                return null;
            }

            return CustomFieldResolver.ExtractCustomFieldValue<string>(rule, keysJson, out _);
        }

        private static string? NormalizeCustomFieldKeys(string customFieldKey)
        {
            if (string.IsNullOrWhiteSpace(customFieldKey))
            {
                return null;
            }

            string trimmed = customFieldKey.Trim();

            try
            {
                string[]? keyArray = JsonSerializer.Deserialize<string[]>(trimmed);
                if (keyArray is not null)
                {
                    List<string> cleanedKeys = keyArray
                        .Where(key => !string.IsNullOrWhiteSpace(key))
                        .Select(key => key.Trim())
                        .ToList();

                    if (cleanedKeys.Count > 0)
                    {
                        return JsonSerializer.Serialize(cleanedKeys);
                    }

                    return null;
                }
            }
            catch (JsonException)
            {
                // Fall back to treating the value as a single raw key.
            }

            try
            {
                string? singleKey = JsonSerializer.Deserialize<string>(trimmed);
                if (!string.IsNullOrWhiteSpace(singleKey))
                {
                    return JsonSerializer.Serialize(new[] { singleKey.Trim() });
                }
            }
            catch (JsonException)
            {
                // Fall back to treating the value as a single raw key.
            }

            return JsonSerializer.Serialize(new[] { trimmed });
        }
    }
}

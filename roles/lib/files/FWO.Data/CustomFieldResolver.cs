using FWO.Logging;
using System.Text.Json;

namespace FWO.Data
{
    public static class CustomFieldResolver
    {
        public static T? ExtractCustomFieldValue<T>(Rule rule, string keysJson)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.CustomFields) || string.IsNullOrWhiteSpace(keysJson))
            {
                return default;
            }

            Dictionary<string, JsonElement>? customFields = null;
            List<string> keysList;

            try
            {
                customFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rule.CustomFields.Replace("'", "\""));
                keysList = JsonSerializer.Deserialize<List<string>>(keysJson) ?? new List<string>();
            }
            catch
            {
                return default;
            }

            if (customFields == null || keysList.Count == 0)
            {
                return default;
            }

            if (customFields.Values.All(v =>
                    v.ValueKind == JsonValueKind.Null ||
                    v.ValueKind == JsonValueKind.Undefined ||
                    (v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString()))
                ))
            {
                return default;
            }

            foreach (var key in keysList)
            {
                if (customFields.TryGetValue(key, out var value))
                {
                    try
                    {
                        return value.Deserialize<T>();
                    }
                    catch
                    {
                        return default;
                    }
                }
            }
            return default;
        }
    }
}

using FWO.Logging;
using System.Globalization;
using System.Text.Json;

namespace FWO.Config.File
{
    /// <summary>
    /// Reads optional values of the config file that operators may edit by hand. A value of the wrong type is logged
    /// and ignored, so the component falls back to its default instead of refusing to start.
    /// </summary>
    internal static class ConfigValueParser
    {
        private const string kLogTitle = "Config file";
        private const char kListSeparator = ',';
        private const string kJsonListStart = "[";

        /// <summary>
        /// Reads a whole number, given as a number or as a string holding one.
        /// </summary>
        /// <param name="value">the raw value from the config file, null if the key is missing</param>
        /// <param name="key">the key of the value, for the log</param>
        /// <returns>the number, or null if the value is missing, empty or not a whole number</returns>
        public static int? ReadOptionalInt(JsonElement? value, string key)
        {
            if (value == null || IsEmpty(value.Value))
            {
                return null;
            }
            JsonElement element = value.Value;
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int number))
            {
                return number;
            }
            if (element.ValueKind == JsonValueKind.String
                && int.TryParse(element.GetString()!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
            LogIgnoredValue(key, element, "a whole number");
            return null;
        }

        /// <summary>
        /// Reads a list of strings, given as a list, a comma separated string or a string holding a JSON list.
        /// The entries are trimmed, empty and repeated entries are dropped.
        /// </summary>
        /// <param name="value">the raw value from the config file, null if the key is missing</param>
        /// <param name="key">the key of the value, for the log</param>
        /// <returns>the entries, or null if the value is missing, empty or neither a list nor a string</returns>
        public static List<string>? ReadOptionalStringList(JsonElement? value, string key)
        {
            if (value == null || IsEmpty(value.Value))
            {
                return null;
            }
            JsonElement element = value.Value;
            return element.ValueKind switch
            {
                JsonValueKind.Array => ReadArray(element, key),
                JsonValueKind.String => ReadListText(element.GetString()!, key),
                _ => LogIgnoredList(key, element)
            };
        }

        /// <summary>
        /// Reads a comma separated string or a string holding a JSON list.
        /// </summary>
        private static List<string>? ReadListText(string text, string key)
        {
            string trimmedText = text.Trim();
            if (!trimmedText.StartsWith(kJsonListStart, StringComparison.Ordinal))
            {
                return Normalize(trimmedText.Split(kListSeparator));
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmedText);
                return ReadArray(document.RootElement, key);
            }
            catch (JsonException)
            {
                Log.WriteWarning(kLogTitle, $"Ignoring {key}: the value starts like a JSON list, but is none.");
                return null;
            }
        }

        /// <summary>
        /// Reads the string and number entries of a JSON list; other entries are logged and skipped.
        /// </summary>
        private static List<string> ReadArray(JsonElement array, string key)
        {
            List<string> entries = [];
            foreach (JsonElement entry in array.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    entries.Add(entry.GetString()!);
                }
                else if (entry.ValueKind == JsonValueKind.Number)
                {
                    entries.Add(entry.GetRawText());
                }
                else
                {
                    LogIgnoredValue(key, entry, "a string entry");
                }
            }
            return Normalize(entries);
        }

        /// <summary>
        /// Trims the entries and drops empty and repeated ones.
        /// </summary>
        private static List<string> Normalize(IEnumerable<string> entries)
        {
            return entries.Select(entry => entry.Trim()).Where(entry => entry.Length > 0).Distinct().ToList();
        }

        /// <summary>
        /// Tells whether a value counts as not configured: null or a blank string.
        /// </summary>
        private static bool IsEmpty(JsonElement element)
        {
            return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString()));
        }

        /// <summary>
        /// Logs that a list value is ignored.
        /// </summary>
        /// <returns>always null</returns>
        private static List<string>? LogIgnoredList(string key, JsonElement element)
        {
            LogIgnoredValue(key, element, "a list or a comma separated string");
            return null;
        }

        /// <summary>
        /// Logs that a value is ignored, naming the key and the type found (not the value itself).
        /// </summary>
        private static void LogIgnoredValue(string key, JsonElement element, string expected)
        {
            Log.WriteWarning(kLogTitle, $"Ignoring {key}: expected {expected}, found {element.ValueKind}.");
        }
    }
}

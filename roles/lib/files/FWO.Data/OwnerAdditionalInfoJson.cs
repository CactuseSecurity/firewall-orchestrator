using System.Linq;
using System.Text.Json;

namespace FWO.Data
{
    /// <summary>
    /// Converts owner additional information dictionaries to and from editable JSON text.
    /// </summary>
    public static class OwnerAdditionalInfoJson
    {
        private static readonly JsonSerializerOptions kJsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Serializes owner additional information for UI editing.
        /// </summary>
        /// <param name="additionalInfo">Additional owner metadata.</param>
        /// <returns>Indented JSON text or an empty string when no data is present.</returns>
        public static string Serialize(Dictionary<string, string>? additionalInfo)
        {
            if (additionalInfo == null || additionalInfo.Count == 0)
            {
                return "";
            }

            return JsonSerializer.Serialize(additionalInfo, kJsonOptions);
        }

        /// <summary>
        /// Tries to parse owner additional information from editable JSON text.
        /// </summary>
        /// <param name="json">JSON text entered by the user.</param>
        /// <param name="additionalInfo">Parsed metadata dictionary or null when the input is empty.</param>
        /// <returns>True when parsing succeeded, otherwise false.</returns>
        public static bool TryDeserialize(string? json, out Dictionary<string, string>? additionalInfo)
        {
            additionalInfo = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                Dictionary<string, string?> parsedAdditionalInfo =
                    JsonSerializer.Deserialize<Dictionary<string, string?>>(json, kJsonOptions) ?? [];
                additionalInfo = parsedAdditionalInfo.ToDictionary(entry => entry.Key, entry => entry.Value ?? "");
                return true;
            }
            catch (JsonException)
            {
                additionalInfo = null;
                return false;
            }
        }
    }
}

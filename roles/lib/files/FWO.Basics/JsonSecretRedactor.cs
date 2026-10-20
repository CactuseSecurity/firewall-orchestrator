using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Basics
{
    /// <summary>
    /// Masks the values of secret-bearing properties (secret, password, private key) in JSON text, so that
    /// data written to alerts and logs never carries a credential in clear text.
    /// </summary>
    public static class JsonSecretRedactor
    {
        /// <summary>
        /// Value that replaces a masked secret.
        /// </summary>
        public const string kRedactedValue = "***";

        private const int kMaxDepth = 100;

        private static readonly HashSet<string> kSecretPropertyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "secret",
            "password",
            "private_key"
        };

        /// <summary>
        /// Returns the JSON text with the values of all secret-bearing properties masked, at any nesting level.
        /// </summary>
        /// <param name="jsonText">JSON text, or any other text.</param>
        /// <returns>The masked JSON text. Text that is not JSON or holds no secret is returned unchanged.</returns>
        public static string Redact(string jsonText)
        {
            JsonNode? rootNode;
            try
            {
                rootNode = JsonNode.Parse(jsonText);
            }
            catch (JsonException)
            {
                return jsonText;
            }
            return RedactNode(rootNode, 0) ? rootNode!.ToJsonString() : jsonText;
        }

        /// <summary>
        /// Masks secret-bearing properties below the given node.
        /// </summary>
        /// <returns>True if a value was masked.</returns>
        private static bool RedactNode(JsonNode? node, int depth)
        {
            if (depth > kMaxDepth)
            {
                return false;
            }
            return node switch
            {
                JsonObject jsonObject => RedactObject(jsonObject, depth),
                JsonArray jsonArray => RedactArray(jsonArray, depth),
                _ => false
            };
        }

        private static bool RedactObject(JsonObject jsonObject, int depth)
        {
            bool redacted = false;
            List<string> propertyNames = [.. jsonObject.Select(property => property.Key)];
            foreach (string propertyName in propertyNames)
            {
                if (kSecretPropertyNames.Contains(propertyName))
                {
                    if (jsonObject[propertyName] != null)
                    {
                        jsonObject[propertyName] = kRedactedValue;
                        redacted = true;
                    }
                }
                else
                {
                    redacted |= RedactNode(jsonObject[propertyName], depth + 1);
                }
            }
            return redacted;
        }

        private static bool RedactArray(JsonArray jsonArray, int depth)
        {
            bool redacted = false;
            foreach (JsonNode? element in jsonArray)
            {
                redacted |= RedactNode(element, depth + 1);
            }
            return redacted;
        }
    }
}

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

        /// <summary>
        /// Nesting depth up to which the JSON is searched for secrets. Deeper objects and arrays are masked as a whole.
        /// </summary>
        public const int kMaxDepth = 100;

        // The parser is not recursive, so it may accept any depth; only the search below is bounded by kMaxDepth.
        // With the default parser limit of 64, deeper JSON would fail to parse and be returned unmasked.
        private static readonly JsonDocumentOptions kParseOptions = new() { MaxDepth = int.MaxValue };

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
        /// <returns>The masked JSON text. Text that is not JSON or holds no secret is returned unchanged. JSON that
        /// cannot be searched (duplicate property names) is replaced by <see cref="kRedactedValue"/> as a whole.</returns>
        public static string Redact(string jsonText)
        {
            try
            {
                JsonNode? rootNode = JsonNode.Parse(jsonText, null, kParseOptions);
                return RedactNode(rootNode, 0) ? rootNode!.ToJsonString() : jsonText;
            }
            catch (JsonException)
            {
                return jsonText;
            }
            catch (ArgumentException)
            {
                // JsonObject rejects duplicate property names, so a secret in such JSON cannot be masked selectively
                return kRedactedValue;
            }
        }

        /// <summary>
        /// Masks secret-bearing properties below the given node.
        /// </summary>
        /// <returns>True if a value was masked.</returns>
        private static bool RedactNode(JsonNode? node, int depth)
        {
            return node switch
            {
                JsonObject jsonObject => RedactObject(jsonObject, depth),
                JsonArray jsonArray => RedactArray(jsonArray, depth),
                _ => false
            };
        }

        /// <summary>
        /// Masks the secret-bearing properties of the object and searches its other values.
        /// </summary>
        /// <returns>True if a value was masked.</returns>
        private static bool RedactObject(JsonObject jsonObject, int depth)
        {
            bool redacted = false;
            List<string> propertyNames = [.. jsonObject.Select(property => property.Key)];
            foreach (string propertyName in propertyNames)
            {
                JsonNode? value = jsonObject[propertyName];
                if ((kSecretPropertyNames.Contains(propertyName) && value != null) || IsBeyondMaxDepth(value, depth))
                {
                    jsonObject[propertyName] = kRedactedValue;
                    redacted = true;
                }
                else
                {
                    redacted |= RedactNode(value, depth + 1);
                }
            }
            return redacted;
        }

        /// <summary>
        /// Searches the elements of the array.
        /// </summary>
        /// <returns>True if a value was masked.</returns>
        private static bool RedactArray(JsonArray jsonArray, int depth)
        {
            bool redacted = false;
            for (int index = 0; index < jsonArray.Count; index++)
            {
                if (IsBeyondMaxDepth(jsonArray[index], depth))
                {
                    jsonArray[index] = kRedactedValue;
                    redacted = true;
                }
                else
                {
                    redacted |= RedactNode(jsonArray[index], depth + 1);
                }
            }
            return redacted;
        }

        /// <summary>
        /// Checks whether a child of a node at the given depth is an object or array too deep to be searched.
        /// </summary>
        private static bool IsBeyondMaxDepth(JsonNode? child, int parentDepth)
        {
            return parentDepth + 1 > kMaxDepth && child is JsonObject or JsonArray;
        }
    }
}

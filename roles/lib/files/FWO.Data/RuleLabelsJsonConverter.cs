using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FWO.Data
{
    /// <summary>
    /// Converts the labels of a rule side, stored as {"label-key": "label-value", ...}. A key with several values
    /// holds an array of them instead of a single string, so that the labels read from the database are written back
    /// in exactly the same shape.
    /// </summary>
    public class RuleLabelsJsonConverter : JsonConverter<Dictionary<string, List<string>>>
    {
        /// <summary>
        /// Reads a label object, accepting a string or an array of strings per key.
        /// </summary>
        public override Dictionary<string, List<string>>? ReadJson(JsonReader reader, Type objectType, Dictionary<string, List<string>>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            if (JToken.Load(reader) is not JObject labels)
            {
                throw new JsonSerializationException("Rule labels must be a JSON object.");
            }
            Dictionary<string, List<string>> result = [];
            foreach (JProperty label in labels.Properties())
            {
                result[label.Name] = label.Value.Type == JTokenType.Array
                    ? label.Value.Values<string>().OfType<string>().ToList()
                    : new List<string> { label.Value.Value<string>() ?? "" };
            }
            return result;
        }

        /// <summary>
        /// Writes a label object, a single value as string and several values as array.
        /// </summary>
        public override void WriteJson(JsonWriter writer, Dictionary<string, List<string>>? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            writer.WriteStartObject();
            foreach (KeyValuePair<string, List<string>> label in value)
            {
                writer.WritePropertyName(label.Key);
                if (label.Value.Count == 1)
                {
                    writer.WriteValue(label.Value[0]);
                    continue;
                }
                writer.WriteStartArray();
                foreach (string labelValue in label.Value)
                {
                    writer.WriteValue(labelValue);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }
}

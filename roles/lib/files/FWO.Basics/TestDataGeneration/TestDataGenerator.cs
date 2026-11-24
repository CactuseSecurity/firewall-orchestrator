using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Basics.TestDataGeneration
{
    public class TestDataGenerator<T> where T : class, new()
    {
        public TestDataGenerationResult<T> ImportInstance(string json)
        {
            TestDataGenerationResult<T> result = new();

            try
            {
                T? generatedInstance = DeserializeFromJson<T>(json);

                if (generatedInstance != null)
                {
                    result.SingleInstance = generatedInstance;
                    result.ProcessSuccessful = true;
                }
                else
                {
                    result.ProcessSuccessful = false;
                }
            }
            catch
            {
                result.ProcessSuccessful = false;
            }

            return result;
        }

        public TestDataGenerationResult<T> GenerateInstance(string json)
        {
            TestDataGenerationResult<T> result = new();

            try
            {
                T instance = new();
                JsonArray? configArray = JsonNode.Parse(json)?["config"]?.AsArray();
                if (configArray == null || configArray.Count == 0)
                {
                    result.ProcessSuccessful = false;
                    return result;
                }

                foreach (JsonNode? item in configArray)
                {
                    if (TryGetValueSettings(item, out string propertyName, out JsonObject? valueSettings))
                    {
                        SetRandomizedValue(instance, propertyName, valueSettings);
                    }
                }

                result.SingleInstance = instance;
                result.ProcessSuccessful = true;
                return result;
            }
            catch
            {
                result.ProcessSuccessful = false;
                return result;
            }
        }

        /// <summary>
        /// Reads a config entry of the form { "set": { "PropertyName": { "value": probability, ... } } }.
        /// </summary>
        private static bool TryGetValueSettings(JsonNode? item, out string propertyName, [NotNullWhen(true)] out JsonObject? valueSettings)
        {
            propertyName = "";
            valueSettings = null;
            if (item is not JsonObject jsonObject
                || !jsonObject.TryGetPropertyValue("set", out JsonNode? setNode)
                || setNode is not JsonObject setObject)
            {
                return false;
            }

            KeyValuePair<string, JsonNode?> kvp = setObject.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value is not JsonObject valueSettingsObject)
            {
                return false;
            }

            propertyName = kvp.Key;
            valueSettings = valueSettingsObject;
            return true;
        }

        /// <summary>
        /// Sets the property to one of the configured values, chosen randomly according to the given probabilities.
        /// </summary>
        private static void SetRandomizedValue(T instance, string propertyName, JsonObject valueSettings)
        {
            Dictionary<string, double> valuesWithProbabilities = [];
            foreach (KeyValuePair<string, JsonNode?> valueSetting in valueSettings)
            {
                if (valueSetting.Value != null)
                {
                    valuesWithProbabilities[valueSetting.Key] = valueSetting.Value.GetValue<double>();
                }
            }

            string randomizedValue = GetRandomValueByProbability(valuesWithProbabilities);

            PropertyInfo property = typeof(T).GetProperty(propertyName)
                ?? throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(T)}'.");
            if (!property.CanWrite)
            {
                throw new InvalidOperationException($"Property '{propertyName}' is read-only and cannot be set.");
            }

            object convertedValue = Convert.ChangeType(randomizedValue, property.PropertyType);
            property.SetValue(instance, convertedValue);
        }

        public void SetUpInstance(T instance, string json)
        {
            ArgumentNullException.ThrowIfNull(instance);

            try
            {
                JsonNode? rootNode = JsonNode.Parse(json);
                if (rootNode == null) return;

                var configArray = rootNode["config"]?.AsArray();
                if (configArray == null || configArray.Count == 0) return;

                var setObject = configArray[0]?["set"];
                if (setObject is not JsonObject setObjectJson) return;

                var nameNode = setObjectJson["name"];
                if (nameNode is not JsonObject nameObject) return;

                // Zufällige Auswahl eines Namens basierend auf den Keys
                var possibleNames = nameObject.Select(kvp => kvp.Key).ToList();
                if (possibleNames.Count == 0) return;

                Random random = new();
                string selectedName = possibleNames[random.Next(possibleNames.Count)];

                // Instanz mit neuen Werten konfigurieren
                var nameProperty = typeof(T).GetProperty("Name");
                if (nameProperty != null && nameProperty.PropertyType == typeof(string))
                {
                    nameProperty.SetValue(instance, selectedName);
                }
            }
            catch
            {
                // Falls Fehler auftreten, keine Änderungen an der Instanz vornehmen
            }
        }

        private static TDeserializedObject? DeserializeFromJson<TDeserializedObject>(string json)
        {
            bool isValidated = ValidateJson(json);

            if (isValidated)
            {
                TDeserializedObject? generatedInstance = JsonSerializer.Deserialize<TDeserializedObject>(json);

                if (generatedInstance != null)
                {
                    return generatedInstance;
                }
            }

            throw new JsonException($"Generated test data could not be deserialized to {typeof(TDeserializedObject).Name}.");
        }

        private static bool ValidateJson(string json)
        {
            try
            {
                JsonDocument jsonDoc = JsonDocument.Parse(json);
                JsonElement rootElement = jsonDoc.RootElement;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetRandomValueByProbability(Dictionary<string, double> valuesWithProbabilities)
        {
            Random random = new();

            // validates correct probability config
            double total = valuesWithProbabilities.Values.Sum();
            if (total != 1)
            {
                throw new InvalidOperationException("The sum of the probabilities has to be equal 1.");
            }

            double roll = random.NextDouble();
            double cumulative = 0;
            string lastKey = string.Empty;

            foreach (KeyValuePair<string, double> entry in valuesWithProbabilities)
            {
                cumulative += entry.Value;
                lastKey = entry.Key;
                if (roll <= cumulative)
                {
                    return entry.Key;
                }
            }

            return lastKey;
        }
    }

}

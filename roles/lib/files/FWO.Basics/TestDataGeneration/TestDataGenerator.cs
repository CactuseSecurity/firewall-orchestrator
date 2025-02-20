using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Basics.TestDataGeneration
{
    public class TestDataGenerator<T> where T : new()
    {
        public TestDataGenerationResult<T> ImportInstance(string json)
        {
            TestDataGenerationResult<T> result= new();

            try
            {
                T generatedInstance = DeserializeFromJson<T>(json);

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
            catch (Exception exception)
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
                // create instance

                T instance = new();
                if (instance == null)
                {
                    result.ProcessSuccessful = false;
                    return result;
                }

                // get config as json array

                JsonNode? rootNode = JsonNode.Parse(json);
                if (rootNode == null)
                {
                    result.ProcessSuccessful = false;
                    return result;
                }

                JsonArray? configArray = rootNode["config"]?.AsArray();
                if (configArray == null || configArray.Count == 0)
                {
                    result.ProcessSuccessful = false;
                    return result;
                }

                // loop over settings

                foreach (var item in configArray)
                {
                    // get set object
                    if (item is JsonObject jsonObject 
                        && jsonObject.TryGetPropertyValue("set", out JsonNode? setNode) 
                        && setNode is JsonObject setObject)
                    {
                        KeyValuePair<string, JsonNode?>? kvp = setObject.FirstOrDefault();

                        if (kvp != null && kvp.Value.Value != null)
                        {
                            string propertyName = kvp.Value.Key;
                            JsonNode valueSettingsNode = kvp.Value.Value;
                            Dictionary<string, double> valuesWithProbabilities = new();

                            foreach (var valueSetting in valueSettingsNode.AsObject())
                            {
                                valuesWithProbabilities[valueSetting.Key] = valueSetting.Value.GetValue<double>(); 
                            }

                            string randomizedValue = GetRandomValueByProbability(valuesWithProbabilities);

                            // get property
                            PropertyInfo property = typeof(T).GetProperty(propertyName);
                            if (property == null)
                            {
                                throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(T)}'.");
                            }

                            // validate that property can be set
                            if (!property.CanWrite)
                            {
                                throw new InvalidOperationException($"Property '{propertyName}' is read-only and cannot be set.");
                            }

                            object convertedValue = Convert.ChangeType(randomizedValue, property.PropertyType);
                            property.SetValue(instance, convertedValue);

                        }

        
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


        public void SetUpInstance(T instance, string json)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            try
            {
                JsonNode? rootNode = JsonNode.Parse(json);
                if (rootNode == null) return;

                var configArray = rootNode["config"]?.AsArray();
                if (configArray == null || configArray.Count == 0) return;

                var setObject = configArray[0]?["set"];
                if (setObject == null) return;

                var nameObject = setObject["name"]?.AsObject();
                if (nameObject == null) return;

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

        private T? DeserializeFromJson<T>(string json)
        {
            bool isValidated = ValidateJson(json);
            
            if (isValidated)
            {
                T? generatedInstance = JsonSerializer.Deserialize<T>(json);

                if (generatedInstance != null)
                {
                    return generatedInstance;
                }
            }

            throw new Exception();
        }

        private bool ValidateJson(string json)
        {
            try
            {
                JsonDocument jsonDoc = JsonDocument.Parse(json);
                JsonElement rootElement = jsonDoc.RootElement;
                
                return true;
            }
            catch (Exception exception)
            {
                return false;                
            }
        }

        private string GetRandomValueByProbability(Dictionary<string, double> valuesWithProbabilities)
        {
            Random random = new();

            // validates correct probability config
            double total = valuesWithProbabilities.Values.Sum();
            if(total != 1)
            {
                throw new InvalidOperationException("The sum of the probabilities has to be equal 1.");
            }

            // TODO: Select after probability
            List<KeyValuePair<string, double>> values = valuesWithProbabilities.AsEnumerable<KeyValuePair<string, double>>().ToList();

            int elementIndex = random.Next(valuesWithProbabilities.Count() - 1);

            return values.ElementAt(elementIndex).Key;
        }
    }

}

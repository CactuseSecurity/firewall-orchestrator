
using NetTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace FWO.Data
{
    public class WrapperConverter<ValueType> : JsonConverter
    {
        private readonly string wrappedObjectName = "";

        public WrapperConverter(string wrappedObjectName)
        {
            this.wrappedObjectName = wrappedObjectName;
        }

        public override bool CanConvert(Type objectType) => typeof(ValueType).IsAssignableFrom(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            // Load the JSON as a JObject
            JObject jsonObject = JObject.Load(reader);

            // Check if the "wrappedObjectName" property exists
            if (jsonObject.TryGetValue(wrappedObjectName, out JToken? wrappedObjectToken))
            {
                // Deserialize the wrapped object
                return wrappedObjectToken.ToObject<ValueType>(serializer);
            }

            // Deserialize the wrapper object otherwise
            return jsonObject.ToObject<ValueType>(serializer);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            // Wrap the object with a property named "wrappedObjectName"
            JObject jsonObject = new JObject
            {
                { wrappedObjectName, value == null ? null : JToken.FromObject(value, serializer) }
            };

            // Write the JSON
            jsonObject.WriteTo(writer);
        }
    }

    public class IpAddressRangeJsonTypeConverter : JsonConverter<IPAddressRange>
    {
        public override IPAddressRange ReadJson(JsonReader reader, Type objectType, IPAddressRange? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Load the JSON as a JObject
            JObject jsonObject = JObject.Load(reader);
            // Deserialize the IP address range based on the properties ip_range_start and ip_range_end 
            IPAddress start = IPAddress.Parse((jsonObject.GetValue("ip_range_start")?.ToObject<string>() ?? throw new ArgumentNullException("ip_range_start")).Replace("/32", ""));
            IPAddress end = IPAddress.Parse((jsonObject.GetValue("ip_range_end")?.ToObject<string>() ?? throw new ArgumentNullException("ip_range_start")).Replace("/32", ""));
            return new IPAddressRange(start, end);
        }

        public override void WriteJson(JsonWriter writer, IPAddressRange? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                // Create a JSON JObject
                JObject result = new JObject
                {
                    { "ip_range_start", value.Begin.ToString() },
                    { "ip_range_end", value.Begin.ToString() }
                };

                result.WriteTo(writer);
            }
        }
    }

    public class ComplianceViolationConverter : JsonConverter<ComplianceViolation>
    {
        public override ComplianceViolation? ReadJson(JsonReader reader, Type objectType, ComplianceViolation? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            ComplianceViolation? violation = null;

            if (reader.TokenType != JsonToken.Null)
            {
                // Try deserialize base.

                JObject jsonObject = JObject.Load(reader);
                ComplianceViolationBase? violationBase = jsonObject.ToObject<ComplianceViolationBase>(serializer);

                if (violationBase != null)
                {
                    // Get id from json object.

                    int id = jsonObject.GetValue("id")?.ToObject<int>() ?? 0;

                    // Create instance from base and set id.

                    violation = new(id, violationBase);

                    // Parse Violation Type via criterion.

                    violation.Type = violation.ParseViolationType(violation.Criterion);
                }
            }

            return violation;
        }

        public override void WriteJson(JsonWriter writer, ComplianceViolation? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (value.Criterion != null)
            {
                value.Criterion.CriterionType = value.Type switch
                {
                    ComplianceViolationType.MatrixViolation => "Matrix",
                    ComplianceViolationType.NotAssessable => "Assessability",
                    ComplianceViolationType.ServiceViolation => "ForbiddenService",
                    _ => value.Criterion.CriterionType
                };
            }

            serializer.Serialize(writer, value);
        }
    }
}

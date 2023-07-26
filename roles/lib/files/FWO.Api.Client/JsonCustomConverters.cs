using NetTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FWO.Api.Client
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
}

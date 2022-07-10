using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum AccessField
    {
        source, 
        destination, 
        service
    }

    public class ElementBase
    {
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string? CidrString { get; set; }

        [JsonProperty("port"), JsonPropertyName("port")]
        public int Port { get; set; } = 1;

        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int? ProtoId { get; set; } = 6;

        [JsonProperty("network_object_id"), JsonPropertyName("network_object_id")]
        public long? NetworkId { get; set; }

        [JsonProperty("service_id"), JsonPropertyName("service_id")]
        public long? ServiceId { get; set; }

        [JsonProperty("field"), JsonPropertyName("field")]
        public string Field { get; set; } = "source";

        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public long? UserId { get; set; }

        [JsonProperty("original_nat_id"), JsonPropertyName("original_nat_id")]
        public long? OriginalNatId { get; set; }


        public ElementBase()
        { }

        public ElementBase(ElementBase element)
        {
            CidrString = element.CidrString;
            Port = element.Port;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
            UserId = element.UserId;
            OriginalNatId = element.OriginalNatId;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            CidrString = Sanitizer.SanitizeOpt(CidrString, ref shortened);
            Field = Sanitizer.SanitizeMand(Field, ref shortened);
            return shortened;
        }
    }
}

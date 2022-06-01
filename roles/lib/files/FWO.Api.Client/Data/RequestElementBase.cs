using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum RuleField
    {
        source, 
        destination, 
        service
    }

    public class RequestElementBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = "create";

        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        [JsonProperty("port"), JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonProperty("proto"), JsonPropertyName("proto")]
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


        public RequestElementBase()
        { }

        public RequestElementBase(RequestElementBase element)
        {
            RequestAction = element.RequestAction;
            Ip = element.Ip;
            Port = element.Port;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
            UserId = element.UserId;
            OriginalNatId = element.OriginalNatId;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            RequestAction = Sanitizer.SanitizeMand(RequestAction, ref shortened);
            Ip = Sanitizer.SanitizeMand(Ip, ref shortened);
            Field = Sanitizer.SanitizeMand(Field, ref shortened);
            return shortened;
        }
    }
}

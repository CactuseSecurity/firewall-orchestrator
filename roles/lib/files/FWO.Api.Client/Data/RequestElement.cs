using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestElement
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public int RequestAction { get; set; }

        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        [JsonProperty("port"), JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonProperty("proto"), JsonPropertyName("proto")]
        public int? ProtoId { get; set; }

        [JsonProperty("network_id"), JsonPropertyName("network_id")]
        public long NetworkId { get; set; }

        [JsonProperty("service_id"), JsonPropertyName("service_id")]
        public long ServiceId { get; set; }

        [JsonProperty("field"), JsonPropertyName("field")]
        public string Field { get; set; } = "";

        public RequestElement()
        { }

        public RequestElement(RequestElement element)
        {
            Id = element.Id;
            RequestAction = element.RequestAction;
            Ip = element.Ip;
            Port = element.Port;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Field = Sanitizer.SanitizeMand(Field, ref shortened);
            return shortened;
        }
    }
}

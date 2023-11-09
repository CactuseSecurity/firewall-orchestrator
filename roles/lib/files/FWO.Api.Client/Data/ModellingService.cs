using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingService : ModellingSvcElem
    {
        [JsonProperty("port"), JsonPropertyName("port")]
        public int? Port { get; set; }

        [JsonProperty("port_end"), JsonPropertyName("port_end")]
        public int? PortEnd { get; set; }

        [JsonProperty("proto_id"), JsonPropertyName("proto_id")]
        public int? ProtoId { get; set; }

        [JsonProperty("protocol"), JsonPropertyName("protocol")]
        public NetworkProtocol? Protocol { get; set; } = new();


        public bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            return shortened;
        }

        public static NetworkService ToNetworkService(ModellingService service)
        {
            return new NetworkService()
            {
                Id = service.Id,
                Name = service?.Name ?? "",
                DestinationPort = service?.Port,
                DestinationPortEnd = service?.PortEnd,
                ProtoId = service?.ProtoId,
                Protocol = service?.Protocol ?? new NetworkProtocol()
            };
        }
    }

    public class ModellingServiceWrapper
    {
        [JsonProperty("service"), JsonPropertyName("service")]
        public ModellingService Content { get; set; } = new();

        public static ModellingService[] Resolve(List<ModellingServiceWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}

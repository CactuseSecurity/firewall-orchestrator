using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Basics;

namespace FWO.Data.Modelling
{
    public class ModellingService : ModellingSvcObject
    {
        [JsonProperty("port"), JsonPropertyName("port")]
        public int? Port { get; set; }

        [JsonProperty("port_end"), JsonPropertyName("port_end")]
        public int? PortEnd { get; set; }

        [JsonProperty("proto_id"), JsonPropertyName("proto_id")]
        public int? ProtoId { get; set; }

        [JsonProperty("protocol"), JsonPropertyName("protocol")]
        public NetworkProtocol? Protocol { get; set; } = new();


        public ModellingService()
        {}

        public ModellingService(ModellingService service) : base(service)
        {
            Port = service.Port;
            PortEnd = service.PortEnd;
            ProtoId = service.ProtoId;
            Protocol = service.Protocol;
        }

        public override string Display()
        {
            return DisplayBase.DisplayService(ToNetworkService(this), false, Name).ToString();
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.Service}\"></span> " + DisplayHtml();
        }

        public static NetworkService ToNetworkService(ModellingService service)
        {
            return new NetworkService()
            {
                Id = service.Id,
                Number = service.Number,
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

        public static NetworkService[] ResolveAsNetworkServices(List<ModellingServiceWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => ModellingService.ToNetworkService(wrapper.Content));
        }
    }
}

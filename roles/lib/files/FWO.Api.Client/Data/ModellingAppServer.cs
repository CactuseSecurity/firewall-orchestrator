using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingAppServer : ModellingNwObject
    {
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        [JsonProperty("import_source"), JsonPropertyName("import_source")]
        public string ImportSource { get; set; } = "";

        public string ExtAppId { get; set; } = "";

        public override string Display()
        {
            return (IsDeleted ? "*" : "") + DisplayBase.DisplayIpWithName(ToNetworkObject());
        }

        public bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Ip = Sanitizer.SanitizeCidrMand(Ip, ref shortened);
            ImportSource = Sanitizer.SanitizeMand(ImportSource, ref shortened);
            return shortened;
        }

        public NetworkObject ToNetworkObject()
        {
            return new NetworkObject()
            {
                Id = Id,
                Name = Name,
                IP = Ip,
                IpEnd = Ip
            };
        }
    }

    public class ModellingAppServerWrapper
    {
        [JsonProperty("nwobject"), JsonPropertyName("nwobject")]
        public ModellingAppServer Content { get; set; } = new();

        public static ModellingAppServer[] Resolve(List<ModellingAppServerWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}

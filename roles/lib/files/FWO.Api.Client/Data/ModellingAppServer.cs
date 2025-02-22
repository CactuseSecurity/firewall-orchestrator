using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingAppServer : ModellingNwObject
    {
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string IpEnd { get; set; } = "";

        [JsonProperty("import_source"), JsonPropertyName("import_source")]
        public string ImportSource { get; set; } = "";

        [JsonProperty("custom_type"), JsonPropertyName("custom_type")]
        public int? CustomType { get; set; }

        public bool InUse { get; set; } = true;
        public bool HighestPrio { get; set; } = true;


        public override string Display()
        {
            return (IsDeleted ? "!" : "") + (InUse ? "" : "*") + DisplayBase.DisplayIpWithName(ToNetworkObject(this));
        }

        public override string DisplayHtml()
        {
            string tooltip = $"data-toggle=\"tooltip\" title=\"{TooltipText}\"";
            return $"<span class=\"{(InUse ? "" : "text-success")}\" {(!InUse && TooltipText != "" ? tooltip : "")}>{base.DisplayHtml()}</span>";
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.Host}\"></span> " + DisplayHtml();
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Ip = Sanitizer.SanitizeCidrMand(Ip, ref shortened);
            IpEnd = Sanitizer.SanitizeCidrMand(IpEnd, ref shortened);
            ImportSource = Sanitizer.SanitizeMand(ImportSource, ref shortened);
            return shortened;
        }

        public static NetworkObject ToNetworkObject(ModellingAppServer appServer)
        {
            return new NetworkObject()
            {
                Id = appServer.Id,
                Number = appServer.Number,
                Name = appServer.Name,
                IP = appServer.Ip,
                IpEnd = appServer.IpEnd
            };
        }

        public ModellingAppServer()
        {}

        public ModellingAppServer(ModellingAppServer appServer) : base(appServer)
        {
            Ip = appServer.Ip;
            IpEnd = appServer.IpEnd;
            ImportSource = appServer.ImportSource;
            InUse = appServer.InUse;
            CustomType = appServer.CustomType;
            HighestPrio = appServer.HighestPrio;
        }

        public ModellingAppServer(NetworkObject nwObj)  : base(nwObj)
        {
            Ip = nwObj.IP;
            IpEnd = nwObj.IpEnd;
            CustomType = 0;
        }

        public override bool Equals(object? obj)
        {
            return obj switch
            {
                ModellingAppServer apps => Id == apps.Id && AppId == apps.AppId && Name == apps.Name && IsDeleted == apps.IsDeleted
                    && Ip == apps.Ip && IpEnd == apps.IpEnd && ImportSource == apps.ImportSource && InUse == apps.InUse && CustomType == apps.CustomType,
                _ => base.Equals(obj),
            };
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }


    public class ModellingAppServerWrapper
    {
        [JsonProperty("owner_network"), JsonPropertyName("owner_network")]
        public ModellingAppServer Content { get; set; } = new();

        public static ModellingAppServer[] Resolve(List<ModellingAppServerWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}

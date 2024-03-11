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

        public bool InUse { get; set; } = true;


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
                IpEnd = appServer.Ip
            };
        }

        public ModellingAppServer()
        {}

        public ModellingAppServer(ModellingAppServer appServer)
        {
            Id = appServer.Id;
            Number = appServer.Number;
            AppId = appServer.AppId;
            Name = appServer.Name;
            IsDeleted = appServer.IsDeleted;
            Ip = appServer.Ip;
            ImportSource = appServer.ImportSource;
            InUse = appServer.InUse;
        }

        public override bool Equals(object? obj)
        {
            return obj switch
            {
                ModellingAppServer apps => Id == apps.Id && AppId == apps.AppId && Name == apps.Name && IsDeleted == apps.IsDeleted
                    && Ip == apps.Ip && ImportSource == apps.ImportSource && InUse == apps.InUse,
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

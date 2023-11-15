using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingConnection
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int AppId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; } = "";

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; } = "";

        [JsonProperty("is_interface"), JsonPropertyName("is_interface")]
        public bool IsInterface { get; set; } = false;

        [JsonProperty("used_interface_id"), JsonPropertyName("used_interface_id")]
        public long? UsedInterfaceId { get; set; }

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<ModellingServiceWrapper> Services { get; set; } = new();

        [JsonProperty("service_groups"), JsonPropertyName("service_groups")]
        public List<ModellingServiceGroupWrapper> ServiceGroups { get; set; } = new();

        [JsonProperty("source_nwobjects"), JsonPropertyName("source_nwobjects")]
        public List<ModellingAppServerWrapper> SourceAppServers { get; set; } = new();

        [JsonProperty("source_approles"), JsonPropertyName("source_approles")]
        public List<ModellingAppRoleWrapper> SourceAppRoles { get; set; } = new();

        [JsonProperty("source_nwgroups"), JsonPropertyName("source_nwgroups")]
        public List<ModellingNwGroupObjectWrapper> SourceNwGroups { get; set; } = new();

        [JsonProperty("destination_nwobjects"), JsonPropertyName("destination_nwobjects")]
        public List<ModellingAppServerWrapper> DestinationAppServers { get; set; } = new();

        [JsonProperty("destination_approles"), JsonPropertyName("destination_approles")]
        public List<ModellingAppRoleWrapper> DestinationAppRoles { get; set; } = new();

        [JsonProperty("destination_nwgroups"), JsonPropertyName("destination_nwgroups")]
        public List<ModellingNwGroupObjectWrapper> DestinationNwGroups { get; set; } = new();


        public bool SrcFromInterface { get; set; } = false;
        public bool DstFromInterface { get; set; } = false;

        public ModellingConnection()
        {}

        public ModellingConnection(ModellingConnection conn)
        {
           Id = conn.Id;
           AppId = conn.AppId;
           Name = conn.Name;
           Reason = conn.Reason;
           IsInterface = conn.IsInterface;
           UsedInterfaceId = conn.UsedInterfaceId;
           Creator = conn.Creator;
           CreationDate = conn.CreationDate;
           Services = new List<ModellingServiceWrapper>(conn.Services);
           ServiceGroups = new List<ModellingServiceGroupWrapper>(conn.ServiceGroups);
           SourceAppServers = new List<ModellingAppServerWrapper>(conn.SourceAppServers);
           SourceAppRoles = new List<ModellingAppRoleWrapper>(conn.SourceAppRoles);
           SourceNwGroups = new List<ModellingNwGroupObjectWrapper>(conn.SourceNwGroups);
           DestinationAppServers = new List<ModellingAppServerWrapper>(conn.DestinationAppServers);
           DestinationAppRoles = new List<ModellingAppRoleWrapper>(conn.DestinationAppRoles);
           DestinationNwGroups = new List<ModellingNwGroupObjectWrapper>(conn.DestinationNwGroups);
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            Reason = Sanitizer.SanitizeCommentOpt(Reason, ref shortened);
            return shortened;
        }
    }
}

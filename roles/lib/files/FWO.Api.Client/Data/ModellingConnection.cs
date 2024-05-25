using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ConState
    {
        // Connections:
        // Standard,
        InterfaceRequested,
        InterfaceRejected,
        // DivergencyVarianceAnalysis,

        // Interfaces:
        // Published,
        Requested,
        // Internal,
        Rejected
    }

    public class ModellingConnection
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int? AppId { get; set; }

        [JsonProperty("proposed_app_id"), JsonPropertyName("proposed_app_id")]
        public int? ProposedAppId { get; set; }

        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner App { get; set; } = new();

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; } = "";

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; } = "";

        [JsonProperty("is_interface"), JsonPropertyName("is_interface")]
        public bool IsInterface { get; set; } = false;

        [JsonProperty("used_interface_id"), JsonPropertyName("used_interface_id")]
        public long? UsedInterfaceId { get; set; }

        [JsonProperty("is_requested"), JsonPropertyName("is_requested")]
        public bool IsRequested { get; set; } = false;

        [JsonProperty("is_published"), JsonPropertyName("is_published")]
        public bool IsPublished { get; set; } = false;

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long? TicketId { get; set; }

        [JsonProperty("common_service"), JsonPropertyName("common_service")]
        public bool IsCommonService { get; set; } = false;

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("conn_prop"), JsonPropertyName("conn_prop")]
        public string? Properties { get; set; } = "";

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<ModellingServiceWrapper> Services { get; set; } = [];

        [JsonProperty("service_groups"), JsonPropertyName("service_groups")]
        public List<ModellingServiceGroupWrapper> ServiceGroups { get; set; } = [];

        [JsonProperty("source_nwobjects"), JsonPropertyName("source_nwobjects")]
        public List<ModellingAppServerWrapper> SourceAppServers { get; set; } = [];

        [JsonProperty("source_approles"), JsonPropertyName("source_approles")]
        public List<ModellingAppRoleWrapper> SourceAppRoles { get; set; } = [];

        [JsonProperty("destination_nwobjects"), JsonPropertyName("destination_nwobjects")]
        public List<ModellingAppServerWrapper> DestinationAppServers { get; set; } = [];

        [JsonProperty("destination_approles"), JsonPropertyName("destination_approles")]
        public List<ModellingAppRoleWrapper> DestinationAppRoles { get; set; } = [];

        public List<ModellingNwGroupWrapper> SourceNwGroups { get; set; } = [];
        public List<ModellingNwGroupWrapper> DestinationNwGroups { get; set; } = [];
        
        public bool SrcFromInterface { get; set; } = false;
        public bool DstFromInterface { get; set; } = false;
        public bool InterfaceIsRequested { get; set; } = false;
        public bool InterfaceIsRejected { get; set; } = false;

        public int OrderNumber { get; set; } = 0;
        public Dictionary<string, string>? Props;


        public ModellingConnection()
        {}

        public ModellingConnection(ModellingConnection conn)
        {
            OrderNumber = conn.OrderNumber;
            Id = conn.Id;
            AppId = conn.AppId;
            ProposedAppId = conn.ProposedAppId;
            Name = conn.Name;
            Reason = conn.Reason;
            IsInterface = conn.IsInterface;
            UsedInterfaceId = conn.UsedInterfaceId;
            IsRequested = conn.IsRequested;
            IsPublished = conn.IsPublished;
            TicketId = conn.TicketId;
            IsCommonService = conn.IsCommonService;
            Creator = conn.Creator;
            CreationDate = conn.CreationDate;
            Properties = conn.Properties;
            Services = new List<ModellingServiceWrapper>(conn.Services);
            ServiceGroups = new List<ModellingServiceGroupWrapper>(conn.ServiceGroups);
            SourceAppServers = new List<ModellingAppServerWrapper>(conn.SourceAppServers);
            SourceAppRoles = new List<ModellingAppRoleWrapper>(conn.SourceAppRoles);
            SourceNwGroups = new List<ModellingNwGroupWrapper>(conn.SourceNwGroups);
            DestinationAppServers = new List<ModellingAppServerWrapper>(conn.DestinationAppServers);
            DestinationAppRoles = new List<ModellingAppRoleWrapper>(conn.DestinationAppRoles);
            DestinationNwGroups = new List<ModellingNwGroupWrapper>(conn.DestinationNwGroups);
            SrcFromInterface = conn.SrcFromInterface;
            DstFromInterface = conn.DstFromInterface;
            InterfaceIsRequested = conn.InterfaceIsRequested;
            InterfaceIsRejected = conn.InterfaceIsRejected;
        }

        private void InitProps()
        {
            if(Properties != null && Properties != "")
            {
                Props = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Properties);
            }
        }

        public int CompareTo(ModellingConnection secondConnection)
        {
            int interfaceCompare = Compare(IsInterface, secondConnection.IsInterface);
            if (interfaceCompare != 0)
            {
                return interfaceCompare;
            }
            int comSvcCompare = Compare(IsCommonService, secondConnection.IsCommonService);
            if (comSvcCompare != 0)
            {
                return comSvcCompare;
            }
            return Name?.CompareTo(secondConnection.Name) ?? -1;
        }

        private static int Compare(bool first, bool second)
        {
            if(first && !second)
            {
                return -1;
            }
            if(!first && second)
            {
                return 1;
            }
            return 0;
        }

        public string DisplayNameWithOwner(FwoOwner owner)
        {
            return Name + " (" + owner.ExtAppId + ":" + owner.Name + ")";
        }
        
        public bool SourceFilled()
        {
            return SourceAppServers.Count > 0 || SourceAppRoles.Count > 0 || SourceNwGroups.Count > 0;
        }

        public bool DestinationFilled()
        {
            return DestinationAppServers.Count > 0 || DestinationAppRoles.Count > 0 || DestinationNwGroups.Count > 0;
        }

        public void ExtractNwGroups()
        {
            SourceNwGroups = [];
            foreach(var nwGroup in SourceAppRoles)
            {
                if(nwGroup.Content.GroupType != (int)ModellingTypes.ModObjectType.AppRole)
                {
                    SourceNwGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup.Content.ToBase() });
                }
            }
            SourceAppRoles = SourceAppRoles.Where(nwGroup => nwGroup.Content.GroupType == (int)ModellingTypes.ModObjectType.AppRole).ToList();
            DestinationNwGroups = [];
            foreach(var nwGroup in DestinationAppRoles)
            {
                if(nwGroup.Content.GroupType != (int)ModellingTypes.ModObjectType.AppRole)
                {
                    DestinationNwGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup.Content.ToBase() });
                }
            }
            DestinationAppRoles = DestinationAppRoles.Where(nwGroup => nwGroup.Content.GroupType == (int)ModellingTypes.ModObjectType.AppRole).ToList();
        }

        public void AddProperty(string key, string value = "")
        {
            Props ??= [];
            InitProps();
            Props.TryAdd(key, value);
            Properties = System.Text.Json.JsonSerializer.Serialize(Props);
        }

        public void RemoveProperty(string key)
        {
            InitProps();
            if(Props != null && Props.ContainsKey(key))
            {
                Props.Remove(key);
            }
        }

        public string GetStringProperty(string prop)
        {
            InitProps();
            if(Props != null && Props.TryGetValue(prop, out string? value))
            {
                return value;
            }
            return "";
        }

        public bool GetBoolProperty(string prop)
        {
            InitProps();
            return Props?.ContainsKey(prop) ?? false;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            Reason = Sanitizer.SanitizeCommentOpt(Reason, ref shortened);
            Creator = Sanitizer.SanitizeOpt(Creator, ref shortened);
            Properties = Sanitizer.SanitizeOpt(Properties, ref shortened);
            return shortened;
        }
    }

    public class ModellingConnectionWrapper
    {
        [JsonProperty("connection"), JsonPropertyName("connection")]
        public ModellingConnection Content { get; set; } = new();

        public static ModellingConnection[] Resolve(List<ModellingConnectionWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}

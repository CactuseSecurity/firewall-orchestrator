using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ConState
    {
        // Connections:
        InterfaceRequested,
        InterfaceRejected,

        // Interfaces:
        Requested,
        Rejected,

        EmptyAppRoles,
        DeletedObjects
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

        [JsonProperty("extra_params"), JsonPropertyName("extra_params")]
        public string? ExtraParams { get; set; } = "";

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

        [JsonProperty("source_areas"), JsonPropertyName("source_areas")]
        public List<ModellingNetworkAreaWrapper> SourceAreas { get; set; } = [];

        [JsonProperty("destination_areas"), JsonPropertyName("destination_areas")]
        public List<ModellingNetworkAreaWrapper> DestinationAreas { get; set; } = [];

        [JsonProperty("source_other_groups"), JsonPropertyName("source_other_groups")]
        public List<ModellingNwGroupWrapper> SourceOtherGroups { get; set; } = [];

        [JsonProperty("destination_other_groups"), JsonPropertyName("destination_other_groups")]
        public List<ModellingNwGroupWrapper> DestinationOtherGroups { get; set; } = [];

        
        public bool SrcFromInterface { get; set; } = false;
        public bool DstFromInterface { get; set; } = false;
        public bool InterfaceIsRequested { get; set; } = false;
        public bool InterfaceIsRejected { get; set; } = false;

        public int OrderNumber { get; set; } = 0;
        public Dictionary<string, string>? Props;
        public List<ModellingExtraConfig> ExtraConfigs
        {  
            get => ExtraParams != null && ExtraParams != "" ? System.Text.Json.JsonSerializer.Deserialize<List<ModellingExtraConfig>>(ExtraParams) ?? throw new Exception("ExtraParams could not be parsed.") : [];
            set
            {
                if(value != null)
                {
                    ExtraParams = System.Text.Json.JsonSerializer.Serialize(value) ?? throw new Exception("value could not be parsed.");
                }
            }
        }


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
            ExtraParams = conn.ExtraParams;
            Services = [.. conn.Services];
            ServiceGroups = [.. conn.ServiceGroups];
            SourceAppServers = [.. conn.SourceAppServers];
            SourceAppRoles = [.. conn.SourceAppRoles];
            SourceAreas = [.. conn.SourceAreas];
            SourceOtherGroups = [.. conn.SourceOtherGroups];
            DestinationAppServers = [.. conn.DestinationAppServers];
            DestinationAppRoles = [.. conn.DestinationAppRoles];
            DestinationAreas = [.. conn.DestinationAreas];
            DestinationOtherGroups = [.. conn.DestinationOtherGroups];
            SrcFromInterface = conn.SrcFromInterface;
            DstFromInterface = conn.DstFromInterface;
            InterfaceIsRequested = conn.InterfaceIsRequested;
            InterfaceIsRejected = conn.InterfaceIsRejected;
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
            int publishedCompare = Compare(IsPublished, secondConnection.IsPublished);
            if (publishedCompare != 0)
            {
                return publishedCompare;
            }
            int rejectedCompare = -Compare(GetBoolProperty(ConState.Rejected.ToString()), secondConnection.GetBoolProperty(ConState.Rejected.ToString()));
            if (rejectedCompare != 0)
            {
                return rejectedCompare;
            }
            return Name?.CompareTo(secondConnection.Name) ?? -1;
        }

        public string DisplayNameWithOwner(FwoOwner owner)
        {
            return Name + " (" + owner.ExtAppId + ":" + owner.Name + ")";
        }
        
        public bool SourceFilled()
        {
            return SourceAppServers.Count > 0 || SourceAppRoles.Count > 0  || SourceAreas.Count > 0 || SourceOtherGroups.Count > 0;
        }

        public bool DestinationFilled()
        {
            return DestinationAppServers.Count > 0 || DestinationAppRoles.Count > 0 || DestinationAreas.Count > 0 || DestinationOtherGroups.Count > 0;
        }

        public void AddProperty(string key, string value = "")
        {
            InitProps();
            Props?.TryAdd(key, value);
            Properties = System.Text.Json.JsonSerializer.Serialize(Props);
        }

        public void RemoveProperty(string key)
        {
            InitProps();
            if(Props != null && Props.Count > 0 && Props.ContainsKey(key))
            {
                Props.Remove(key);
            }
            Properties = System.Text.Json.JsonSerializer.Serialize(Props);
        }

        public string GetStringProperty(string prop)
        {
            InitProps();
            if(Props != null && Props.Count > 0 && Props.TryGetValue(prop, out string? value))
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


        public void SyncState()
        {
            if(IsInterface)
            {
                if(!GetBoolProperty(ConState.Rejected.ToString()))
                {
                    if(!IsPublished && IsRequested)
                    {
                        AddProperty(ConState.Requested.ToString());
                    }
                    else
                    {
                        RemoveProperty(ConState.Requested.ToString());
                    }
                }
            }
            else if(UsedInterfaceId != null)
            {
                if(InterfaceIsRejected)
                {
                    RemoveProperty(ConState.InterfaceRequested.ToString());
                    AddProperty(ConState.InterfaceRejected.ToString());
                }
                else if(InterfaceIsRequested)
                {
                    AddProperty(ConState.InterfaceRequested.ToString());
                }
                else
                {
                    RemoveProperty(ConState.InterfaceRequested.ToString());
                }
            }
            if(EmptyAppRolesFound())
            {
                AddProperty(ConState.EmptyAppRoles.ToString());
            }
            else
            {
                RemoveProperty(ConState.EmptyAppRoles.ToString());
            }
            if(DeletedObjectsFound())
            {
                AddProperty(ConState.DeletedObjects.ToString());
            }
            else
            {
                RemoveProperty(ConState.DeletedObjects.ToString());
            }
        }

        public bool EmptyAppRolesFound()
        {
            foreach(var appRole in SourceAppRoles)
            {
                if(appRole.Content.AppServers.Count == 0)
                {
                    return true;
                }
            }
            foreach(var appRole in DestinationAppRoles)
            {
                if(appRole.Content.AppServers.Count == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public bool DeletedObjectsFound()
        {
            foreach(var area in SourceAreas)
            {
                if(area.Content.IsDeleted)
                {
                    return true;
                }
            }
            foreach(var area in DestinationAreas)
            {
                if(area.Content.IsDeleted)
                {
                    return true;
                }
            }
            foreach(var appServer in SourceAppServers)
            {
                if(appServer.Content.IsDeleted)
                {
                    return true;
                }
            }
            foreach(var appServer in DestinationAppServers)
            {
                if(appServer.Content.IsDeleted)
                {
                    return true;
                }
            }
            return false;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            Reason = Sanitizer.SanitizeCommentOpt(Reason, ref shortened);
            Creator = Sanitizer.SanitizeOpt(Creator, ref shortened);
            Properties = Sanitizer.SanitizeKeyOpt(Properties, ref shortened);
            ExtraParams = Sanitizer.SanitizeKeyOpt(ExtraParams, ref shortened);
            return shortened;
        }

        private void InitProps()
        {
            Props ??= [];
            if(Properties != null && Properties != "")
            {
                Props = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Properties) ?? [];
            }
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

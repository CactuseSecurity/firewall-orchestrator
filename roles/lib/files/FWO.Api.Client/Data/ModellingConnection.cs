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

        [JsonProperty("common_service"), JsonPropertyName("common_service")]
        public bool IsCommonService { get; set; } = false;

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

        [JsonProperty("destination_nwobjects"), JsonPropertyName("destination_nwobjects")]
        public List<ModellingAppServerWrapper> DestinationAppServers { get; set; } = new();

        [JsonProperty("destination_approles"), JsonPropertyName("destination_approles")]
        public List<ModellingAppRoleWrapper> DestinationAppRoles { get; set; } = new();

        
        public List<ModellingNwGroupWrapper> SourceNwGroups { get; set; } = new();
        public List<ModellingNwGroupWrapper> DestinationNwGroups { get; set; } = new();
        

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
           SourceNwGroups = new List<ModellingNwGroupWrapper>(conn.SourceNwGroups);
           DestinationAppServers = new List<ModellingAppServerWrapper>(conn.DestinationAppServers);
           DestinationAppRoles = new List<ModellingAppRoleWrapper>(conn.DestinationAppRoles);
           DestinationNwGroups = new List<ModellingNwGroupWrapper>(conn.DestinationNwGroups);
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

        public string DisplayWithOwner(FwoOwner owner)
        {
            return Name + " (" + owner.ExtAppId + ":" + owner.Name + ")";
        }
        
        public string GetConnType()
        {
            if(IsInterface)
            {
                return "interface";
            }
            if(IsCommonService)
            {
                return "common_service";
            }
            return "regular_connection";
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
            SourceNwGroups = new();
            foreach(var nwGroup in SourceAppRoles)
            {
                if(nwGroup.Content.GroupType != (int)ModellingTypes.ObjectType.AppRole)
                {
                    SourceNwGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup.Content.ToBase() });
                }
            }
            SourceAppRoles = SourceAppRoles.Where(nwGroup => nwGroup.Content.GroupType == (int)ModellingTypes.ObjectType.AppRole).ToList();
            DestinationNwGroups = new();
            foreach(var nwGroup in DestinationAppRoles)
            {
                if(nwGroup.Content.GroupType != (int)ModellingTypes.ObjectType.AppRole)
                {
                    DestinationNwGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup.Content.ToBase() });
                }
            }
            DestinationAppRoles = DestinationAppRoles.Where(nwGroup => nwGroup.Content.GroupType == (int)ModellingTypes.ObjectType.AppRole).ToList();
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            Reason = Sanitizer.SanitizeCommentOpt(Reason, ref shortened);
            Creator = Sanitizer.SanitizeOpt(Creator, ref shortened);
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

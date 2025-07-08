using FWO.Basics;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Modelling
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
        DeletedObjects,
        EmptySvcGrps,
        DocumentationOnly,
        VarianceChecked,
        NotImplemented,
        VarianceFound
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

        [JsonProperty("requested_on_fw"), JsonPropertyName("requested_on_fw")]
        public bool RequestedOnFw { get; set; } = false;

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public bool Removed { get; set; } = false;

        [JsonProperty("removal_date"), JsonPropertyName("removal_date")]
        public DateTime? RemovalDate { get; set; }

        
        public bool SrcFromInterface { get; set; } = false;
        public bool DstFromInterface { get; set; } = false;
        public bool InterfaceIsRequested { get; set; } = false;
        public bool InterfaceIsRejected { get; set; } = false;

        public int OrderNumber { get; set; } = 0;
        public Dictionary<string, string>? Props { get; set; }
        public List<ModellingExtraConfig> ExtraConfigs
        {  
            get => ExtraParams != null && ExtraParams != "" ? System.Text.Json.JsonSerializer.Deserialize<List<ModellingExtraConfig>>(ExtraParams) ?? throw new JsonException("ExtraParams could not be parsed.") : [];
            set
            {
                if(value != null)
                {
                    ExtraParams = System.Text.Json.JsonSerializer.Serialize(value) ?? throw new JsonException("value could not be parsed.");
                }
            }
        }
        public List<ModellingExtraConfig> ExtraConfigsFromInterface { get; set; } = [];
        public bool ProdRuleFound { get; set; } = false;


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
            RequestedOnFw = conn.RequestedOnFw;
            Removed = conn.Removed;
            RemovalDate = conn.RemovalDate;
            SrcFromInterface = conn.SrcFromInterface;
            DstFromInterface = conn.DstFromInterface;
            InterfaceIsRequested = conn.InterfaceIsRequested;
            InterfaceIsRejected = conn.InterfaceIsRejected;
            ExtraConfigsFromInterface = conn.ExtraConfigsFromInterface;
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

        public bool IsRelevantForVarianceAnalysis(long dummyAppRoleId)
        {
            return !(IsInterface ||
                IsDocumentationOnly() ||
                GetBoolProperty(ConState.InterfaceRequested.ToString()) ||
                GetBoolProperty(ConState.InterfaceRejected.ToString()) || 
                EmptyAppRolesFound(dummyAppRoleId) ||
                DeletedObjectsFound() ||
                EmptyServiceGroupsFound());
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

        public void UpdateProperty(string key, bool condition)
        {
            if (condition)
            {
                AddProperty(key);
            }
            else
            {
                RemoveProperty(key);
            }
        }

        public string GetStringProperty(string prop)
        {
            InitProps();
            if (Props != null && Props.Count > 0 && Props.TryGetValue(prop, out string? value))
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


        public void SyncState(long dummyAppRoleId)
        {
            if(IsInterface)
            {
                SyncInterface();
            }
            else if(UsedInterfaceId != null)
            {
                SyncInterfaceUser();
            }
            SyncMemberIssues(dummyAppRoleId);
            UpdateProperty(ConState.DocumentationOnly.ToString(), IsDocumentationOnly());
        }

        private void SyncInterface()
        {
            if (!GetBoolProperty(ConState.Rejected.ToString()))
            {
                UpdateProperty(ConState.Requested.ToString(), !IsPublished && IsRequested);
            }
        }

        private void SyncInterfaceUser()
        {
            if (InterfaceIsRejected)
            {
                RemoveProperty(ConState.InterfaceRequested.ToString());
                AddProperty(ConState.InterfaceRejected.ToString());
            }
            else
            {
                UpdateProperty(ConState.InterfaceRequested.ToString(), InterfaceIsRequested);
            }
        }

        private void SyncMemberIssues(long dummyAppRoleId)
        {
            UpdateProperty(ConState.EmptyAppRoles.ToString(), EmptyAppRolesFound(dummyAppRoleId));
            UpdateProperty(ConState.DeletedObjects.ToString(), DeletedObjectsFound());
            UpdateProperty(ConState.EmptySvcGrps.ToString(), EmptyServiceGroupsFound());
        }

        public void CleanUpVarianceResults()
        {
            RemoveProperty(ConState.VarianceChecked.ToString());
            RemoveProperty(ConState.VarianceFound.ToString());
            RemoveProperty(ConState.NotImplemented.ToString());
        }

        public bool EmptyAppRolesFound(long dummyAppRoleId)
        {
            return SourceAppRoles.Any(a => a.Content.Id != dummyAppRoleId && a.Content.AppServers.Count == 0) ||
                DestinationAppRoles.Any(a => a.Content.Id != dummyAppRoleId && a.Content.AppServers.Count == 0);
        }

        public bool EmptyServiceGroupsFound() 
            => ServiceGroups.Any(_ => _.Content.Services.Count == 0);

        public bool IsDocumentationOnly()
            => ExtraConfigs.Any(_ => _.ExtraConfigType.StartsWith(GlobalConst.kDoku_));

        public bool IsNat()
            => ExtraConfigs.Any(_ => _.ExtraConfigType.ToUpper() == GlobalConst.kNAT);

        public Dictionary<string, bool> GetSpecialUserObjectNames()
        {
            Dictionary<string, bool> userObjectNames = [];
            foreach (var extraConfig in ExtraConfigs.Where(e => e.ExtraConfigType.ToLower().EndsWith(GlobalConst.k_user)
                                                            || e.ExtraConfigType.ToLower().EndsWith(GlobalConst.k_user2)))
            {
                userObjectNames.Add(extraConfig.ExtraConfigText.ToLower(), false);
            }
            return userObjectNames;
        }

        public Dictionary<string, bool> GetUpdateableObjectNames()
        {
            Dictionary<string, bool> updateableObjectNames = [];
            foreach (var extraConfig in ExtraConfigs.Where(e => e.ExtraConfigType.ToLower().StartsWith(GlobalConst.kUpdateable)))
            {
                updateableObjectNames.Add(extraConfig.ExtraConfigText.ToLower(), false);
            }
            return updateableObjectNames;
        }

        public bool DeletedObjectsFound()
        {
            return SourceAreas.Any(a => a.Content.IsDeleted) ||
                DestinationAreas.Any(a => a.Content.IsDeleted) ||
                SourceAppRoles.Any(aR => aR.Content.AppServers.Any(a => a.Content.IsDeleted)) ||
                DestinationAppRoles.Any(aR => aR.Content.AppServers.Any(a => a.Content.IsDeleted)) ||
                SourceAppServers.Any(a => a.Content.IsDeleted) ||
                DestinationAppServers.Any(a => a.Content.IsDeleted);
        }

        public Rule ToRule()
        {
            List<NetworkLocation> froms = [];
            foreach (var areaWrapper in SourceAreas)
            {
                froms.Add(new(new(), areaWrapper.Content.ToNetworkObjectGroup()));
            }
            foreach (var groupWrapper in SourceOtherGroups)
            {
                froms.Add(new(new(), groupWrapper.Content.ToNetworkObjectGroup()));
            }
            foreach (var appRoleWrapper in SourceAppRoles)
            {
                froms.Add(new(new(), appRoleWrapper.Content.ToNetworkObjectGroup()));
            }
            foreach (var appServerWrapper in SourceAppServers)
            {
                froms.Add(new(new(), ModellingAppServer.ToNetworkObject(appServerWrapper.Content)));
            }

            List<NetworkLocation> tos = [];
            foreach (var areaWrapper in DestinationAreas)
            {
                tos.Add(new(new(), areaWrapper.Content.ToNetworkObjectGroup()));
            }
            foreach (var groupWrapper in DestinationOtherGroups)
            {
                tos.Add(new(new(), groupWrapper.Content.ToNetworkObjectGroup()));
            }
            foreach (var appRoleWrapper in DestinationAppRoles)
            {
                tos.Add(new(new(), appRoleWrapper.Content.ToNetworkObjectGroup()));
            }
            foreach (var appServerWrapper in DestinationAppServers)
            {
                tos.Add(new(new(), ModellingAppServer.ToNetworkObject(appServerWrapper.Content)));
            }

            List<ServiceWrapper> services = [];
            foreach (var svcGrp in ServiceGroups)
            {
                services.Add(new(){ Content = svcGrp.Content.ToNetworkServiceGroup() });
            }
            foreach (var svc in Services)
            {
                services.Add(new(){ Content = ModellingService.ToNetworkService(svc.Content) });
            }

            return new Rule()
            {
                Name = Name,
                Froms = [.. froms],
                Tos = [.. tos],
                Services = [.. services]
            };
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

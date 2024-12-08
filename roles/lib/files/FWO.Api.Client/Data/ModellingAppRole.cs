using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Basics;

namespace FWO.Api.Data
{
    public class ModellingAppRole : ModellingNwGroup
    {
        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("nwobjects"), JsonPropertyName("nwobjects")]
        public List<ModellingAppServerWrapper> AppServers { get; set; } = [];

        public ModellingNetworkArea? Area { get; set; } = new();


        public ModellingAppRole()
        {}

        public ModellingAppRole(ModellingAppRole appRole) : base(appRole)
        {
            Comment = appRole.Comment;
            Creator = appRole.Creator;
            CreationDate = appRole.CreationDate;
            AppServers = appRole.AppServers;
            Area = appRole.Area;
        }

        public ModellingAppRole(NetworkObject nwObj, ModellingNamingConvention? namCon = null) : base(nwObj, namCon)
        {
            Comment = nwObj.Comment;
            CreationDate = nwObj.CreateTime.Time;
            AppServers = ConvertNwObjectsToAppServers(nwObj.ObjectGroupFlats);
            // Todo: Fill Area + AppId from IdString (-> Naming Convention)?
        }

        protected static List<ModellingAppServerWrapper> ConvertNwObjectsToAppServers(GroupFlat<NetworkObject>[] groupFlats)
        {
            List<ModellingAppServerWrapper> appServers = [];
            foreach(var obj in groupFlats.Where(x => x.Object?.IP != null && x.Object?.IP != "").ToList())
            {
                appServers.Add(new ModellingAppServerWrapper(){ Content = obj.Object != null ? new(obj.Object) : new() });
            }
            return appServers;
        }

        public ModellingNwGroup ToBase()
        {
            return new ModellingNwGroup()
            {
                Id = Id,
                Number = Number,
                GroupType = GroupType,
                IdString = IdString,
                Name = Name,
                AppId = AppId,
                IsDeleted = IsDeleted
            };
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.AppRole}\"></span> " + DisplayHtml();
        }

        public override NetworkObject ToNetworkObjectGroup()
        {
            Group<NetworkObject>[] objectGroups = ModellingAppRoleWrapper.ResolveAppServersAsNetworkObjectGroup(AppServers ?? []);
            return new()
            {
                Id = Id,
                Number = Number,
                Name = Name ?? "",
                Comment = Comment ?? "",
                Type = new NetworkObjectType(){ Name = ObjectType.Group },
                ObjectGroups = objectGroups,
                MemberNames = string.Join("|", Array.ConvertAll(objectGroups, o => o.Object?.Name))
            };
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Comment = Sanitizer.SanitizeCommentOpt(Comment, ref shortened);
            Creator = Sanitizer.SanitizeOpt(Creator, ref shortened);
            return shortened;
        }
    }
    
    public class ModellingAppRoleWrapper : ModellingNwGroupWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public new ModellingAppRole Content { get; set; } = new();

        public static ModellingAppRole[] Resolve(List<ModellingAppRoleWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }

        public static Group<NetworkObject>[] ResolveAppServersAsNetworkObjectGroup(List<ModellingAppServerWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => new Group<NetworkObject> {Id = wrapper.Content.Id, Object = ModellingAppServer.ToNetworkObject(wrapper.Content)});
        }
    }
}

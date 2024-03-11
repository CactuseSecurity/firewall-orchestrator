using System.Text.Json.Serialization; 
using Newtonsoft.Json;

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
        public List<ModellingAppServerWrapper> AppServers { get; set; } = new();

        public ModellingNetworkArea? Area { get; set; } = new();


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

        public NetworkObject ToNetworkObjectGroup()
        {
            return new()
            {
                Id = Id,
                Number = Number,
                Name = Name ?? "",
                Comment = Comment ?? "",
                Type = new NetworkObjectType(){ Name = ObjectType.Group },
                ObjectGroups = ModellingAppRoleWrapper.ResolveAsNetworkObjectGroup(AppServers ?? new List<ModellingAppServerWrapper>())
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
    
    public class ModellingAppRoleWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public ModellingAppRole Content { get; set; } = new();

        public static ModellingAppRole[] Resolve(List<ModellingAppRoleWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }

        public static Group<NetworkObject>[] ResolveAsNetworkObjectGroup(List<ModellingAppServerWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => new Group<NetworkObject> {Id = wrapper.Content.Id, Object = ModellingAppServer.ToNetworkObject(wrapper.Content)});
        }
    }
}

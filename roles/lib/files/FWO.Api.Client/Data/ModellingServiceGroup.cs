using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.GlobalConstants;

namespace FWO.Api.Data
{
    public class ModellingServiceGroup : ModellingSvcObject
    {
        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<ModellingServiceWrapper> Services { get; set; } = new();


        public ModellingServiceGroup()
        {}

        public ModellingServiceGroup(ModellingServiceGroup svcGroup) : base(svcGroup)
        {
            Comment = svcGroup.Comment;
            Creator = svcGroup.Creator;
            CreationDate = svcGroup.CreationDate;
            Services = svcGroup.Services;
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.ServiceGroup}\"></span> " + DisplayHtml();
        }

        public NetworkService ToNetworkServiceGroup()
        {
            Group<NetworkService>[] serviceGroups = ModellingServiceGroupWrapper.ResolveAsNetworkServiceGroup(Services ?? new List<ModellingServiceWrapper>());
            return new()
            {
                Id = Id,
                Name = Name ?? "",
                Comment = Comment ?? "",
                Type = new NetworkServiceType(){ Name = ObjectType.Group },
                ServiceGroups = serviceGroups,
                MemberNames = string.Join("|", Array.ConvertAll(serviceGroups, o => o.Object?.Name))
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

    public class ModellingServiceGroupWrapper
    {
        [JsonProperty("service_group"), JsonPropertyName("service_group")]
        public ModellingServiceGroup Content { get; set; } = new();

        public static ModellingServiceGroup[] Resolve(List<ModellingServiceGroupWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }

        public static Group<NetworkService>[] ResolveAsNetworkServiceGroup(List<ModellingServiceWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => new Group<NetworkService> {Id = wrapper.Content.Id, Object = ModellingService.ToNetworkService(wrapper.Content)});
        }
    }
}

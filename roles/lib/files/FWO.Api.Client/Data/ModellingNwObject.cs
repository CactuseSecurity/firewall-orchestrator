using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNwObject: ModellingObject
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("is_deleted"), JsonPropertyName("is_deleted")]
        public bool IsDeleted { get; set; }


        public ModellingNwObject()
        {}

        public ModellingNwObject(ModellingNwObject nwObject) : base(nwObject)
        {
            Id = nwObject.Id;
            IsDeleted = nwObject.IsDeleted;
        }

        public ModellingNwObject(NetworkObject nwObj) : base(nwObj)
        {
            Id = nwObj.Id;
            IsDeleted = false; // Todo: !nwObj.Active ?
        }

        public override string Display()
        {
            return (IsDeleted ? "!" : "") + Name;
        }

        public override string DisplayHtml()
        {
            string tooltip = $"data-toggle=\"tooltip\" title=\"{TooltipText}\"";
            return $"<span class=\"{(IsDeleted ? "text-danger" : "")}\" {(IsDeleted && TooltipText != "" ? tooltip : "")}>{(IsDeleted ? "<i>" : "")}{base.DisplayHtml()}{(IsDeleted ? "</i>" : "")}</span>";
        }
    }
}

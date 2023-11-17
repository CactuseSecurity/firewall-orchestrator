using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNwGroup : ModellingNwObject
    {
        [JsonProperty("group_type"), JsonPropertyName("group_type")]
        public int GroupType { get; set; }

       
        public override string DisplayHtml()
        {
            return $"<span><b>{Display()}</b></span>";
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"oi oi-folder\"></span> " + DisplayHtml();
        }
    }
    
    public class ModellingNwGroupWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public ModellingNwGroup Content { get; set; } = new();

        public static ModellingNwGroup[] Resolve(List<ModellingNwGroupWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}

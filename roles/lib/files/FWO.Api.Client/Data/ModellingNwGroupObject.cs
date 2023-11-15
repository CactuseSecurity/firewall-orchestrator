using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNwGroupObject : ModellingNwObject
    {
        [JsonProperty("group_type"), JsonPropertyName("group_type")]
        public int GroupType { get; set; }
    }
    
    public class ModellingNwGroupObjectWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public ModellingNwGroupObject Content { get; set; } = new();

        public static ModellingNwGroupObject[] Resolve(List<ModellingNwGroupObjectWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}

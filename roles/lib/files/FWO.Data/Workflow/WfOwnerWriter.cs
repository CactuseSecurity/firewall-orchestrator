using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfOwnerWriter
    {
        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int? OwnerId { get; set; }

        public WfOwnerWriter()
        {}

        public WfOwnerWriter(FwoOwner owner)
        { 
            OwnerId = owner.Id;
        }
    }
}

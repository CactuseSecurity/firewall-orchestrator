using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestOwnerWriter
    {
        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int? OwnerId { get; set; }

        public RequestOwnerWriter()
        {}

        public RequestOwnerWriter(FwoOwner owner)
        { 
            OwnerId = owner.Id;
        }
    }
}

using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestOwner : RequestOwnerBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }


        public RequestOwner()
        { }

        public RequestOwner(RequestOwner owner) : base(owner)
        {
            Id = owner.Id;
        }
    }

    public class RequestOwnerDataHelper
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public RequestOwner Owner { get; set; } = new RequestOwner();
    }

}

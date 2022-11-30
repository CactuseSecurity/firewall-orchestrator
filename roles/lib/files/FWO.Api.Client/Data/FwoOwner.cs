using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class FwoOwner : FwoOwnerBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }


        public FwoOwner()
        { }

        public FwoOwner(FwoOwner owner) : base(owner)
        {
            Id = owner.Id;
        }
    }

    public class FwoOwnerDataHelper
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner { get; set; } = new FwoOwner();
    }

}

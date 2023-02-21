using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class FwoOwner : FwoOwnerBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("last_recert_check"), JsonPropertyName("last_recert_check")]
        public DateTime? LastRecertCheck { get; set; }

        [JsonProperty("recert_check_params"), JsonPropertyName("recert_check_params")]
        public string? RecertCheckParams { get; set; }

        public List<NwObjectElement> NwObjElements { get; set; } = new List<NwObjectElement>();


        public FwoOwner()
        { }

        public FwoOwner(FwoOwner owner) : base(owner)
        {
            Id = owner.Id;
            NwObjElements = owner.NwObjElements;
            LastRecertCheck = owner.LastRecertCheck;
            RecertCheckParams = owner.RecertCheckParams;
        }
    }

    public class FwoOwnerDataHelper
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner { get; set; } = new FwoOwner();
    }

}

using System.Text.Json.Serialization; 
using Newtonsoft.Json; 

namespace FWO.Api.Client
{
    public class ReturnId
    {
        [JsonProperty("newId"), JsonPropertyName("newId")]
        public int NewId { get; set; }

        [JsonProperty("UpdatedId"), JsonPropertyName("UpdatedId")]
        public int UpdatedId { get; set; }

        [JsonProperty("DeletedId"), JsonPropertyName("DeletedId")]
        public int DeletedId { get; set; }

        [JsonProperty("affected_rows"), JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }

        [JsonProperty("uiuser_password_must_be_changed"), JsonPropertyName("uiuser_password_must_be_changed")]
        public bool PasswordMustBeChanged { get; set; }
    }
    
    public class NewReturning
    {
        [JsonProperty("returning"), JsonPropertyName("returning")]
        public ReturnId[]? ReturnIds { get; set; }
    }

    public class AggregateCount
    {
        [JsonProperty("aggregate"), JsonPropertyName("aggregate")]
        public Aggregate Aggregate {get; set;}
    }

    public class Aggregate
    {
        [JsonProperty("count"), JsonPropertyName("count")]     
        public int Count { get; set; }
    }
}

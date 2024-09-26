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

    public class AggregateCountLastHit
    // {
    //     [JsonProperty("device"), JsonPropertyName("device")]
    //     public List<DeviceLastHit> Devices {get; set;} = [];
    // }
    // public class DeviceLastHit
    {
        [JsonProperty("rulebase_on_gateways"), JsonPropertyName("rulebase_on_gateways")]
        public List<RulebaseOnGatewaysLastHit> RulebasesOnGateway {get; set;} = [];
    }
    public class RulebaseOnGatewaysLastHit
    {
        [JsonProperty("rulebase"), JsonPropertyName("rulebase")]
        public RulebaseLastHit Rulebase {get; set;} = new RulebaseLastHit();
    }
    public class RulebaseLastHit
    {
        [JsonProperty("rulesWithHits"), JsonPropertyName("rulesWithHits")]
        public AggregateCount RulesWithHits {get; set;} = new AggregateCount();
    }

    public class AggregateCount
    {
        [JsonProperty("aggregate"), JsonPropertyName("aggregate")]
        public Aggregate Aggregate {get; set;} = new Aggregate();
    }

    public class Aggregate
    {
        [JsonProperty("count"), JsonPropertyName("count")]     
        public int Count { get; set; }
    }
}

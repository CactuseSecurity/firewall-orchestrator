using System.Text.Json.Serialization;
using Newtonsoft.Json;
using FWO.Data;

namespace FWO.Data
{
    public class ReturnId
    {
        [JsonProperty("newId"), JsonPropertyName("newId")]
        public int NewId { get; set; }

        [JsonProperty("newIdLong"), JsonPropertyName("newIdLong")]
        public long NewIdLong { get; set; }

        [JsonProperty("updatedId"), JsonPropertyName("updatedId")]
        public int UpdatedId { get; set; }

        [JsonProperty("id"), JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonProperty("updatedIdLong"), JsonPropertyName("updatedIdLong")]
        public long UpdatedIdLong { get; set; }

        [JsonProperty("deletedId"), JsonPropertyName("deletedId")]
        public int DeletedId { get; set; }

        [JsonProperty("deletedIdLong"), JsonPropertyName("deletedIdLong")]
        public long DeletedIdLong { get; set; }

        [JsonProperty("insertedId"), JsonPropertyName("insertedId")]
        public int InsertedId { get; set; }

        [JsonProperty("insertedIdLong"), JsonPropertyName("insertedIdLong")]
        public long InsertedIdLong { get; set; }

        [JsonProperty("affected_rows"), JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }

        [JsonProperty("uiuser_password_must_be_changed"), JsonPropertyName("uiuser_password_must_be_changed")]
        public bool PasswordMustBeChanged { get; set; }
    }

    public class ReturnIdWrapper
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
        [JsonProperty("rulebase_link"), JsonPropertyName("rulebase_link")]
        public List<RulebaseLink> RulebasesOnGateway { get; set; } = [];
    }

    public class RulebaseOnGatewaysLastHit
    {
        [JsonProperty("rulebase"), JsonPropertyName("rulebase")]
        public RulebaseLastHit Rulebase { get; set; } = new RulebaseLastHit();
    }
    
    public class RulebaseLastHit
    {
        [JsonProperty("rulesWithHits"), JsonPropertyName("rulesWithHits")]
        public AggregateCount RulesWithHits { get; set; } = new AggregateCount();
    }

    public class AggregateCount
    {
        [JsonProperty("aggregate"), JsonPropertyName("aggregate")]
        public Aggregate Aggregate { get; set; } = new Aggregate();
    }

    public class Aggregate
    {
        [JsonProperty("count"), JsonPropertyName("count")]
        public int Count { get; set; }
    }
}

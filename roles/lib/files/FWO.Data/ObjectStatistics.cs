using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class ObjectStatistics
    {
        [JsonProperty("aggregate"), JsonPropertyName("aggregate")]
        public ObjectAggregate ObjectAggregate { get; set; } = new ObjectAggregate();

        
        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used by JSON deserialization")]
        private ObjectStatistics RulesAggregateWrapper
        {
            get => default!;
            set
            {
                if (value != null && value.ObjectAggregate != null)
                {
                    ObjectAggregate = value.ObjectAggregate;
                }
            }
        }
    }

    public class ObjectAggregate
    {
        [JsonProperty("count"), JsonPropertyName("count")]
        public int ObjectCount { get; set; } = 0;

    }

}


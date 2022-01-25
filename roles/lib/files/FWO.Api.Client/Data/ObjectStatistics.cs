using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ObjectStatistics
    {
        [JsonProperty("aggregate"), JsonPropertyName("aggregate")]
        public ObjectAggregate ObjectAggregate { get; set; } = new ObjectAggregate();
    }

    public class ObjectAggregate
    {
        [JsonProperty("count"), JsonPropertyName("count")]
        public int ObjectCount { get; set; } = 0;

    }

}


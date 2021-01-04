using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ObjectStatistics
    {
        [JsonPropertyName("aggregate")]
        public ObjectAggregate ObjectAggregate { get; set; } = new ObjectAggregate();
    }

    public class ObjectAggregate
    {
        [JsonPropertyName("count")]
        public int ObjectCount { get; set; } = 0;

    }

}


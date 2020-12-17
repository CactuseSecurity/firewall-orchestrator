using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ObjectStatistics
    {
        [JsonPropertyName("aggregate")]
        public ElementAggregate ElementAggregate { get; set; }
    }

}

using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ElementAggregate
    {
        [JsonPropertyName("aggregate")]
        public Counter Counter { get; set; }
    }

    public class Counter
    {
        [JsonPropertyName("count")]
        public int ElementNumber { get; set; }
    }

}

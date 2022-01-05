using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class Import
    {
        [JsonProperty("aggregate"), JsonPropertyName("aggregate")]
        public ImportAggregate ImportAggregate { get; set; } = new ImportAggregate();
    }

    public class ImportAggregate
    {
        [JsonProperty("max"), JsonPropertyName("max")]
        public ImportAggregateMax ImportAggregateMax { get; set; } = new ImportAggregateMax();
    }

    public class ImportAggregateMax
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long? RelevantImportId { get; set; }
    }

}

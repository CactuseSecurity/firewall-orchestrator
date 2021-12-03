using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class Import
    {
        [JsonPropertyName("aggregate")]
        public ImportAggregate ImportAggregate { get; set; } = new ImportAggregate();
    }

    public class ImportAggregate
    {
        [JsonPropertyName("max")]
        public ImportAggregateMax ImportAggregateMax { get; set; } = new ImportAggregateMax();
    }

    public class ImportAggregateMax
    {
        [JsonPropertyName("id")]
        public long? RelevantImportId { get; set; }
    }

}

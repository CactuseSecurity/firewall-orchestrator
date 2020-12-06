using System.Text.Json.Serialization;

namespace FWO.Ui.Data.API
{
    public class Import
    {
        [JsonPropertyName("aggregate")]
        public ImportAggregate ImportAggregate { get; set; }
    }

    public class ImportAggregate
    {
        [JsonPropertyName("max")]
        public ImportAggregateMax ImportAggregateMax { get; set; }
    }

    public class ImportAggregateMax
    {
        [JsonPropertyName("id")]
        public int? RelevantImportId { get; set; }
    }

}

﻿using System.Text.Json.Serialization;

namespace FWO.Api.Data
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
        public long? RelevantImportId { get; set; }
    }

}

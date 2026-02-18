using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class OwnerMappingSource
    {
        [JsonProperty("owner_mapping_source_type_id"), JsonPropertyName("owner_mapping_source_type_id")]
        public int Id { get; set; }

        [JsonProperty("owner_mapping_source_type_name"), JsonPropertyName("owner_mapping_source_type_name")]
        public string Name { get; set; } = "";
    }
}

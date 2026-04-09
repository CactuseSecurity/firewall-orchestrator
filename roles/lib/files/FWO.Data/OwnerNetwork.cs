using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class OwnerNetwork
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string IP { get; set; } = "";

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string IpEnd { get; set; } = "";

        [JsonProperty("port"), JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int IpProtoId { get; set; }

        [JsonProperty("nw_type"), JsonPropertyName("nw_type")]
        public int NwType { get; set; }

        [JsonProperty("import_source"), JsonPropertyName("import_source")]
        public string ImportSource { get; set; } = "";

        [JsonProperty("is_deleted"), JsonPropertyName("is_deleted")]
        public bool IsDeleted { get; set; }

        [JsonProperty("custom_type"), JsonPropertyName("custom_type")]
        public int CustomType { get; set; }

    }
}
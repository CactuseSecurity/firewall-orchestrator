﻿using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class PaginationVariables
    {
        [JsonProperty("management_id", DefaultValueHandling = DefaultValueHandling.Ignore), JsonPropertyName("management_id")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ManagementId { get; set; }

        [JsonProperty("device_id", DefaultValueHandling = DefaultValueHandling.Ignore), JsonPropertyName("device_id")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int DeviceId { get; set; }

        [JsonProperty("limit"), JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonProperty("offset"), JsonPropertyName("offset")]
        public int Offset { get; set; }
    }
}

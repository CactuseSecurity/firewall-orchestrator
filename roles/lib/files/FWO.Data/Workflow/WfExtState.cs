﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Data.Workflow
{
    public class WfExtState
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int? StateId { get; set; }
    }
}

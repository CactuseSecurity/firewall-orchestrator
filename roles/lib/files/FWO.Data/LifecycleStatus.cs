using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class LifecycleStatus
    {
        [JsonProperty("lifecycleStatus_id"), JsonPropertyName("lifecycleStatus_id")]
        public int Id { get; set; }

        [JsonProperty("lifecycleStatus_name"), JsonPropertyName("lifecycleStatus_name")]
        public string Name { get; set; } = "";

        public LifecycleStatus()
        { }

        public LifecycleStatus(LifecycleStatus lifecycleStatus)
        {
            Id = lifecycleStatus.Id;
            Name = lifecycleStatus.Name;
        }
    }
}

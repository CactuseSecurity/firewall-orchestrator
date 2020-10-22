using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.Api
{
    public class DeviceType
    {
        [JsonPropertyName("dev_typ_id")]
        public int Id { get; set; }

        [JsonPropertyName("dev_typ_name")]
        public string Name { get; set; }

        [JsonPropertyName("dev_typ_version")]
        public string Version { get; set; }
    }
}

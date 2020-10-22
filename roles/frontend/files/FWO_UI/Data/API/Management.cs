using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class Management
    {
        [JsonPropertyName("mgm_id")]
        public int Id { get; set; }

        [JsonPropertyName("mgm_name")]
        public string Name { get; set; }

        [JsonPropertyName("dev_typ_id")]
        public int Type { get; set; }

        [JsonPropertyName("ssh_private_key")]
        public string PrivateKey { get; set; }

        [JsonPropertyName("devices")]
        public Device[] Devices { get; set; }

        [JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; }

        [JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; }

        [JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; }
    }

    public class ReturnManagement
    {
        [JsonPropertyName("returning")]
        public Management[] ReturnId { get; set; }
    }
}

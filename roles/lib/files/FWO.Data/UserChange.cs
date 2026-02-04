using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Data
{
    public class UserChange
    {
        [JsonProperty("import"), JsonPropertyName("import")]
        public ChangeImport ChangeImport { get; set; } = new ChangeImport();

        [JsonProperty("change_action"), JsonPropertyName("change_action")]
        public char ChangeAction { get; set; }

        [JsonProperty("old"), JsonPropertyName("old")]
        public NetworkUser OldUser { get; set; } = new NetworkUser();

        [JsonProperty("new"), JsonPropertyName("new")]
        public NetworkUser NewUser { get; set; } = new NetworkUser();
    }
}

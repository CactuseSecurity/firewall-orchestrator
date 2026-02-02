using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Data
{
    public class ObjectChange
    {
        [JsonProperty("import"), JsonPropertyName("import")]
        public ChangeImport ChangeImport { get; set; } = new ChangeImport();

        [JsonProperty("change_action"), JsonPropertyName("change_action")]
        public char ChangeAction { get; set; }

        [JsonProperty("old"), JsonPropertyName("old")]
        public NetworkObject OldObject { get; set; } = new NetworkObject();

        [JsonProperty("new"), JsonPropertyName("new")]
        public NetworkObject NewObject { get; set; } = new NetworkObject();
    }
}

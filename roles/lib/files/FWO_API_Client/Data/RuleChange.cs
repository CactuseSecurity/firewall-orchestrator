using System;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class RuleChange
    {
        [JsonPropertyName("import")]
        public ChangeImport ChangeImport { get; set; }

        [JsonPropertyName("change_action")]
        public char ChangeAction { get; set; }

        [JsonPropertyName("old")]
        public Rule OldRule { get; set; }

        [JsonPropertyName("new")]
        public Rule NewRule { get; set; }

        public string DeviceName { get; set; }
    }
}

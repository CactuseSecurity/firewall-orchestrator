using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class RuleChange
    {
        [JsonPropertyName("import")]
        public ChangeImport ChangeImport { get; set; } = new ChangeImport();

        [JsonPropertyName("change_action")]
        public char ChangeAction { get; set; }

        [JsonPropertyName("old")]
        public Rule OldRule { get; set; } = new Rule();

        [JsonPropertyName("new")]
        public Rule NewRule { get; set; } = new Rule();

        public string DeviceName { get; set; } = "";
    }
}

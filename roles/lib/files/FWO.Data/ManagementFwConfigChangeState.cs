using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public static class ManagementFwConfigChangeTargets
    {
        public const string Disabled = "Disabled";
        public const string InternalWork = "InternalWork";
    }

    public static class ManagementFwConfigChangeCategories
    {
        public const string ObjectChanges = "Object Changes";
        public const string RuleChanges = "Rule Changes";

        public static readonly IReadOnlyList<string> All =
        [
            ObjectChanges,
            RuleChanges
        ];
    }

    public class ManagementFwConfigChangeState
    {
        [JsonProperty(nameof(Id)), JsonPropertyName(nameof(Id))]
        public int Id { get; set; }

        [JsonProperty(nameof(Name)), JsonPropertyName(nameof(Name))]
        public string Name { get; set; } = "";

        [JsonProperty(nameof(Enabled)), JsonPropertyName(nameof(Enabled))]
        public bool Enabled { get; set; } = false;

        [JsonProperty(nameof(SelectedChanges)), JsonPropertyName(nameof(SelectedChanges))]
        public Dictionary<string, string> SelectedChanges { get; set; } = [];
    }
}

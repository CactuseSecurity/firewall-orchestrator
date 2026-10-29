using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Label assigned to a rule as a whole (labelling.rule_label), e.g. the App label.
    /// </summary>
    public class RuleLabelAssignment
    {
        /// <summary>
        /// The assigned label.
        /// </summary>
        [JsonProperty("label_value"), JsonPropertyName("label_value")]
        public LabelValue Label { get; set; } = new();
    }
}

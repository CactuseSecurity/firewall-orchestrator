using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedGateway
    {
        [JsonProperty("Uid"), JsonPropertyName("Uid")]
        public string? Uid { get; set; }

        [JsonProperty("Name"), JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonProperty("Routing"), JsonPropertyName("Routing")]
        public object[] Routing { get; set; } = [];

        [JsonProperty("Interfaces"), JsonPropertyName("Interfaces")]
        public object[] Interfaces { get; set; } = [];

        [JsonProperty("RulebaseLinks"), JsonPropertyName("RulebaseLinks")]
        public NormalizedRulebaseLink[] RulebaseLinks { get; set; } = [];

        [JsonProperty("GlobalPolicyUid"), JsonPropertyName("GlobalPolicyUid")]
        public string? GlobalPolicyUid { get; set; }

        [JsonProperty("EnforcedPolicyUids"), JsonPropertyName("EnforcedPolicyUids")]
        public string[]? EnforcedPolicyUids { get; set; }

        [JsonProperty("EnforcedNatPolicyUids"), JsonPropertyName("EnforcedNatPolicyUids")]
        public string[]? EnforcedNatPolicyUids { get; set; }

        [JsonProperty("ImportDisabled"), JsonPropertyName("ImportDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonProperty("ShowInUI"), JsonPropertyName("ShowInUI")]
        public bool ShowInUI { get; set; }

        public static NormalizedGateway FromDevice(Device device)
        {
            return new NormalizedGateway
            {
                Uid = device.Uid,
                Name = device.Name,
                Routing = [], // TODO: implement (see #3645)
                Interfaces = [], // TODO: implement (see #3645)
                RulebaseLinks = [.. device.RulebaseLinks.Select(NormalizedRulebaseLink.FromRulebaseLink)],
                GlobalPolicyUid = device.GlobalRulebaseUid,
                EnforcedPolicyUids = [], // TODO: implement (see #3645)
                EnforcedNatPolicyUids = [], // TODO: implement - property not available in Device (see #3645)
                ImportDisabled = device.ImportDisabled,
                ShowInUI = !device.HideInUi
            };
        }
    }
}

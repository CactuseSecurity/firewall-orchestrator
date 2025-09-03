using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedGateway
    {
        [JsonProperty("Uid"), JsonPropertyName("Uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("Name"), JsonPropertyName("Name")]
        public string Name { get; set; } = "";

        [JsonProperty("Routing"), JsonPropertyName("Routing")]
        public object[] Routing { get; set; } = [];

        [JsonProperty("Interfaces"), JsonPropertyName("Interfaces")]
        public object[] Interfaces { get; set; } = [];

        [JsonProperty("RulebaseLinks"), JsonPropertyName("RulebaseLinks")]
        public NormalizedRulebaseLink[] RulebaseLinks { get; set; } = [];

        [JsonProperty("GlobalPolicyUid"), JsonPropertyName("GlobalPolicyUid")]
        public string? GlobalPolicyUid { get; set; }

        [JsonProperty("EnforcedPolicyUids"), JsonPropertyName("EnforcedPolicyUids")]
        public string[] EnforcedPolicyUids { get; set; } = [];

        [JsonProperty("EnforcedNatPolicyUids"), JsonPropertyName("EnforcedNatPolicyUids")]
        public string[] EnforcedNatPolicyUids { get; set; } = [];

        [JsonProperty("ImportDisabled"), JsonPropertyName("ImportDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonProperty("ShowInUI"), JsonPropertyName("ShowInUI")]
        public bool ShowInUI { get; set; }

        public static NormalizedGateway FromDevice(Device device)
        {
            return new NormalizedGateway
            {
                Uid = device.Uid ?? "",
                Name = device.Name ?? "",
                Routing = [], // TODO: implement
                Interfaces = [], // TODO: implement
                RulebaseLinks = device.RulebaseLinks.Select(rl => NormalizedRulebaseLink.FromRulebaseLink(rl)).ToArray(),
                GlobalPolicyUid = "", // TODO: implement
                EnforcedPolicyUids = [], // TODO: implement
                EnforcedNatPolicyUids = [], // TODO: implement - property not available in Device
                ImportDisabled = device.ImportDisabled,
                ShowInUI = !device.HideInUi // Use inverse of HideInUi
            };
        }
    }
}

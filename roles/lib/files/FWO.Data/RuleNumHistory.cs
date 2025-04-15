using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class RuleNumHistory
    {
        [JsonProperty("rule_num"), JsonPropertyName("rule_num")]
        public int RuleNum { get; set; }
    }
}
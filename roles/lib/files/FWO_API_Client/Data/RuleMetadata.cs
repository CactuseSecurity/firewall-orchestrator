using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class RuleMetadata
    {
        [JsonPropertyName("rule_created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("rule_last_modified")]
        public DateTime? LastModified { get; set; }

        [JsonPropertyName("rule_first_hit")]
        public DateTime? FirstHit { get; set; }

        [JsonPropertyName("rule_last_hit")]
        public DateTime? LastHit { get; set; }
    }
}

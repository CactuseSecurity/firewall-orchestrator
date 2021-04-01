using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class RulePaginationVariables : PaginationVariables
    {
        [JsonPropertyName("rule_uid")]
        public string RuleUid { get; set; }
    }
}

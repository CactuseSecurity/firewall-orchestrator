using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RuleMetadata
    {
        [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
        public long Id { get; set; }

        [JsonProperty("rule_created"), JsonPropertyName("rule_created")]
        public DateTime? Created { get; set; }

        [JsonProperty("rule_last_modified"), JsonPropertyName("rule_last_modified")]
        public DateTime? LastModified { get; set; }

        [JsonProperty("rule_first_hit"), JsonPropertyName("rule_first_hit")]
        public DateTime? FirstHit { get; set; }

        [JsonProperty("rule_last_hit"), JsonPropertyName("rule_last_hit")]
        public DateTime? LastHit { get; set; }

        [JsonProperty("rule_last_certified"), JsonPropertyName("rule_last_certified")]
        public DateTime? LastCertified { get; set; }

        [JsonProperty("rule_last_certifier_dn"), JsonPropertyName("rule_last_certifier_dn")]
        public string LastCertifierDn { get; set; } = "";

        [JsonProperty("rule_to_be_removed"), JsonPropertyName("rule_to_be_removed")]
        public bool ToBeRemoved { get; set; }

        [JsonProperty("rule_decert_date"), JsonPropertyName("rule_decert_date")]
        public DateTime? DecertificationDate { get; set; }

        [JsonProperty("rule_recertification_comment"), JsonPropertyName("rule_recertification_comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("recertification"), JsonPropertyName("recertification")]
        public List<Recertification> RuleRecertification { get; set; } = new List<Recertification>();

        [JsonProperty("recert_history"), JsonPropertyName("recert_history")]
        public List<Recertification> RecertHistory { get; set; } = new List<Recertification>();

        [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
        public int DeviceId { get; set; }

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? Uid { get; set; } = "";

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public Rule[] Rules { get; set; } = [];

        public DateTime NextRecert { get; set; }

        public string LastCertifierName { get; set; } = "";

        public bool Recert { get; set; }

        public string Style { get; set; } = "";

        public void UpdateRecertPeriods(int recertificationPeriod, int recertificationNoticePeriod)
        {

            if (LastCertifierDn != null && LastCertifierDn != "")
                LastCertifierName = (new FWO.Api.Data.DistName(LastCertifierDn)).UserName;
            else
                LastCertifierName = "-";

            if (LastCertified != null)
                NextRecert = ((DateTime)LastCertified).AddDays(recertificationPeriod);
            else if (Created != null)
                NextRecert = ((DateTime)Created).AddDays(recertificationPeriod);
            else
                NextRecert = DateTime.Now;

            if (NextRecert <= DateTime.Now)
                Style = "background-overdue";
            else if (NextRecert <= DateTime.Now.AddDays(recertificationNoticePeriod))
                Style = "background-upcoming";
        }
    }

}

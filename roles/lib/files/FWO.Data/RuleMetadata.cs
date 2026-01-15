using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using NewtonsoftJsonIgnore = Newtonsoft.Json.JsonIgnoreAttribute;
using SystemTextJsonIgnore = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace FWO.Data
{
    public class RuleMetadata
    {
        [JsonProperty("rule_metadata_id"), JsonPropertyName("rule_metadata_id")]
        public long Id { get; set; }

        [JsonProperty("rule_created"), JsonPropertyName("rule_created")]
        public long? CreatedImportId { get; set; }

        [JsonProperty("created_import"), JsonPropertyName("created_import")]
        public ImportControl? CreatedImport { get; set; }

        [JsonProperty("rule_last_modified"), JsonPropertyName("rule_last_modified")]
        public long? LastModifiedImportId { get; set; }

        [JsonProperty("last_modified_import"), JsonPropertyName("last_modified_import")]
        public ImportControl? LastModifiedImport { get; set; }

        [JsonProperty("rule_first_hit"), JsonPropertyName("rule_first_hit")]
        public DateTime? FirstHit { get; set; }

        [JsonProperty("rule_last_hit"), JsonPropertyName("rule_last_hit")]
        public DateTime? LastHit { get; set; }

        [JsonProperty("recertification"), JsonPropertyName("recertification")]
        public List<Recertification> RuleRecertification { get; set; } = [];

        [JsonProperty("recert_history"), JsonPropertyName("recert_history")]
        public List<Recertification> RecertHistory { get; set; } = [];

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? Uid { get; set; } = "";

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public Rule[] Rules { get; set; } = [];

        [SystemTextJsonIgnore, NewtonsoftJsonIgnore]
        public DateTime? Created => CreatedImport?.StartTime;

        [SystemTextJsonIgnore, NewtonsoftJsonIgnore]
        public DateTime? LastModified => LastModifiedImport?.StartTime;

        [SystemTextJsonIgnore, NewtonsoftJsonIgnore]
        public string Comment => RecertHistory.OrderByDescending(r => r.RecertDate).FirstOrDefault()?.Comment ?? "";

        [SystemTextJsonIgnore, NewtonsoftJsonIgnore]
        public DateTime? LastCertified => RecertHistory.Where(r => r.Recertified)
            .OrderByDescending(r => r.RecertDate).FirstOrDefault()?.RecertDate;

        [SystemTextJsonIgnore, NewtonsoftJsonIgnore]
        public DateTime? DecertificationDate => RecertHistory.Where(r => !r.Recertified)
            .OrderByDescending(r => r.RecertDate).FirstOrDefault()?.RecertDate;

        [SystemTextJsonIgnore, NewtonsoftJsonIgnore]
        public bool ToBeRemoved { get; set; }

        public DateTime NextRecert { get; set; }

        public string LastCertifierName { get; set; } = "";

        public bool Recert { get; set; }

        public string Style { get; set; } = "";

        public void UpdateRecertPeriods(int recertificationPeriod, int recertificationNoticePeriod)
        {
            Recertification? latestRecert = RecertHistory.OrderByDescending(r => r.RecertDate).FirstOrDefault();
            LastCertifierName = latestRecert?.UserDn != null ? new DistName(latestRecert.UserDn).UserName : "-";

            DateTime? nextRecertFromData = RuleRecertification.Where(r => r.NextRecertDate != null)
                .Select(r => r.NextRecertDate)
                .OrderBy(d => d)
                .FirstOrDefault();

            NextRecert = nextRecertFromData ?? DateTime.Now;

            if (NextRecert <= DateTime.Now)
            {
                Style = "background-overdue";
            }
            else if (NextRecert <= DateTime.Now.AddDays(recertificationNoticePeriod))
            {
                Style = "background-upcoming";
            }
        }
    }
}

using System;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ImportControl
    {
        [JsonPropertyName("control_id")]
        public int ControlId { get; set; }

        [JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [JsonPropertyName("stop_time")]
        public DateTime? StopTime { get; set; }

        [JsonPropertyName("successful_import")]
        public bool SuccessfulImport { get; set; }

        [JsonPropertyName("import_errors")]
        public string ImportErrors { get; set; }

        [JsonPropertyName("last_change_in_config")]
        public DateTime? LastChangeInConfig { get; set; }
    }

    public class ImportStatus
    {
        [JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonPropertyName("mgm_name")]
        public string MgmName { get; set; }

        [JsonPropertyName("last_import")]
        public ImportControl[] LastImport { get; set; }

        [JsonPropertyName("first_import")]
        public ImportControl[] FirstImport { get; set; }
    }
}

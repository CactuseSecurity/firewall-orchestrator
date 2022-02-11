using System;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ImportControl
    {
        [JsonProperty("control_id"), JsonPropertyName("control_id")]
        public long ControlId { get; set; }

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("stop_time"), JsonPropertyName("stop_time")]
        public DateTime? StopTime { get; set; }

        [JsonProperty("successful_import"), JsonPropertyName("successful_import")]
        public bool SuccessfulImport { get; set; }

        [JsonProperty("import_errors"), JsonPropertyName("import_errors")]
        public string? ImportErrors { get; set; }
    }

    public class ImportStatus
    {
        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonProperty("mgm_name"), JsonPropertyName("mgm_name")]
        public string MgmName { get; set; } = "";
        
        [JsonProperty("importDisabled"), JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        public int SortPrio = 0;

        [JsonProperty("deviceType"), JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; } = new DeviceType();

        [JsonProperty("last_import"), JsonPropertyName("last_import")]
        public ImportControl[]? LastImport { get; set; }

        [JsonProperty("last_successful_import"), JsonPropertyName("last_successful_import")]
        public ImportControl[]? LastSuccessfulImport { get; set; }

        [JsonProperty("last_incomplete_import"), JsonPropertyName("last_incomplete_import")]
        public ImportControl[]? LastIncompleteImport { get; set; }

        [JsonProperty("first_import"), JsonPropertyName("first_import")]
        public ImportControl[]? FirstImport { get; set; }
        
        [JsonProperty("erroneous_imports"), JsonPropertyName("erroneous_imports")]
        public ImportControl[]? ErroneousImports { get; set; }

        public int ErrorCount = 0;
    }
}

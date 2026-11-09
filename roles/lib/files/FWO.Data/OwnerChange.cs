using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class OwnerChange
    {
        [JsonProperty("import"), JsonPropertyName("import")]
        public ChangeImport ChangeImport { get; set; } = new ChangeImport();

        [JsonProperty("change_action"), JsonPropertyName("change_action")]
        public char ChangeAction { get; set; }

        [JsonProperty("old"), JsonPropertyName("old")]
        public FwoOwner OldOwner { get; set; } = new FwoOwner();

        [JsonProperty("new"), JsonPropertyName("new")]
        public FwoOwner NewOwner { get; set; } = new FwoOwner();

        [JsonProperty("source_id"), JsonPropertyName("source_id")]
        public string source_id { get; set; } = "";

        public OwnerChange()
        { }

        public OwnerChange(OwnerChange change)
        {
            ChangeImport = new ChangeImport(change.ChangeImport);
            ChangeAction = change.ChangeAction;
            OldOwner = change.OldOwner;
            NewOwner = change.NewOwner;
            source_id = change.source_id;
        }
    }
}

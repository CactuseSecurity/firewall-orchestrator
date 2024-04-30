using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class FwoOwner : FwoOwnerBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("last_recert_check"), JsonPropertyName("last_recert_check")]
        public DateTime? LastRecertCheck { get; set; }

        [JsonProperty("recert_check_params"), JsonPropertyName("recert_check_params")]
        public string? RecertCheckParamString { get; set; }

        [JsonProperty("criticality"), JsonPropertyName("criticality")]
        public string? Criticality { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("import_source"), JsonPropertyName("import_source")]
        public string? ImportSource { get; set; }

        [JsonProperty("common_service_possible"), JsonPropertyName("common_service_possible")]
        public bool CommSvcPossible { get; set; } = false;

        [JsonProperty("connections_aggregate"), JsonPropertyName("connections_aggregate")]
        public Client.AggregateCount ConnectionCount { get; set; } = new();


        public FwoOwner()
        { }

        public FwoOwner(FwoOwner owner) : base(owner)
        {
            Id = owner.Id;
            LastRecertCheck = owner.LastRecertCheck;
            RecertCheckParamString = owner.RecertCheckParamString;
            Criticality = owner.Criticality;
            Active = owner.Active;
            ImportSource = owner.ImportSource;
            CommSvcPossible = owner.CommSvcPossible;
            ConnectionCount = owner.ConnectionCount;
        }

        public string Display(string comSvcTxt)
        {
            return Name + " (" + ExtAppId + (CommSvcPossible? $", {comSvcTxt}" : "") + ")";
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Criticality = Sanitizer.SanitizeOpt(Criticality, ref shortened);
            ImportSource = Sanitizer.SanitizeCommentOpt(ImportSource, ref shortened);
            return shortened;
        }
    }

    public class FwoOwnerDataHelper
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner { get; set; } = new FwoOwner();
    }

}

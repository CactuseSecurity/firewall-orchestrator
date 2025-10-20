using FWO.Basics;
using Newtonsoft.Json;
using System.Text.Json.Serialization; 

namespace FWO.Data
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
        public AggregateCount ConnectionCount { get; set; } = new();

        [JsonProperty("last_recertified"), JsonPropertyName("last_recertified")]
        public DateTime? LastRecertified { get; set; }

        [JsonProperty("last_recertifier"), JsonPropertyName("last_recertifier")]
        public int? LastRecertifierId { get; set; }

        [JsonProperty("last_recertifier_dn"), JsonPropertyName("last_recertifier_dn")]
        public string? LastRecertifierDn { get; set; }

        [JsonProperty("next_recert_date"), JsonPropertyName("next_recert_date")]
        public DateTime? NextRecertDate { get; set; }

        [JsonProperty("recert_active"), JsonPropertyName("recert_active")]
        public bool RecertActive { get; set; } = false;

        public bool RecertOverdue { get; set; } = false;
        public bool RecertUpcoming { get; set; } = false;
        public long LastRecertId { get; set; } = 0;

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
            LastRecertified = owner.LastRecertified;
            LastRecertifierId = owner.LastRecertifierId;
            LastRecertifierDn = owner.LastRecertifierDn;
            NextRecertDate = owner.NextRecertDate;
            RecertOverdue = owner.RecertOverdue;
            RecertUpcoming = owner.RecertUpcoming;
            LastRecertId = owner.LastRecertId;
        }

        public string Display(string comSvcTxt)
        {
            string comSvcAppendix = CommSvcPossible && comSvcTxt != "" ? $", {comSvcTxt}" : "";
            string appIdPart = !string.IsNullOrEmpty(ExtAppId) ? $" ({ExtAppId}{comSvcAppendix})" : "";

            return $"{Name}{appIdPart}";
        }
        
        public string DisplayWithoutAppId(string comSvcTxt)
        {
            if (CommSvcPossible)
            {
                return $"{Name} ({comSvcTxt})";
            }

            return $"{Name}";
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Criticality = Criticality.SanitizeOpt(ref shortened);
            ImportSource = ImportSource.SanitizeCommentOpt(ref shortened);
            LastRecertifierDn = LastRecertifierDn.SanitizeLdapPathOpt(ref shortened);
            return shortened;
        }

        public int CompareTo(FwoOwner secondOwner)
        {
            if(Id <= 0 || secondOwner.Id <= 0)
            {
                return Id.CompareTo(secondOwner.Id);
            }
            return Name?.CompareTo(secondOwner.Name) ?? -1;
        }
    }

    public class FwoOwnerDataHelper
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner { get; set; } = new FwoOwner();
    }

}

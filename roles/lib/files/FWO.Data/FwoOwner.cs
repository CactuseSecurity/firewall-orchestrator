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

        [JsonProperty("additional_info"), JsonPropertyName("additional_info")]
        public Dictionary<string, string>? AdditionalInfo { get; set; }

        [JsonProperty("criticality"), JsonPropertyName("criticality")]
        public string? Criticality { get; set; }

        [JsonProperty("owner_lifecycle_state_id"), JsonPropertyName("owner_lifecycle_state_id")]
        public int? OwnerLifeCycleStateId { get; set; }

        [JsonProperty("owner_lifecycle_state"), JsonPropertyName("owner_lifecycle_state")]
        public OwnerLifeCycleState? OwnerLifeCycleState { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("import_source"), JsonPropertyName("import_source")]
        public string? ImportSource { get; set; }

        [JsonProperty("owner_networks"), JsonPropertyName("owner_networks")]
        public OwnerNetwork[] OwnerNetworks { get; set; } = [];

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

        [JsonProperty("changelog_owners"), JsonPropertyName("changelog_owners")]
        public List<OwnerChange> ChangelogOwners { get; set; } = [];

        [JsonProperty("decomm_date"), JsonPropertyName("decomm_date")]
        public DateTime? DecommDate { get; set; }

        [JsonProperty("recert_active"), JsonPropertyName("recert_active")]
        public bool RecertActive { get; set; } = false;

        public bool RecertOverdue { get; set; } = false;
        public bool RecertUpcoming { get; set; } = false;
        public long? LastRecertId { get; set; }

        public FwoOwner()
        { }

        public FwoOwner(FwoOwner owner) : base(owner)
        {
            Id = owner.Id;
            LastRecertCheck = owner.LastRecertCheck;
            RecertCheckParamString = owner.RecertCheckParamString;
            AdditionalInfo = owner.AdditionalInfo == null ? null : new Dictionary<string, string>(owner.AdditionalInfo);
            Criticality = owner.Criticality;
            OwnerLifeCycleStateId = owner.OwnerLifeCycleStateId;
            OwnerLifeCycleState = owner.OwnerLifeCycleState == null ? null : new OwnerLifeCycleState(owner.OwnerLifeCycleState);
            Active = owner.Active;
            ImportSource = owner.ImportSource;
            CommSvcPossible = owner.CommSvcPossible;
            ConnectionCount = owner.ConnectionCount;
            LastRecertified = owner.LastRecertified;
            LastRecertifierId = owner.LastRecertifierId;
            LastRecertifierDn = owner.LastRecertifierDn;
            NextRecertDate = owner.NextRecertDate;
            ChangelogOwners = owner.ChangelogOwners.Select(change => new OwnerChange(change)).ToList();
            DecommDate = owner.DecommDate;
            RecertActive = owner.RecertActive;
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
            if (Id <= 0 || secondOwner.Id <= 0)
            {
                return Id.CompareTo(secondOwner.Id);
            }
            return Name?.CompareTo(secondOwner.Name) ?? -1;
        }

        public bool UsesCreationDateFallback()
        {
            return LastRecertified == null && NextRecertDate == null && GetOwnerCreationDateHint() != null;
        }

        public DateTime? GetEffectiveLastRecertified()
        {
            return LastRecertified ?? GetOwnerCreationDateHint();
        }

        public DateTime? GetEffectiveNextRecertDate(int defaultRecertInterval)
        {
            if (NextRecertDate != null)
            {
                return NextRecertDate;
            }

            DateTime? ownerCreationDate = GetOwnerCreationDateHint();
            if (ownerCreationDate == null)
            {
                return null;
            }
            int recertInterval = RecertInterval ?? defaultRecertInterval;
            return recertInterval > 0 ? ownerCreationDate.Value.AddDays(recertInterval) : null;
        }

        private DateTime? GetOwnerCreationDateHint()
        {
            DateTime ownerCreationDate = ChangelogOwners
                .Where(change => change.ChangeAction == ChangelogActionType.INSERT && change.ChangeImport.Time != default)
                .Select(change => change.ChangeImport.Time)
                .OrderBy(time => time)
                .FirstOrDefault();
            return ownerCreationDate == default ? null : ownerCreationDate;
        }
    }

    public class FwoOwnerDataHelper
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner { get; set; } = new FwoOwner();
    }

}

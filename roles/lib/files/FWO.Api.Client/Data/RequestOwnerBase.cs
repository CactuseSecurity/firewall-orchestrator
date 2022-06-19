using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestOwnerBase
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("dn"), JsonPropertyName("dn")]
        public string Dn { get; set; } = "";

        [JsonProperty("group_dn"), JsonPropertyName("group_dn")]
        public string GroupDn { get; set; } = "";

        [JsonProperty("is_default"), JsonPropertyName("is_default")]
        public bool IsDefault { get; set; } = false;

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonProperty("recert_interval"), JsonPropertyName("recert_interval")]
        public DateTime? RecertInterval { get; set; }

        [JsonProperty("next_recert_date"), JsonPropertyName("next_recert_date")]
        public DateTime? NextRecertDate { get; set; }

        [JsonProperty("app_id_external"), JsonPropertyName("app_id_external")]
        public string ExtAppId { get; set; } = "";


        public RequestOwnerBase()
        { }

        public RequestOwnerBase(RequestOwnerBase owner)
        {
            Name = owner.Name;
            Dn = owner.Dn;
            GroupDn = owner.GroupDn;
            IsDefault = owner.IsDefault;
            TenantId = owner.TenantId;
            RecertInterval = owner.RecertInterval;
            NextRecertDate = owner.NextRecertDate;
            ExtAppId = owner.ExtAppId;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Dn = Sanitizer.SanitizeLdapNameMand(Dn, ref shortened);
            GroupDn = Sanitizer.SanitizeLdapNameMand(GroupDn, ref shortened);
            ExtAppId = Sanitizer.SanitizeMand(ExtAppId, ref shortened);
            return shortened;
        }
    }
}

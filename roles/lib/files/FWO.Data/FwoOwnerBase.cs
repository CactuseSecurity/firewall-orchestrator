﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public enum RuleOwnershipMode
    {
        mixed, 
        exclusive
    }

    public class FwoOwnerBase
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
        public int? RecertInterval { get; set; }

        [JsonProperty("app_id_external"), JsonPropertyName("app_id_external")]
        public string? ExtAppId { get; set; }


        public FwoOwnerBase()
        { }

        public FwoOwnerBase(FwoOwnerBase owner)
        {
            Name = owner.Name;
            Dn = owner.Dn;
            GroupDn = owner.GroupDn;
            IsDefault = owner.IsDefault;
            TenantId = owner.TenantId;
            RecertInterval = owner.RecertInterval;
            ExtAppId = owner.ExtAppId;
        }

        public virtual string Display()
        {
            return Name + " (" + ExtAppId + ")";
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Dn = Sanitizer.SanitizeLdapPathMand(Dn, ref shortened);
            GroupDn = Sanitizer.SanitizeLdapPathMand(GroupDn, ref shortened);
            ExtAppId = Sanitizer.SanitizeCommentOpt(ExtAppId, ref shortened);
            return shortened;
        }
    }
}

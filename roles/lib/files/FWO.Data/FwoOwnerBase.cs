using FWO.Basics;
using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization; 
using System.Linq;

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

        [JsonProperty("owner_responsibles"), JsonPropertyName("owner_responsibles")]
        public List<OwnerResponsible> OwnerResponsibles { get; set; } = [];

        [JsonProperty("is_default"), JsonPropertyName("is_default")]
        public bool IsDefault { get; set; } = false;

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonProperty("recert_interval"), JsonPropertyName("recert_interval")]
        public int? RecertInterval { get; set; }

        [JsonProperty("app_id_external"), JsonPropertyName("app_id_external")]
        public string? ExtAppId { get; set; }

        public string OwnerResponsiblesType1Key => string.Join(", ", GetOwnerResponsiblesByType(1));
        public string OwnerResponsiblesType2Key => string.Join(", ", GetOwnerResponsiblesByType(2));
        public string OwnerResponsiblesType3Key => string.Join(", ", GetOwnerResponsiblesByType(3));


        public FwoOwnerBase()
        { }

        public FwoOwnerBase(FwoOwnerBase owner)
        {
            Name = owner.Name;
            OwnerResponsibles = owner.OwnerResponsibles.Select(responsible => new OwnerResponsible(responsible)).ToList();
            IsDefault = owner.IsDefault;
            TenantId = owner.TenantId;
            RecertInterval = owner.RecertInterval;
            ExtAppId = owner.ExtAppId;
        }

        public virtual string Display()
        {
            return Name + (!string.IsNullOrEmpty(ExtAppId) ? " (" + ExtAppId + ")" : "");
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeMand(ref shortened);
            OwnerResponsibles = SanitizeResponsibles(OwnerResponsibles ?? [], ref shortened);
            ExtAppId = ExtAppId.SanitizeCommentOpt(ref shortened);
            return shortened;
        }

        public List<string> GetAllOwnerResponsibles()
        {
            HashSet<string> responsibles = new(StringComparer.OrdinalIgnoreCase);
            foreach (OwnerResponsible responsible in OwnerResponsibles ?? [])
                AddResponsible(responsibles, responsible.Dn);
            return responsibles.ToList();
        }

        public List<string> GetOwnerResponsiblesByType(int responsibleType)
        {
            return (OwnerResponsibles ?? [])
                .Where(responsible => responsible.ResponsibleType == responsibleType)
                .Select(responsible => responsible.Dn)
                .Where(dn => !string.IsNullOrWhiteSpace(dn))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void SetOwnerResponsiblesByType(int responsibleType, IEnumerable<string> dns)
        {
            OwnerResponsibles ??= [];
            OwnerResponsibles.RemoveAll(responsible => responsible.ResponsibleType == responsibleType);
            foreach (string dn in dns.Where(dn => !string.IsNullOrWhiteSpace(dn)))
            {
                OwnerResponsibles.Add(new OwnerResponsible { Dn = dn, ResponsibleType = responsibleType });
            }
        }

        public void AddOwnerResponsible(int responsibleType, string dn)
        {
            if (string.IsNullOrWhiteSpace(dn))
            {
                return;
            }
            OwnerResponsibles ??= [];
            if (!OwnerResponsibles.Any(r => r.ResponsibleType == responsibleType && r.Dn.Equals(dn, StringComparison.OrdinalIgnoreCase)))
            {
                OwnerResponsibles.Add(new OwnerResponsible { Dn = dn, ResponsibleType = responsibleType });
            }
        }

        public void RemoveOwnerResponsible(int responsibleType, string dn)
        {
            if (string.IsNullOrWhiteSpace(dn))
            {
                return;
            }
            OwnerResponsibles?.RemoveAll(responsible =>
                responsible.ResponsibleType == responsibleType && responsible.Dn.Equals(dn, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddResponsible(HashSet<string> responsibles, string? dn)
        {
            if (!string.IsNullOrWhiteSpace(dn))
                responsibles.Add(dn);
        }

        private static List<OwnerResponsible> SanitizeResponsibles(List<OwnerResponsible> responsibles, ref bool shortened)
        {
            List<OwnerResponsible> sanitized = [];
            foreach (OwnerResponsible responsible in responsibles)
            {
                if (!string.IsNullOrWhiteSpace(responsible.Dn))
                {
                    sanitized.Add(new OwnerResponsible
                    {
                        Dn = responsible.Dn.SanitizeLdapPathMand(ref shortened),
                        ResponsibleType = responsible.ResponsibleType
                    });
                }
            }
            return sanitized;
        }
    }

    public class OwnerResponsible
    {
        [JsonProperty("dn"), JsonPropertyName("dn")]
        public string Dn { get; set; } = "";

        [JsonProperty("responsible_type"), JsonPropertyName("responsible_type")]
        public int ResponsibleType { get; set; }

        public OwnerResponsible()
        { }

        public OwnerResponsible(OwnerResponsible responsible)
        {
            Dn = responsible.Dn;
            ResponsibleType = responsible.ResponsibleType;
        }
    }
}

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class TenantFilter
    {
        [JsonProperty("is_active"), JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = false;

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int TenantId { get; set; }

        public TenantFilter()
        {}

        public TenantFilter(TenantFilter tenantFilter)
        {
            IsActive = tenantFilter.IsActive;
            TenantId = tenantFilter.TenantId;
        }

        public TenantFilter(Tenant? tenant)
        {
            IsActive = tenant?.Id > 1;
            TenantId = tenant?.Id ?? 0;
        }
    }
}
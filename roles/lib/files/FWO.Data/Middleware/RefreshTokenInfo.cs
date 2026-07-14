using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    public class RefreshTokenInfo
    {
        [JsonPropertyName("user_id"), JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("expires_at"), JsonProperty("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("revoked_at"), JsonProperty("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [JsonPropertyName("created_at"), JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}

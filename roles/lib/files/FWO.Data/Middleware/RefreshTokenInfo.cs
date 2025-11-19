using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    public class RefreshTokenInfo
    {
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }
}

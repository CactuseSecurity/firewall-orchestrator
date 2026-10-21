using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    /// <summary>
    /// Asks the middleware whether the session of the calling user is still live (POST api/AuthenticationToken/CheckSession).
    /// </summary>
    public class SessionCheckParameters
    {
        /// <summary>
        /// Longest accepted refresh token: the length of a refresh token issued by the middleware.
        /// </summary>
        public const int kMaxRefreshTokenLength = 88;

        /// <summary>
        /// Description of <see cref="RefreshToken"/>, used for the API documentation and the validation feedback.
        /// </summary>
        public static readonly string kRefreshTokenDescription = "Required. Refresh token of the session to check, as issued together with the access token of the caller; "
            + $"at most {kMaxRefreshTokenLength} characters.";

        /// <summary>
        /// Refresh token of the session to check, described by <see cref="kRefreshTokenDescription"/>.
        /// </summary>
        [JsonProperty("refreshToken"), JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = "";

        /// <summary>
        /// Options of the check. Defaults to an empty object; null is treated like the empty object.
        /// </summary>
        [JsonProperty("options"), JsonPropertyName("options")]
        public SessionCheckOptions? Options { get; set; } = new();
    }

    /// <summary>
    /// Options of a session check. There are none yet; the object keeps the contract extensible.
    /// </summary>
    public class SessionCheckOptions
    {
    }
}

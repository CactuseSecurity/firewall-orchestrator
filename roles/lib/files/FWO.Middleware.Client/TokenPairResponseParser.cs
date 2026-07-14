using FWO.Data.Middleware;
using FWO.Logging;
using RestSharp;
using System.Text.Json;

namespace FWO.Middleware.Client
{
    /// <summary>
    /// Shared parsing of middleware token-pair REST responses.
    /// </summary>
    public static class TokenPairResponseParser
    {
        /// <summary>
        /// Deserializes a token pair from a middleware REST response.
        /// </summary>
        /// <param name="response">The REST response returned by the middleware.</param>
        /// <param name="logCategory">Log category used when deserialization fails.</param>
        /// <returns>The parsed token pair, or null when the response was unsuccessful, empty, or could not be deserialized.</returns>
        public static TokenPair? Parse(RestResponse<TokenPair> response, string logCategory)
        {
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<TokenPair>(response.Content);
            }
            catch (JsonException exception)
            {
                Log.WriteWarning(logCategory, $"Failed to deserialize token pair: {exception.Message}");
                return null;
            }
        }
    }
}

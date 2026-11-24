using FWO.Basics.Exceptions;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Basics.OAuth
{
    /// <summary>
    /// Requests access tokens with the OAuth 2.0 client credentials grant (RFC 6749, section 4.4). Client id and secret
    /// are sent in the request body (client_secret_post). The token endpoint must use https unless it is a loopback
    /// address. Redirects are not followed, so the client secret only ever goes to the configured URL, and answers are
    /// not decompressed. Failures are thrown as <see cref="OAuthTokenException"/>, whose message never contains the
    /// client secret or a token. Thread-safe; nothing is cached, see <see cref="CachedOAuthTokenProvider"/> for that.
    /// </summary>
    public sealed class OAuthTokenClient : IDisposable
    {
        /// <summary>Time a token request may take, reading the answer included.</summary>
        public static readonly TimeSpan kRequestTimeout = TimeSpan.FromSeconds(30);

        /// <summary>Largest answer of the token endpoint that is read; a larger answer is refused.</summary>
        public const int kMaxResponseBytes = 64 * 1024;

        /// <summary>Texts of the token endpoint (error code, error description, token type) are shortened to this length.</summary>
        public const int kMaxServerTextLength = 500;

        /// <summary>A lifetime above this is cut to it, so that a token is renewed at least once a day.</summary>
        public const long kMaxLifetimeSeconds = 24 * 60 * 60;

        private const int kReadChunkBytes = 8192;
        private const string kGrantTypeClientCredentials = "client_credentials";
        private const string kBearerTokenType = "Bearer";
        private const string kJsonMediaType = "application/json";
        private static readonly TimeSpan kPooledConnectionLifetime = TimeSpan.FromMinutes(5);

        private readonly HttpClient httpClient;
        private readonly TimeProvider timeProvider;
        private readonly TimeSpan requestTimeout;

        /// <summary>
        /// Creates a client with its own connection handling.
        /// </summary>
        /// <param name="checkCertificates">False accepts any server certificate of the token endpoint</param>
        /// <param name="timeProvider">Clock the expiry of a token is computed with; the system clock if null</param>
        /// <param name="requestTimeout">Time limit for a token request; uses the default if null.</param>
        public OAuthTokenClient(bool checkCertificates = true, TimeProvider? timeProvider = null, TimeSpan? requestTimeout = null)
            : this(CreateHandler(checkCertificates), timeProvider, requestTimeout)
        { }

        /// <summary>
        /// Creates a client that sends its requests through the given handler, e.g. a stub in unit tests. The handler
        /// decides about certificates and redirects, and is disposed with the client.
        /// </summary>
        /// <param name="handler">Sends the token requests</param>
        /// <param name="timeProvider">Clock the expiry of a token is computed with; the system clock if null</param>
        /// <param name="requestTimeout">Time limit for a token request; uses the default if null.</param>
        public OAuthTokenClient(HttpMessageHandler handler, TimeProvider? timeProvider = null, TimeSpan? requestTimeout = null)
        {
            ArgumentNullException.ThrowIfNull(handler);
            // the timeout is applied per request, reading the answer included, see RequestClientCredentialsTokenAsync
            httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
            this.timeProvider = timeProvider ?? TimeProvider.System;
            this.requestTimeout = requestTimeout ?? kRequestTimeout;
        }

        /// <summary>
        /// Requests an access token with the client credentials grant.
        /// </summary>
        /// <param name="credentials">Token endpoint, client id and secret, and the scope if any</param>
        /// <param name="cancellationToken">Cancels the request</param>
        /// <returns>The access token and when it expires.</returns>
        /// <exception cref="ArgumentException">The token URL uses plain http to a remote host or another scheme.</exception>
        /// <exception cref="OAuthTokenException">The token endpoint could not be reached or gave no access token.</exception>
        /// <exception cref="OperationCanceledException">The request was cancelled.</exception>
        public async Task<OAuthAccessToken> RequestClientCredentialsTokenAsync(OAuthClientCredentials credentials, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            RequireSecureTokenUrl(credentials.TokenUrl);
            string host = credentials.TokenUrl.Host;
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(requestTimeout);
            DateTimeOffset requestedAt = timeProvider.GetUtcNow();
            try
            {
                using HttpRequestMessage request = CreateRequest(credentials);
                using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
                string content = await ReadLimitedContentAsync(response.Content, host, timeoutSource.Token);
                return ParseAnswer(response.IsSuccessStatusCode, (int)response.StatusCode, content, host, requestedAt);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new OAuthTokenException(OAuthTokenFailure.Unreachable,
                    $"The token endpoint {host} did not answer within {requestTimeout.TotalSeconds} seconds.", innerException: exception);
            }
            catch (HttpRequestException exception)
            {
                throw ToTransportFailure(exception, host);
            }
            catch (IOException exception)
            {
                throw new OAuthTokenException(OAuthTokenFailure.Unreachable, $"The answer of the token endpoint {host} could not be read.",
                    innerException: exception);
            }
        }

        /// <summary>
        /// Disposes the HTTP client and its handler.
        /// </summary>
        public void Dispose()
        {
            httpClient.Dispose();
        }

        /// <summary>
        /// Requires https, or plain http to a loopback address, as the client secret is sent to the token endpoint.
        /// The URL is not part of the message, as it may contain user information.
        /// </summary>
        /// <exception cref="ArgumentException">The token URL is not secure.</exception>
        private static void RequireSecureTokenUrl(Uri tokenUrl)
        {
            bool isHttps = tokenUrl.Scheme == Uri.UriSchemeHttps;
            bool isLoopbackHttp = tokenUrl.Scheme == Uri.UriSchemeHttp && tokenUrl.IsLoopback;
            if (!isHttps && !isLoopbackHttp)
            {
                throw new ArgumentException("The token URL must use https unless it is a loopback address, as the client secret is sent to it.",
                    nameof(tokenUrl));
            }
        }

        /// <summary>
        /// Creates the handler of a client with its own connection handling: no redirects, no cookies, connections
        /// renewed regularly so that DNS changes are picked up.
        /// </summary>
        private static SocketsHttpHandler CreateHandler(bool checkCertificates)
        {
            SocketsHttpHandler handler = new()
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                PooledConnectionLifetime = kPooledConnectionLifetime
            };
            if (!checkCertificates)
            {
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
            }
            return handler;
        }

        /// <summary>
        /// Creates the token request: a form post with grant type, client id and secret, and the scope if any.
        /// </summary>
        private static HttpRequestMessage CreateRequest(OAuthClientCredentials credentials)
        {
            List<KeyValuePair<string, string>> parameters =
            [
                new("grant_type", kGrantTypeClientCredentials),
                new("client_id", credentials.ClientId),
                new("client_secret", credentials.ClientSecret)
            ];
            if (credentials.Scope != null)
            {
                parameters.Add(new("scope", credentials.Scope));
            }
            HttpRequestMessage request = new(HttpMethod.Post, credentials.TokenUrl) { Content = new FormUrlEncodedContent(parameters) };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(kJsonMediaType));
            return request;
        }

        /// <summary>
        /// Reads the answer as UTF-8 text, at most <see cref="kMaxResponseBytes"/> bytes.
        /// </summary>
        /// <exception cref="OAuthTokenException">The answer is larger.</exception>
        private static async Task<string> ReadLimitedContentAsync(HttpContent content, string host, CancellationToken cancellationToken)
        {
            if (content.Headers.ContentLength > kMaxResponseBytes)
            {
                throw TooLargeAnswer(host);
            }
            await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
            using MemoryStream answer = new();
            byte[] chunk = new byte[kReadChunkBytes];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (answer.Length + read > kMaxResponseBytes)
                {
                    throw TooLargeAnswer(host);
                }
                answer.Write(chunk, 0, read);
            }
            return Encoding.UTF8.GetString(answer.GetBuffer(), 0, (int)answer.Length);
        }

        /// <summary>
        /// Returns the access token of an answer.
        /// </summary>
        /// <exception cref="OAuthTokenException">The answer is an error, or has no access token or one of another type.</exception>
        private static OAuthAccessToken ParseAnswer(bool isSuccess, int statusCode, string content, string host, DateTimeOffset requestedAt)
        {
            JsonObject? answer = ParseJsonObject(content);
            if (!isSuccess)
            {
                string? error = Shorten(ReadString(answer, "error"));
                string errorSuffix = error == null ? "" : $" ({error})";
                throw new OAuthTokenException(OAuthTokenFailure.Rejected,
                    $"The token endpoint {host} refused the token request with HTTP {statusCode}{errorSuffix}.",
                    statusCode, error, Shorten(ReadString(answer, "error_description")));
            }
            string? accessToken = ReadString(answer, "access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new OAuthTokenException(OAuthTokenFailure.InvalidResponse, $"The token endpoint {host} answered without an access token.", statusCode);
            }
            string? tokenType = ReadString(answer, "token_type");
            if (tokenType != null && !tokenType.Equals(kBearerTokenType, StringComparison.OrdinalIgnoreCase))
            {
                throw new OAuthTokenException(OAuthTokenFailure.InvalidResponse,
                    $"The token endpoint {host} issued a token of type {Shorten(tokenType)}, only bearer tokens are supported.", statusCode);
            }
            return new OAuthAccessToken(accessToken, ReadExpiry(answer, requestedAt));
        }

        /// <summary>
        /// Returns when the token expires, from expires_in (seconds, as number or text); null if missing or not positive.
        /// </summary>
        private static DateTimeOffset? ReadExpiry(JsonObject? answer, DateTimeOffset requestedAt)
        {
            if (answer?["expires_in"] is not JsonValue value)
            {
                return null;
            }
            bool isNumber = value.TryGetValue(out long seconds);
            if (!isNumber && value.TryGetValue(out string? text))
            {
                isNumber = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seconds);
            }
            if (!isNumber || seconds <= 0)
            {
                return null;
            }
            return requestedAt.AddSeconds(Math.Min(seconds, kMaxLifetimeSeconds));
        }

        /// <summary>
        /// Parses the answer as JSON object; null for an empty or invalid answer.
        /// </summary>
        private static JsonObject? ParseJsonObject(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            try
            {
                return JsonNode.Parse(content) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a text field of the answer; null if missing or not a text.
        /// </summary>
        private static string? ReadString(JsonObject? answer, string name)
        {
            return answer?[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
        }

        /// <summary>
        /// Shortens a text of the token endpoint to <see cref="kMaxServerTextLength"/> characters.
        /// </summary>
        private static string? Shorten(string? text)
        {
            return text != null && text.Length > kMaxServerTextLength ? text[..kMaxServerTextLength] : text;
        }

        /// <summary>
        /// Returns the failure for a request that got no answer: a refused certificate or a connection failure.
        /// </summary>
        private static OAuthTokenException ToTransportFailure(HttpRequestException exception, string host)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is AuthenticationException)
                {
                    return new OAuthTokenException(OAuthTokenFailure.CertificateRejected,
                        $"The server certificate of the token endpoint {host} was refused.", innerException: exception);
                }
            }
            return new OAuthTokenException(OAuthTokenFailure.Unreachable, $"The token endpoint {host} could not be reached.", innerException: exception);
        }

        /// <summary>
        /// Returns the failure for an answer larger than <see cref="kMaxResponseBytes"/>.
        /// </summary>
        private static OAuthTokenException TooLargeAnswer(string host)
        {
            return new OAuthTokenException(OAuthTokenFailure.InvalidResponse,
                $"The answer of the token endpoint {host} is larger than {kMaxResponseBytes} bytes.");
        }
    }
}

using FWO.Logging;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Serializers;
using RestSharp.Serializers.NewtonsoftJson;
using System.Net.Security;

namespace FWO.Api.Client
{
    public abstract class RestApiClient
    {
        private const string kTlsLogCategory = "REST TLS";
        private static readonly HashSet<string> uncheckedEndpointsWarned = [];
        private static readonly object uncheckedEndpointLock = new();

        protected RestClient restClient;
        readonly string BaseUrl;
        readonly TimeSpan? ResponseTimeout;
        readonly bool CheckCertificates;

        /// <summary>
        /// Creates a REST client for the given base url.
        /// </summary>
        /// <remarks>
        /// Certificate checking defaults to on, so a new client is safe unless it opts out.
        /// Clients talking to third party systems (CheckPoint, FortiManager, SecureChange)
        /// take the decision from the configured connection, because some of those appliances
        /// present a self-signed certificate that no FWO host has a reason to trust.
        /// </remarks>
        /// <param name="baseUrl">Base url of the REST api.</param>
        /// <param name="timeout">Response timeout in seconds, null for the RestSharp default.</param>
        /// <param name="checkCertificates">False accepts any server certificate.</param>
        protected RestApiClient(string baseUrl, double? timeout = null, bool checkCertificates = true)
        {
            BaseUrl = baseUrl;
            ResponseTimeout = timeout != null ? TimeSpan.FromSeconds((double)timeout) : null;
            CheckCertificates = checkCertificates;
            if (!checkCertificates)
            {
                WarnUncheckedEndpoint(baseUrl);
            }
            restClient = CreateRestClient(authenticator: null);
        }

        /// <summary>
        /// Says in the log that an endpoint is reached without certificate checking.
        /// </summary>
        /// <remarks>
        /// Everything keeps working without the check, so an installation left in that state
        /// has to be able to say so. Warned once per endpoint per process, because clients
        /// are created per request or discovery run and would otherwise fill the log.
        /// </remarks>
        /// <param name="baseUrl">Base url of the REST api.</param>
        /// <returns>True when the warning was written, false when it was already written before.</returns>
        internal static bool WarnUncheckedEndpoint(string baseUrl)
        {
            lock (uncheckedEndpointLock)
            {
                if (!uncheckedEndpointsWarned.Add(baseUrl))
                {
                    return false;
                }
            }
            Log.WriteWarning(kTlsLogCategory,
                $"Certificate checking is switched off for {baseUrl}: any server certificate is accepted, so credentials " +
                "sent to this endpoint can be intercepted. Add the issuing CA to the host trust store and switch " +
                "certificate checking on for this connection.");
            return true;
        }

        public void SetAuthenticationToken(string jwt)
        {
            restClient = CreateRestClient(new JwtAuthenticator(jwt));
        }

        private RestClient CreateRestClient(IAuthenticator? authenticator)
        {
            RestClientOptions restClientOptions = new() { Timeout = ResponseTimeout };
            // Assigned, not combined: a multicast validation callback only ever returns the
            // result of its last delegate, which silently discards a rejection made earlier.
            restClientOptions.RemoteCertificateValidationCallback = (requestMessage, cert, chain, sslErrors) =>
            {
                return !CheckCertificates || sslErrors == SslPolicyErrors.None;
            };
            restClientOptions.BaseUrl = new Uri(BaseUrl);
            restClientOptions.Authenticator = authenticator;
            return new RestClient(restClientOptions, null, ConfigureRestClientSerialization);
        }

        protected static void ConfigureRestClientSerialization(SerializerConfig config)
        {
            JsonNetSerializer serializer = new(); // Case insensivitive is enabled by default
            config.UseSerializer(() => serializer);
        }
    }
}

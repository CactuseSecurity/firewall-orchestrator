using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Serializers;
using RestSharp.Serializers.NewtonsoftJson;
using System.Net.Security;

namespace FWO.Api.Client
{
    public abstract class RestApiClient
    {
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
        /// pass false explicitly, because those appliances commonly present a self-signed
        /// certificate that no FWO host has a reason to trust.
        /// </remarks>
        /// <param name="baseUrl">Base url of the REST api.</param>
        /// <param name="timeout">Response timeout in seconds, null for the RestSharp default.</param>
        /// <param name="checkCertificates">False accepts any server certificate.</param>
        protected RestApiClient(string baseUrl, double? timeout = null, bool checkCertificates = true)
        {
            BaseUrl = baseUrl;
            ResponseTimeout = timeout != null ? TimeSpan.FromSeconds((double)timeout) : null;
            CheckCertificates = checkCertificates;
            restClient = CreateRestClient(authenticator: null);
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

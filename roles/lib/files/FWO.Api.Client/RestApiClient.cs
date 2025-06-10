using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Serializers;
using RestSharp.Serializers.NewtonsoftJson;

namespace FWO.Api.Client
{
	public abstract class RestApiClient
	{
		protected RestClient restClient;
		readonly string BaseUrl;
		readonly TimeSpan? ResponseTimeout;

		protected RestApiClient(string baseUrl, double? timeout = null)
		{
			BaseUrl = baseUrl;
			ResponseTimeout = timeout != null ? TimeSpan.FromSeconds((double)timeout) : null;
			restClient = CreateRestClient(authenticator: null);
		}

        public void SetAuthenticationToken(string jwt)
        {
            restClient = CreateRestClient(new JwtAuthenticator(jwt));
        }

		private RestClient CreateRestClient(IAuthenticator? authenticator)
        {
            RestClientOptions restClientOptions = new() { Timeout = ResponseTimeout };
            restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
            restClientOptions.BaseUrl = new Uri(BaseUrl);
            restClientOptions.Authenticator = authenticator;
            return new RestClient(restClientOptions, null, ConfigureRestClientSerialization);
        }

		private static void ConfigureRestClientSerialization(SerializerConfig config)
		{
			JsonNetSerializer serializer = new(); // Case insensivitive is enabled by default
			config.UseSerializer(() => serializer);
		}
	}
}

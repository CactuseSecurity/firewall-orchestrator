using FWO.Middleware.Client;
using RestSharp;

namespace FWO.Test.Mocks
{
    public class TestMiddlewareClient : MiddlewareClient
    {
        private readonly string middlewareApiBaseUrl;

        public TestMiddlewareClient(string middlewareServerUri = "https://middleware.example/")
            : base(middlewareServerUri)
        {
            middlewareApiBaseUrl = new Uri(new Uri(middlewareServerUri.EndsWith('/') ? middlewareServerUri : middlewareServerUri + "/"), "api/").ToString();
            UseHandler(new ThrowingHttpMessageHandler());
        }

        public void UseHandler(HttpMessageHandler handler)
        {
            restClient.Dispose();
            restClient = new RestClient(handler, false, options => options.BaseUrl = new Uri(middlewareApiBaseUrl), ConfigureRestClientSerialization);
        }
    }
}

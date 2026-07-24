using FWO.Middleware.Client;
using RestSharp;

namespace FWO.Test.Mocks
{
    public class TestMiddlewareClient : MiddlewareClient
    {
        private const string kBaseUrl = "https://middleware.example/api/";

        public TestMiddlewareClient(string middlewareServerUri = "https://middleware.example/")
            : base(middlewareServerUri)
        {
        }

        public void UseHandler(HttpMessageHandler handler)
        {
            restClient.Dispose();
            restClient = new RestClient(handler, false, options => options.BaseUrl = new Uri(kBaseUrl));
        }
    }
}

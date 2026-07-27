using System.Net;
using System.Net.Http;
using System.Text;

namespace FWO.Test.Mocks
{
    internal sealed class SingleResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string body;

        public SingleResponseHandler(HttpResponseMessage response)
        {
            statusCode = response.StatusCode;
            body = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    internal static class HttpResponseMessageFactory
    {
        public static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
        }
    }
}

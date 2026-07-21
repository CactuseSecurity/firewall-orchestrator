using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.ExternalSystems.CheckPoint;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class CheckPointClientTest
    {
        private static readonly string[] kSessionHeaderValues = ["session-1"];
        private static readonly string[] kSessionRequestPaths = ["login", "discard", "logout"];

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> responses;

            public List<HttpRequestMessage> Requests { get; } = [];

            public RecordingHandler(params HttpResponseMessage[] responses)
            {
                this.responses = new(responses);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(responses.Count > 0
                    ? responses.Dequeue()
                    : new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private sealed class TestableCheckPointClient : CheckPointClient
        {
            public TestableCheckPointClient(ExternalTicketSystem ticketSystem, Management management)
                : base(ticketSystem, management)
            { }

            public void UseHandler(HttpMessageHandler handler)
            {
                restClient.Dispose();
                restClient = new RestClient(handler, false, options => options.BaseUrl = new Uri("https://checkpoint.example/web_api/"));
            }
        }

        [TestCase("checkpoint.example", 0, "https://checkpoint.example:443/web_api/")]
        [TestCase("checkpoint.example", 8443, "https://checkpoint.example:8443/web_api/")]
        [TestCase("http://checkpoint.example:9443/path", 0, "https://checkpoint.example:9443/web_api/")]
        public void Constructor_UsesManagementHostnameForBaseUrl(string hostname, int port, string expectedBaseUrl)
        {
            TestableCheckPointClient client = new(CreateTicketSystem(), CreateManagement(hostname, port));

            Uri baseUrl = client.GetType().BaseType!
                .GetField("restClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(client) is RestClient restClient
                    ? restClient.Options.BaseUrl!
                    : throw new AssertionException("Rest client missing.");

            Uri expected = new(expectedBaseUrl);
            Assert.Multiple(() =>
            {
                Assert.That(baseUrl.Host, Is.EqualTo(expected.Host));
                Assert.That(baseUrl.Port, Is.EqualTo(expected.Port));
                Assert.That(baseUrl.AbsolutePath, Is.EqualTo(expected.AbsolutePath));
            });
        }

        [Test]
        public void LoginIfNeeded_ThrowsWhenCredentialsAreMissing()
        {
            TestableCheckPointClient client = new(CreateTicketSystem(), new Management { Hostname = "checkpoint.example" });

            ProcessingFailedException exception = Assert.ThrowsAsync<ProcessingFailedException>(client.LoginIfNeeded)!;

            Assert.That(exception.Message, Does.Contain("credentials missing"));
        }

        [Test]
        public async Task RestCall_LogsInOnceAndAddsTheSessionHeader()
        {
            RecordingHandler handler = new(
                JsonResponse(HttpStatusCode.OK, "{\"sid\":\"session-1\"}"),
                JsonResponse(HttpStatusCode.Created, "{\"uid\":\"object-1\"}"));
            TestableCheckPointClient client = CreateClient(handler);
            RestRequest request = new RestRequest("add-host", Method.Post).AddStringBody("{}", ContentType.Json);

            RestResponse<int> response = await client.RestCall(request, "add-host");
            await client.LoginIfNeeded();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                Assert.That(client.CurrentSessionId, Is.EqualTo("session-1"));
                Assert.That(handler.Requests, Has.Count.EqualTo(2));
                Assert.That(handler.Requests[0].RequestUri!.AbsolutePath, Is.EqualTo("/web_api/login"));
                Assert.That(handler.Requests[1].Headers.GetValues("X-chkp-sid"), Is.EqualTo(kSessionHeaderValues));
                Assert.That(handler.Requests[1].Headers.Accept.Single().MediaType, Is.EqualTo("application/json"));
            });
        }

        [Test]
        public void LoginIfNeeded_ThrowsWhenTheLoginResponseHasNoSession()
        {
            RecordingHandler handler = new(JsonResponse(HttpStatusCode.OK, "{}"));
            TestableCheckPointClient client = CreateClient(handler);

            ProcessingFailedException exception = Assert.ThrowsAsync<ProcessingFailedException>(client.LoginIfNeeded)!;

            Assert.That(exception.Message, Does.Contain("login failed"));
        }

        [Test]
        public async Task DiscardAndLogout_UseTheSessionAndLogoutAlwaysClearsIt()
        {
            RecordingHandler handler = new(
                JsonResponse(HttpStatusCode.OK, "{\"sid\":\"session-2\"}"),
                JsonResponse(HttpStatusCode.InternalServerError, "discard failed"),
                JsonResponse(HttpStatusCode.InternalServerError, "logout failed"));
            TestableCheckPointClient client = CreateClient(handler);

            await client.LoginIfNeeded();
            await client.Discard();
            await client.Logout();
            await client.Discard();

            Assert.Multiple(() =>
            {
                Assert.That(client.CurrentSessionId, Is.Null);
                Assert.That(handler.Requests.Select(request => request.RequestUri!.Segments[^1]), Is.EqualTo(kSessionRequestPaths));
                Assert.That(handler.Requests.Skip(1).All(request => request.Headers.GetValues("X-chkp-sid").Single() == "session-2"), Is.True);
            });
        }

        private static TestableCheckPointClient CreateClient(RecordingHandler handler)
        {
            TestableCheckPointClient client = new(CreateTicketSystem(), CreateManagement("checkpoint.example", 443));
            client.UseHandler(handler);
            return client;
        }

        private static ExternalTicketSystem CreateTicketSystem()
        {
            return new ExternalTicketSystem { Url = "https://fallback.example/web_api/", ResponseTimeout = 5 };
        }

        private static Management CreateManagement(string hostname, int port)
        {
            return new Management
            {
                Hostname = hostname,
                Port = port,
                ExportCredential = new ImportCredential("api-user", "unencrypted-secret")
            };
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}

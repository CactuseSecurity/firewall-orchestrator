using FWO.Basics.Exceptions;
using FWO.Basics.OAuth;
using NUnit.Framework;
using System.Net;
using System.Security.Authentication;
using System.Text;

namespace FWO.Test
{
    /// <summary>
    /// Tests the OAuth 2.0 client credentials token client of FWO.Basics against a simulated token endpoint: the request,
    /// the reading of the answer, and that failures are classified and never reveal the client secret or a token.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class OAuthTokenClientTest
    {
        private const string kTokenUrl = "https://sso.example.test/realms/fwo/protocol/openid-connect/token";
        private const string kClientId = "fwo-ai";
        private const string kClientSecret = "very-secret-value";
        private const string kAccessToken = "access-token-1";
        private const string kScope = "https://management.azure.com/.default";
        private const string kEncodedScope = "scope=https%3A%2F%2Fmanagement.azure.com%2F.default";

        private static readonly DateTimeOffset kNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        private static OAuthClientCredentials Credentials(string tokenUrl = kTokenUrl, string? scope = null)
        {
            return new OAuthClientCredentials(new Uri(tokenUrl), kClientId, kClientSecret, scope);
        }

        private static async Task<OAuthAccessToken> Request(OAuthTestEndpoint endpoint, OAuthClientCredentials? credentials = null)
        {
            using OAuthTokenClient client = new(endpoint, new OAuthTestClock(kNow));
            return await client.RequestClientCredentialsTokenAsync(credentials ?? Credentials(), CancellationToken.None);
        }

        private static OAuthTokenException? RequestFails(OAuthTestEndpoint endpoint)
        {
            return Assert.ThrowsAsync<OAuthTokenException>(() => Request(endpoint));
        }

        [Test]
        public async Task Request_PostsClientCredentialsInTheBody()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kAccessToken);

            OAuthAccessToken token = await Request(endpoint);

            OAuthTestEndpoint.RecordedRequest request = endpoint.Requests[0];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(token.Value, Is.EqualTo(kAccessToken));
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.Url, Is.EqualTo(kTokenUrl));
                Assert.That(request.Body, Is.EqualTo($"grant_type=client_credentials&client_id={kClientId}&client_secret={kClientSecret}"));
                Assert.That(request.Accept, Is.EqualTo("application/json"));
                Assert.That(request.Authorization, Is.Null);
            }
        }

        [Test]
        public async Task Request_SendsTheScopeIfGiven()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kAccessToken);

            await Request(endpoint, Credentials(scope: kScope));

            Assert.That(endpoint.Requests[0].Body, Does.EndWith("&" + kEncodedScope));
        }

        [TestCase("3600", 3600)]
        [TestCase("\"300\"", 300)]
        [TestCase("172800", OAuthTokenClient.kMaxLifetimeSeconds)]
        public async Task Request_ReadsTheExpiry(string expiresIn, long expectedSeconds)
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.OK, $"{{\"access_token\":\"{kAccessToken}\",\"expires_in\":{expiresIn}}}");

            OAuthAccessToken token = await Request(endpoint);

            Assert.That(token.ExpiresAt, Is.EqualTo(kNow.AddSeconds(expectedSeconds)));
        }

        [TestCase("")]
        [TestCase(",\"expires_in\":0")]
        [TestCase(",\"expires_in\":-5")]
        [TestCase(",\"expires_in\":\"soon\"")]
        [TestCase(",\"expires_in\":true")]
        public async Task Request_WithoutUsableExpiry_HasNoExpiry(string expiresIn)
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.OK, $"{{\"access_token\":\"{kAccessToken}\"{expiresIn}}}");

            OAuthAccessToken token = await Request(endpoint);

            Assert.That(token.ExpiresAt, Is.Null);
        }

        [Test]
        public async Task Request_AcceptsBearerInAnyCase()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.OK, $"{{\"access_token\":\"{kAccessToken}\",\"token_type\":\"bearer\"}}");

            OAuthAccessToken token = await Request(endpoint);

            Assert.That(token.Value, Is.EqualTo(kAccessToken));
        }

        [Test]
        public void Request_RefusedCredentials_AreRejectedWithTheErrorOfTheTokenEndpoint()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\",\"error_description\":\"Invalid client or Invalid client credentials\"}");

            OAuthTokenException? exception = RequestFails(endpoint);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.Rejected));
                Assert.That(exception.StatusCode, Is.EqualTo((int)HttpStatusCode.Unauthorized));
                Assert.That(exception.Error, Is.EqualTo("invalid_client"));
                Assert.That(exception.ErrorDescription, Is.EqualTo("Invalid client or Invalid client credentials"));
                Assert.That(exception.Message, Does.Contain("sso.example.test").And.Contain("401").And.Contain("invalid_client"));
                Assert.That(exception.Message, Does.Not.Contain(kClientSecret));
            }
        }

        [Test]
        public void Request_ErrorWithoutJson_IsRejectedWithoutErrorCode()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.BadGateway, "<html>bad gateway</html>");

            OAuthTokenException? exception = RequestFails(endpoint);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.Rejected));
                Assert.That(exception.StatusCode, Is.EqualTo((int)HttpStatusCode.BadGateway));
                Assert.That(exception.Error, Is.Null);
                Assert.That(exception.ErrorDescription, Is.Null);
            }
        }

        [Test]
        public void Request_LongErrorTexts_AreShortened()
        {
            OAuthTestEndpoint endpoint = new();
            string longText = new('x', OAuthTokenClient.kMaxServerTextLength * 2);
            endpoint.Answer(HttpStatusCode.BadRequest, $"{{\"error\":\"{longText}\",\"error_description\":\"{longText}\"}}");

            OAuthTokenException? exception = RequestFails(endpoint);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.Error, Has.Length.EqualTo(OAuthTokenClient.kMaxServerTextLength));
                Assert.That(exception.ErrorDescription, Has.Length.EqualTo(OAuthTokenClient.kMaxServerTextLength));
            }
        }

        [TestCase("{\"token_type\":\"Bearer\"}")]
        [TestCase("{\"access_token\":\"\"}")]
        [TestCase("{\"access_token\":42}")]
        [TestCase("[\"access_token\"]")]
        [TestCase("no json")]
        [TestCase("")]
        public void Request_SuccessWithoutToken_IsAnInvalidResponse(string content)
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.OK, content);

            OAuthTokenException? exception = RequestFails(endpoint);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.InvalidResponse));
                Assert.That(exception.StatusCode, Is.EqualTo((int)HttpStatusCode.OK));
            }
        }

        [Test]
        public void Request_TokenOfAnotherType_IsAnInvalidResponseWithoutTheToken()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.OK, $"{{\"access_token\":\"{kAccessToken}\",\"token_type\":\"DPoP\"}}");

            OAuthTokenException? exception = RequestFails(endpoint);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.InvalidResponse));
                Assert.That(exception.Message, Does.Contain("DPoP"));
                Assert.That(exception.Message, Does.Not.Contain(kAccessToken));
            }
        }

        [Test]
        public void Request_AnswerAboveTheLimit_IsAnInvalidResponse()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.OK, new string(' ', OAuthTokenClient.kMaxResponseBytes + 1));

            OAuthTokenException? exception = RequestFails(endpoint);

            Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.InvalidResponse));
        }

        [Test]
        public void Request_AnswerAboveTheLimitWithoutLength_IsAnInvalidResponse()
        {
            OAuthTestEndpoint endpoint = new();
            byte[] content = Encoding.UTF8.GetBytes(new string(' ', OAuthTokenClient.kMaxResponseBytes + 1));
            endpoint.Answers.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UnknownLengthStream(content)) });

            OAuthTokenException? exception = RequestFails(endpoint);

            Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.InvalidResponse));
        }

        [Test]
        public void Request_ConnectionFailure_IsUnreachable()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Fail(new HttpRequestException("No such host is known."));

            OAuthTokenException? exception = RequestFails(endpoint);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.Unreachable));
                Assert.That(exception.StatusCode, Is.Null);
                Assert.That(exception.InnerException, Is.TypeOf<HttpRequestException>());
            }
        }

        [Test]
        public void Request_RefusedCertificate_IsReported()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Fail(new HttpRequestException("The SSL connection could not be established.", new AuthenticationException("remote certificate is invalid")));

            OAuthTokenException? exception = RequestFails(endpoint);

            Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.CertificateRejected));
        }

        [Test]
        public void Request_Timeout_IsUnreachable()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Fail(new TaskCanceledException("timed out"));

            OAuthTokenException? exception = RequestFails(endpoint);

            Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.Unreachable));
        }

        /// <summary>
        /// Verifies that the configured timeout ends a token request that is still waiting for an answer.
        /// </summary>
        [Test]
        public void Request_ConfiguredTimeout_CancelsPendingResponse()
        {
            TaskCompletionSource responseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            OAuthTestEndpoint endpoint = new() { Gate = responseGate.Task };
            TimeSpan requestTimeout = TimeSpan.FromMilliseconds(50);
            using OAuthTokenClient client = new(endpoint, requestTimeout: requestTimeout);

            OAuthTokenException? exception = Assert.ThrowsAsync<OAuthTokenException>(async () =>
                await client.RequestClientCredentialsTokenAsync(Credentials(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.Unreachable));
        }

        [Test]
        public void Request_FailureWhileReading_IsUnreachable()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answers.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingReadContent() });

            OAuthTokenException? exception = RequestFails(endpoint);

            Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.Unreachable));
        }

        [Test]
        public void Request_CancelledByTheCaller_IsNotAFailureOfTheTokenEndpoint()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kAccessToken);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            using OAuthTokenClient client = new(endpoint);

            Assert.CatchAsync<OperationCanceledException>(() => client.RequestClientCredentialsTokenAsync(Credentials(), cancellation.Token));
        }

        [TestCase("http://sso.example.test/token")]
        [TestCase("ftp://sso.example.test/token")]
        public void Request_InsecureTokenUrl_IsRefusedWithoutSendingTheSecret(string tokenUrl)
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kAccessToken);

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(() => Request(endpoint, Credentials(tokenUrl)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(endpoint.Requests, Is.Empty);
                Assert.That(exception!.Message, Does.Not.Contain("sso.example.test"));
            }
        }

        [Test]
        public async Task Request_PlainHttpToLoopback_IsAllowed()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kAccessToken);

            OAuthAccessToken token = await Request(endpoint, Credentials("http://localhost:8080/realms/fwo/protocol/openid-connect/token"));

            Assert.That(token.Value, Is.EqualTo(kAccessToken));
        }

        [Test]
        public void Dispose_DisposesTheHandler()
        {
            OAuthTestEndpoint endpoint = new();
            OAuthTokenClient client = new(endpoint);

            client.Dispose();

            Assert.That(endpoint.IsDisposed, Is.True);
        }

        [Test]
        public void Constructor_WithOwnConnectionHandling_CanBeCreatedAndDisposed()
        {
            Assert.That(() => new OAuthTokenClient().Dispose(), Throws.Nothing);
        }

        [Test]
        public void Constructor_WithoutHandler_IsRefused()
        {
            Assert.That(() => new OAuthTokenClient((HttpMessageHandler)null!), Throws.ArgumentNullException);
        }

        [Test]
        public void Credentials_AreChecked()
        {
            Uri tokenUrl = new(kTokenUrl);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new OAuthClientCredentials(new Uri("/token", UriKind.Relative), kClientId, kClientSecret), Throws.ArgumentException);
                Assert.That(() => new OAuthClientCredentials(tokenUrl, " ", kClientSecret), Throws.ArgumentException);
                Assert.That(() => new OAuthClientCredentials(tokenUrl, kClientId, ""), Throws.ArgumentException);
                Assert.That(() => new OAuthClientCredentials(null!, kClientId, kClientSecret), Throws.ArgumentNullException);
                Assert.That(new OAuthClientCredentials(tokenUrl, kClientId, kClientSecret, " ").Scope, Is.Null);
                Assert.That(new OAuthClientCredentials(tokenUrl, kClientId, kClientSecret).ToString(), Does.Not.Contain(kClientSecret));
            }
        }

        [Test]
        public void AccessToken_MustNotBeEmpty()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new OAuthAccessToken(" ", null), Throws.ArgumentException);
                Assert.That(new OAuthAccessToken(kAccessToken, null).ToString(), Does.Not.Contain(kAccessToken));
            }
        }

        /// <summary>
        /// Stream without a known length, like a chunked answer.
        /// </summary>
        private sealed class UnknownLengthStream(byte[] content) : MemoryStream(content)
        {
            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();
        }
    }
}

using FWO.Api.Client.ExceptionHandling;
using GraphQL.Client.Http;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;

namespace FWO.Test
{
    [TestFixture]
    [Category("ApiReachability")]
    internal class ApiReachabilityTest
    {
        /// <summary>
        /// A request that never got an answer. This is the only case that derives from
        /// HttpRequestException, which is why catching that type alone is not enough.
        /// </summary>
        [Test]
        public void IndicatesUnreachableApi_IsTrueForATransportFailure()
        {
            Assert.That(ApiReachability.IndicatesUnreachableApi(new HttpRequestException("connection reset by peer")), Is.True);
        }

        /// <summary>
        /// The reverse proxy answering on its own behalf because the service behind it is
        /// down or restarting. GraphQLHttpRequestException derives from Exception, not from
        /// HttpRequestException, so this is the case a narrower catch silently misses.
        /// </summary>
        [TestCase(HttpStatusCode.BadGateway)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        [TestCase(HttpStatusCode.GatewayTimeout)]
        public void IndicatesUnreachableApi_IsTrueForAProxyFailure(HttpStatusCode statusCode)
        {
            Assert.That(ApiReachability.IndicatesUnreachableApi(CreateGraphQlHttpException(statusCode)), Is.True);
        }

        /// <summary>
        /// An answer about the request itself, or a failure of the API after it was reached.
        /// Reporting either as "unreachable" would mask a real misconfiguration behind a
        /// retry suggestion.
        /// </summary>
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public void IndicatesUnreachableApi_IsFalseForAnAnswerAboutTheRequest(HttpStatusCode statusCode)
        {
            Assert.That(ApiReachability.IndicatesUnreachableApi(CreateGraphQlHttpException(statusCode)), Is.False);
        }

        /// <summary>
        /// The HttpClient timeout surfaces as a cancellation carrying a TimeoutException.
        /// </summary>
        [Test]
        public void IndicatesUnreachableApi_IsTrueForAClientTimeout()
        {
            TaskCanceledException timedOut = new("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException());

            Assert.That(ApiReachability.IndicatesUnreachableApi(timedOut), Is.True);
        }

        /// <summary>
        /// A cancellation a caller asked for has no TimeoutException inside it and says
        /// nothing about the API, so it must not be reported as an API failure.
        /// </summary>
        [Test]
        public void IndicatesUnreachableApi_IsFalseForARequestedCancellation()
        {
            Assert.That(ApiReachability.IndicatesUnreachableApi(new TaskCanceledException("cancelled by the caller")), Is.False);
        }

        /// <summary>
        /// GraphQL errors and conversion failures arrive as InvalidOperationException from
        /// the API connection. The API answered, so these are not reachability failures.
        /// </summary>
        [Test]
        public void IndicatesUnreachableApi_IsFalseForAnApiSideError()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ApiReachability.IndicatesUnreachableApi(new InvalidOperationException("permission denied")), Is.False);
                Assert.That(ApiReachability.IndicatesUnreachableApi(new Exception("boom")), Is.False);
            });
        }

        /// <summary>
        /// Builds the exception the GraphQL client raises for a non-success status code.
        /// </summary>
        /// <param name="statusCode">Status code the API host answered with.</param>
        /// <returns>The exception a caller would have to classify.</returns>
        private static GraphQLHttpRequestException CreateGraphQlHttpException(HttpStatusCode statusCode)
        {
            using HttpResponseMessage response = new(statusCode);
            HttpResponseHeaders headers = response.Headers;

            return new GraphQLHttpRequestException(statusCode, headers, $"<html><title>{(int)statusCode}</title></html>");
        }
    }
}

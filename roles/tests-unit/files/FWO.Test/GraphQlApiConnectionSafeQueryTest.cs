using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FWO.Api.Client;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Unit tests for non-throwing GraphQL query behavior.
    /// </summary>
    public class GraphQlApiConnectionSafeQueryTest
    {
        /// <summary>
        /// Simple stub handler that returns a fixed response or throws a fixed exception.
        /// </summary>
        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
            private readonly Exception? exception;

            /// <summary>
            /// Create a handler that returns a fixed response.
            /// </summary>
            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                this.responder = responder;
            }

            /// <summary>
            /// Create a handler that throws a fixed exception.
            /// </summary>
            public StubHttpMessageHandler(Exception exception)
            {
                this.exception = exception;
                responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            /// <summary>
            /// Return the stubbed response or throw the stubbed exception.
            /// </summary>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (exception != null)
                {
                    throw exception;
                }

                return Task.FromResult(responder(request));
            }
        }

        /// <summary>
        /// Builds a GraphQlApiConnection instance with an injected GraphQLHttpClient using the provided handler.
        /// </summary>
        private static GraphQlApiConnection CreateConnectionWithHandler(HttpMessageHandler handler)
        {
            GraphQlApiConnection connection = new("http://localhost");
            GraphQLHttpClient client = new(new GraphQLHttpClientOptions
            {
                EndPoint = new Uri("http://localhost/graphql"),
                HttpMessageHandler = handler,
                UseWebSocketForQueriesAndMutations = false
            }, new NewtonsoftJsonSerializer());

            FieldInfo? field = typeof(GraphQlApiConnection).GetField("graphQlClient", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("graphQlClient field not found.");
            }
            field.SetValue(connection, client);

            return connection;
        }

        /// <summary>
        /// Builds a JSON HTTP response.
        /// </summary>
        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        /// <summary>
        /// Errors returned by the GraphQL endpoint are surfaced as ApiResponse errors.
        /// </summary>
        [Test]
        public async Task SendQuerySafeAsync_ReturnsErrors_WhenGraphQlErrorsPresent()
        {
            const string json = "{\"errors\":[{\"message\":\"boom\"}]}";
            using GraphQlApiConnection connection = CreateConnectionWithHandler(new StubHttpMessageHandler(_ => JsonResponse(json)));

            ApiResponse<string> response = await connection.SendQuerySafeAsync<string>("query { test }");

            Assert.That(response.HasErrors, Is.True);
            Assert.That(response.Errors, Is.Not.Null);
            Assert.That(response.Errors!, Does.Contain("boom"));
            Assert.That(response.Result, Is.Null);
        }

        /// <summary>
        /// Exceptions thrown during transport are converted to ApiResponse errors.
        /// </summary>
        [Test]
        public async Task SendQuerySafeAsync_ReturnsError_WhenHttpThrows()
        {
            using GraphQlApiConnection connection = CreateConnectionWithHandler(new StubHttpMessageHandler(new HttpRequestException("network down")));

            ApiResponse<string> response = await connection.SendQuerySafeAsync<string>("query { test }");

            Assert.That(response.HasErrors, Is.True);
            Assert.That(response.Errors, Is.Not.Null);
            Assert.That(response.Errors![0], Does.Contain("network down"));
            Assert.That(response.Result, Is.Null);
        }
    }
}

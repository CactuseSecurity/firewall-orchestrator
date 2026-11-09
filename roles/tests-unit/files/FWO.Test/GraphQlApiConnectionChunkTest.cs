using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FWO.Api.Client;
using FWO.Basics;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class GraphQlApiConnectionChunkTest
    {
        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> responses;
            public List<string?> HasuraRoles { get; } = [];
            public int RequestCount { get; private set; }

            public StubHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
            {
                this.responses = new Queue<HttpResponseMessage>(responses);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestCount++;
                HasuraRoles.Add(request.Headers.TryGetValues("x-hasura-role", out IEnumerable<string>? values)
                    ? values.FirstOrDefault()
                    : null);
                if (responses.Count == 0)
                {
                    throw new InvalidOperationException("No stub response queued.");
                }

                return Task.FromResult(responses.Dequeue());
            }
        }

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

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
        {
            MethodInfo? method = typeof(GraphQlApiConnection).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' not found.");
            }

            try
            {
                return (T)method.Invoke(null, args)!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static Exception InvokePrivateStaticAndCaptureException(string methodName, params object?[] args)
        {
            MethodInfo? method = typeof(GraphQlApiConnection).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' not found.");
            }

            try
            {
                method.Invoke(null, args);
                throw new AssertionException($"Expected method '{methodName}' to throw.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return ex.InnerException;
            }
        }

        [Test]
        public void ExtractChunkItems_ReadsAnonymousObjectList()
        {
            var variables = new { objects = new List<int> { 1, 2, 3 } };

            List<object?> result = InvokePrivateStatic<List<object?>>("ExtractChunkItems", variables, "objects");

            Assert.That(result, Is.EqualTo(new object?[] { 1, 2, 3 }));
        }

        [Test]
        public void ExtractChunkItems_ReadsDictionaryList()
        {
            Dictionary<string, object> variables = new()
            {
                ["objects"] = new List<int> { 4, 5 }
            };

            List<object?> result = InvokePrivateStatic<List<object?>>("ExtractChunkItems", variables, "objects");

            Assert.That(result, Is.EqualTo(new object?[] { 4, 5 }));
        }

        [Test]
        public void ExtractChunkItems_ReadsExpandoObjectList()
        {
            dynamic variables = new ExpandoObject();
            variables.objects = new List<string> { "a", "b" };

            List<object?> result = InvokePrivateStatic<List<object?>>("ExtractChunkItems", (object)variables, "objects");

            Assert.That(result, Is.EqualTo(new object?[] { "a", "b" }));
        }

        [Test]
        public void ExtractChunkItems_ThrowsWhenVariableMissing()
        {
            var variables = new { notObjects = new List<int> { 1 } };

            Exception exception = InvokePrivateStaticAndCaptureException("ExtractChunkItems", variables, "objects");

            Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.Message, Is.EqualTo("Chunk variable 'objects' was not found in variables."));
        }

        [Test]
        public void ExtractChunkItems_ThrowsWhenVariableIsString()
        {
            var variables = new { objects = "abc" };

            Exception exception = InvokePrivateStaticAndCaptureException("ExtractChunkItems", variables, "objects");

            Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.Message, Is.EqualTo("Chunk variable 'objects' must be a non-string enumerable."));
        }

        [Test]
        public void ReplaceChunkVariable_ReplacesDictionaryValue()
        {
            Dictionary<string, object> variables = new()
            {
                ["objects"] = new List<int> { 1, 2, 3 },
                ["removed"] = 42L
            };

            var replaced = InvokePrivateStatic<object>("ReplaceChunkVariable", variables, "objects", new List<object?> { 9, 10 });

            Assert.That(replaced, Is.TypeOf<Dictionary<string, object?>>());
            Dictionary<string, object?> replacedValues = (Dictionary<string, object?>)replaced;
            Assert.That(replacedValues["removed"], Is.EqualTo(42L));
            Assert.That(replacedValues["objects"], Is.EqualTo(new List<object?> { 9, 10 }));
        }

        [Test]
        public void MergeChunkedResponse_MergesAffectedRowsAndReturning()
        {
            JObject mergedResponse =
                JObject.Parse("{\"insert_rule_owner\":{\"affected_rows\":2,\"returning\":[{\"id\":1},{\"id\":2}]}}");
            JObject chunkData =
                JObject.Parse("{\"insert_rule_owner\":{\"affected_rows\":1,\"returning\":[{\"id\":3}]}}");
            QueryChunkingOptions options = new()
            {
                Enabled = true,
                ChunkVariableName = "objects",
                ChunkSize = 2,
                MergeMode = ChunkMergeMode.MutationAffectedRowsAndReturning
            };

            JObject result = InvokePrivateStatic<JObject>("MergeChunkedResponse", mergedResponse, chunkData, options);

            Assert.That(result["insert_rule_owner"]?["affected_rows"]?.Value<long>(), Is.EqualTo(3));
            Assert.That(result["insert_rule_owner"]?["returning"], Is.TypeOf<JArray>());
            Assert.That(((JArray)result["insert_rule_owner"]!["returning"]!).Count, Is.EqualTo(3));
        }

        [Test]
        public void MergeChunkedResponse_MergesAffectedRowsOnly()
        {
            JObject mergedResponse = JObject.Parse("{\"update_rule_owner\":{\"affected_rows\":2}}");
            JObject chunkData = JObject.Parse("{\"update_rule_owner\":{\"affected_rows\":5}}");
            QueryChunkingOptions options = new()
            {
                Enabled = true,
                ChunkVariableName = "objects",
                ChunkSize = 2,
                MergeMode = ChunkMergeMode.MutationAffectedRowsOnly
            };

            JObject result = InvokePrivateStatic<JObject>("MergeChunkedResponse", mergedResponse, chunkData, options);

            Assert.That(result["update_rule_owner"]?["affected_rows"]?.Value<long>(), Is.EqualTo(7));
        }

        [Test]
        public void MergeChunkedResponse_ConcatsTopLevelArrays()
        {
            JObject mergedResponse = JObject.Parse("{\"items\":[1,2]}");
            JObject chunkData = JObject.Parse("{\"items\":[3,4]}");
            QueryChunkingOptions options = new()
            {
                Enabled = true,
                ChunkVariableName = "objects",
                ChunkSize = 2,
                MergeMode = ChunkMergeMode.TopLevelArrayConcat
            };

            JObject result = InvokePrivateStatic<JObject>("MergeChunkedResponse", mergedResponse, chunkData, options);

            Assert.That(result["items"], Is.TypeOf<JArray>());
            Assert.That(result["items"]!.Values<int>().ToList(), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void MergeChunkedResponse_ThrowsForDifferentTopLevelFields()
        {
            JObject mergedResponse = JObject.Parse("{\"items\":[1,2]}");
            JObject chunkData = JObject.Parse("{\"otherItems\":[3]}");
            QueryChunkingOptions options = new()
            {
                Enabled = true,
                ChunkVariableName = "objects",
                ChunkSize = 2,
                MergeMode = ChunkMergeMode.TopLevelArrayConcat
            };

            Exception exception = InvokePrivateStaticAndCaptureException("MergeChunkedResponse", mergedResponse, chunkData, options);

            Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.Message, Does.Contain("different top-level fields"));
        }

        [Test]
        public async Task SendQueryAsync_ConcatsTopLevelArrays_WhenChunkingEnabled()
        {
            StubHttpMessageHandler handler =
            new([
                JsonResponse("{\"data\":{\"items\":[1,2]}}"),
                JsonResponse("{\"data\":{\"items\":[3]}}")
            ]);

            using GraphQlApiConnection connection = CreateConnectionWithHandler(handler);
            Dictionary<string, object> variables = new()
            {
                ["objects"] = new List<int> { 10, 20, 30 }
            };

            List<int> result = await connection.SendQueryAsync<List<int>>(
                "query FetchItems($objects: [Int!]) { items }",
                variables,
                chunkingOptions: new QueryChunkingOptions
                {
                    Enabled = true,
                    ChunkVariableName = "objects",
                    ChunkSize = 2,
                    MergeMode = ChunkMergeMode.TopLevelArrayConcat
                });

            Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(handler.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SendQueryAsync_SendsSelectedRoleHeader_WhenChunkingEnabled()
        {
            StubHttpMessageHandler handler =
            new([
                JsonResponse("{\"data\":{\"items\":[1,2]}}"),
                JsonResponse("{\"data\":{\"items\":[3]}}")
            ]);

            using GraphQlApiConnection connection = CreateConnectionWithHandler(handler);
            connection.SetRole(Roles.Modeller);
            Dictionary<string, object> variables = new()
            {
                ["objects"] = new List<int> { 10, 20, 30 }
            };

            await connection.SendQueryAsync<List<int>>(
                "query FetchItems($objects: [Int!]) { items }",
                variables,
                chunkingOptions: new QueryChunkingOptions
                {
                    Enabled = true,
                    ChunkVariableName = "objects",
                    ChunkSize = 2,
                    MergeMode = ChunkMergeMode.TopLevelArrayConcat
                });

            Assert.That(handler.RequestCount, Is.EqualTo(2));
            Assert.That(handler.HasuraRoles, Is.EqualTo(new[] { Roles.Modeller, Roles.Modeller }));
        }

        [Test]
        public void SendQueryAsync_ThrowsWhenMergeModeNoneAndMultipleChunks()
        {
            StubHttpMessageHandler handler = new([]);
            using GraphQlApiConnection connection = CreateConnectionWithHandler(handler);
            Dictionary<string, object> variables = new()
            {
                ["objects"] = new List<int> { 1, 2, 3 }
            };

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await connection.SendQueryAsync<List<int>>(
                    "query FetchItems($objects: [Int!]) { items }",
                    variables,
                    chunkingOptions: new QueryChunkingOptions
                    {
                        Enabled = true,
                        ChunkVariableName = "objects",
                        ChunkSize = 2,
                        MergeMode = ChunkMergeMode.None
                    }));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("MergeMode is None"));
            Assert.That(handler.RequestCount, Is.EqualTo(0));
        }
    }
}

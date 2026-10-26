using FWO.Data.Middleware;
using FWO.Middleware.Client;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RestSharp;

namespace FWO.Test
{
    [TestFixture]
    public class MiddlewareClientTest
    {
        private static readonly List<string> kFirstMemberPage = ["uid=a", "uid=b"];
        private static readonly List<string> kSecondMemberPage = ["uid=c"];
        private static readonly List<string> kAllMembers = ["uid=a", "uid=b", "uid=c"];
        private static readonly List<int> kBothUserIds = [1, 2];

        [Test]
        public async Task DeclaredPublicMethodsExecuteAgainstLocalServer()
        {
            await using LocalMiddlewareServer server = new();
            using MiddlewareClient client = new(server.BaseUrl);

            List<MethodInfo> methods = typeof(MiddlewareClient)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.Name != nameof(MiddlewareClient.SetAuthenticationToken) && method.Name != nameof(IDisposable.Dispose))
                .OrderBy(method => method.MetadataToken)
                .ToList();

            Assert.That(methods, Is.Not.Empty);

            foreach (MethodInfo method in methods)
            {
                object?[] args = BuildArguments(method.GetParameters());
                server.EnqueueResponse(BuildResponseBody(method));

                object? invocation = method.Invoke(client, args);
                if (invocation is Task task)
                {
                    await task;
                }
            }
        }

        [Test]
        public async Task GetGroupMembersReadsEveryPageWhileFurtherItemsFollow()
        {
            await using LocalMiddlewareServer server = new();
            using MiddlewareClient client = new(server.BaseUrl);
            server.EnqueueResponse(JsonSerializer.Serialize(kFirstMemberPage), hasMore: true);
            server.EnqueueResponse(JsonSerializer.Serialize(kSecondMemberPage), hasMore: false);
            GroupMemberGetParameters parameters = new() { GroupDn = "cn=group" };

            RestResponse<List<string>> response = await client.GetGroupMembers(parameters);

            List<(string PathAndQuery, string Body)> requests = [.. server.ReceivedRequests];
            Assert.Multiple(() =>
            {
                Assert.That(response.IsSuccessful, Is.True);
                Assert.That(response.Data, Is.EqualTo(kAllMembers));
                Assert.That(requests, Has.Count.EqualTo(2));
                Assert.That(ReadOffset(requests[0].Body), Is.Null);
                Assert.That(ReadOffset(requests[1].Body), Is.EqualTo(kFirstMemberPage.Count));
                Assert.That(parameters.Offset, Is.Null);
            });
        }

        [Test]
        public async Task GetGroupMembersWithCallerPagingReadsOnlyTheRequestedPage()
        {
            await using LocalMiddlewareServer server = new();
            using MiddlewareClient client = new(server.BaseUrl);
            server.EnqueueResponse(JsonSerializer.Serialize(kSecondMemberPage), hasMore: true);

            RestResponse<List<string>> response = await client.GetGroupMembers(new GroupMemberGetParameters { GroupDn = "cn=group", Limit = 1, Offset = 2 });

            Assert.Multiple(() =>
            {
                Assert.That(response.Data, Is.EqualTo(kSecondMemberPage));
                Assert.That(server.ReceivedRequests, Has.Count.EqualTo(1));
                Assert.That(ReadOffset(server.ReceivedRequests.Single().Body), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task GetUsersPassesTheOffsetOfFurtherPagesAsQueryParameter()
        {
            await using LocalMiddlewareServer server = new();
            using MiddlewareClient client = new(server.BaseUrl);
            server.EnqueueResponse(JsonSerializer.Serialize(new List<UserGetReturnParameters> { new() { UserId = 1 } }), hasMore: true);
            server.EnqueueResponse(JsonSerializer.Serialize(new List<UserGetReturnParameters> { new() { UserId = 2 } }));

            RestResponse<List<UserGetReturnParameters>> response = await client.GetUsers();

            List<(string PathAndQuery, string Body)> requests = [.. server.ReceivedRequests];
            Assert.Multiple(() =>
            {
                Assert.That(response.Data!.Select(user => user.UserId), Is.EqualTo(kBothUserIds));
                Assert.That(requests[0].PathAndQuery, Is.EqualTo("/api/User"));
                Assert.That(requests[1].PathAndQuery, Is.EqualTo("/api/User?offset=1"));
            });
        }

        [Test]
        public async Task AFailingFurtherPageIsReturnedInsteadOfAPartialList()
        {
            await using LocalMiddlewareServer server = new();
            using MiddlewareClient client = new(server.BaseUrl);
            server.EnqueueResponse(JsonSerializer.Serialize(new List<TenantGetReturnParameters> { new() { Id = 1 } }), hasMore: true);
            server.EnqueueResponse("\"failed\"", statusCode: HttpStatusCode.InternalServerError);

            RestResponse<List<TenantGetReturnParameters>> response = await client.GetTenants();

            Assert.Multiple(() =>
            {
                Assert.That(response.IsSuccessful, Is.False);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            });
        }

        [Test]
        public async Task AnEmptyPageEndsThePagingEvenWhenTheServerAnnouncesMore()
        {
            await using LocalMiddlewareServer server = new();
            using MiddlewareClient client = new(server.BaseUrl);
            server.EnqueueResponse("[]", hasMore: true);
            server.EnqueueResponse("[\"unexpected\"]");

            RestResponse<List<GroupGetReturnParameters>> response = await client.GetInternalGroups();

            Assert.Multiple(() =>
            {
                Assert.That(response.Data, Is.Empty);
                Assert.That(server.ReceivedRequests, Has.Count.EqualTo(1));
            });
        }

        private static int? ReadOffset(string body)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "offset", StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetInt32();
                }
            }
            return null;
        }

        [Test]
        public void SetAuthenticationTokenAndDisposeAreSafe()
        {
            using MiddlewareClient client = new("http://127.0.0.1:1/");

            Assert.That(client, Is.Not.Null);
            client.SetAuthenticationToken("jwt-token");
            client.Dispose();
            client.Dispose();
        }

        private static object?[] BuildArguments(ParameterInfo[] parameters)
        {
            object?[] args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = BuildArgument(parameters[i].ParameterType);
            }

            return args;
        }

        private static object? BuildArgument(Type parameterType)
        {
            if (parameterType == typeof(CancellationToken))
            {
                return default(CancellationToken);
            }

            if (parameterType == typeof(string))
            {
                return "test";
            }

            if (parameterType.IsValueType)
            {
                return Activator.CreateInstance(parameterType);
            }

            object? instance = Activator.CreateInstance(parameterType);
            if (instance != null)
            {
                return instance;
            }

            throw new InvalidOperationException($"Unable to create test argument for parameter type '{parameterType.FullName}'.");
        }

        private static string BuildResponseBody(MethodInfo method)
        {
            Type payloadType = GetPayloadType(method);
            if (payloadType == typeof(string))
            {
                return JsonSerializer.Serialize("ok");
            }

            if (payloadType == typeof(int))
            {
                return "1";
            }

            if (payloadType == typeof(bool))
            {
                return "true";
            }

            if (payloadType.IsEnum)
            {
                return "0";
            }

            if (payloadType.IsGenericType && payloadType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return "[]";
            }

            object? payload = null;
            try
            {
                payload = Activator.CreateInstance(payloadType);
            }
            catch
            {
                payload = null;
            }

            return payload != null ? JsonSerializer.Serialize(payload) : "{}";
        }

        private static Type GetPayloadType(MethodInfo method)
        {
            Type taskType = method.ReturnType;
            if (!taskType.IsGenericType)
            {
                return typeof(object);
            }

            Type resultType = taskType.GetGenericArguments()[0];
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(RestResponse<>))
            {
                return resultType.GetGenericArguments()[0];
            }

            return typeof(object);
        }

        internal sealed class LocalMiddlewareServer : IAsyncDisposable
        {
            private readonly HttpListener listener = new();
            private readonly ConcurrentQueue<QueuedResponse> responses = new();
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private readonly Task listenerTask;

            public string BaseUrl { get; }

            /// <summary>
            /// Path with query and body of every received request, in the order received.
            /// </summary>
            public ConcurrentQueue<(string PathAndQuery, string Body)> ReceivedRequests { get; } = new();

            public LocalMiddlewareServer()
            {
                int port = GetFreePort();
                BaseUrl = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Add($"http://127.0.0.1:{port}/api/");
                listener.Start();
                listenerTask = Task.Run(ListenAsync);
            }

            public void EnqueueResponse(string body, bool? hasMore = null, HttpStatusCode statusCode = HttpStatusCode.OK)
            {
                responses.Enqueue(new QueuedResponse(body, hasMore, statusCode));
            }

            private sealed record QueuedResponse(string Body, bool? HasMore, HttpStatusCode StatusCode);

            private async Task ListenAsync()
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        HttpListenerContext context = await listener.GetContextAsync();
                        using (StreamReader requestReader = new(context.Request.InputStream, Encoding.UTF8))
                        {
                            ReceivedRequests.Enqueue((context.Request.Url?.PathAndQuery ?? "", await requestReader.ReadToEndAsync()));
                        }
                        QueuedResponse response = responses.TryDequeue(out QueuedResponse? queuedResponse)
                            ? queuedResponse
                            : new QueuedResponse("{}", null, HttpStatusCode.OK);
                        byte[] bytes = Encoding.UTF8.GetBytes(response.Body);

                        if (response.HasMore != null)
                        {
                            context.Response.AddHeader(MiddlewareClient.kHasMoreHeader, response.HasMore.Value ? "true" : "false");
                        }
                        context.Response.StatusCode = (int)response.StatusCode;
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                        context.Response.OutputStream.Close();
                    }
                }
                catch (HttpListenerException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            public async ValueTask DisposeAsync()
            {
                cancellationTokenSource.Cancel();
                listener.Close();
                try
                {
                    await listenerTask;
                }
                catch
                {
                }

                cancellationTokenSource.Dispose();
            }

            private static int GetFreePort()
            {
                TcpListener tcpListener = new(IPAddress.Loopback, 0);
                tcpListener.Start();
                int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                tcpListener.Stop();
                return port;
            }
        }
    }
}

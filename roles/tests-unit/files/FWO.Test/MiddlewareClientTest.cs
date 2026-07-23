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

        private sealed class LocalMiddlewareServer : IAsyncDisposable
        {
            private readonly HttpListener listener = new();
            private readonly ConcurrentQueue<string> responses = new();
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private readonly Task listenerTask;

            public string BaseUrl { get; }

            public LocalMiddlewareServer()
            {
                int port = GetFreePort();
                BaseUrl = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Add($"http://127.0.0.1:{port}/api/");
                listener.Start();
                listenerTask = Task.Run(ListenAsync);
            }

            public void EnqueueResponse(string body)
            {
                responses.Enqueue(body);
            }

            private async Task ListenAsync()
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        HttpListenerContext context = await listener.GetContextAsync();
                        string body = responses.TryDequeue(out string? responseBody) ? responseBody : "{}";
                        byte[] bytes = Encoding.UTF8.GetBytes(body);

                        context.Response.StatusCode = (int)HttpStatusCode.OK;
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

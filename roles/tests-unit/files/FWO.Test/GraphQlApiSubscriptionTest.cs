using FWO.Api.Client;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class GraphQlApiSubscriptionTest
    {
        [Test]
        public void ApiSubscriptionDisposeCallsImplementationOnlyOnce()
        {
            TrackingSubscription subscription = new();

            subscription.Dispose();
            subscription.Dispose();

            Assert.That(subscription.DisposeCount, Is.EqualTo(1));
            Assert.That(subscription.DisposedState, Is.True);
        }

        [Test]
        public void GraphQlApiSubscriptionCreatesSubscriptionOnConstruction()
        {
            TestApiConnection apiConnection = new();
            using TestGraphQlApiSubscription<string> subscription = CreateSubscription<string>(apiConnection);

            Assert.That(subscription.CreateSubscriptionCount, Is.EqualTo(1));
        }

        [Test]
        public void GraphQlApiSubscriptionRecreateCreatesFreshSubscription()
        {
            TestApiConnection apiConnection = new();
            using TestGraphQlApiSubscription<string> subscription = CreateSubscription<string>(apiConnection);
            GraphQLHttpClient recreatedClient = new(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new HttpClient());

            TestGraphQlApiSubscription<string> recreated = (TestGraphQlApiSubscription<string>)subscription.Recreate(recreatedClient);

            Assert.That(recreated, Is.Not.SameAs(subscription));
            Assert.That(recreated.CreateSubscriptionCount, Is.EqualTo(1));
            Assert.That(subscription.CreateSubscriptionCount, Is.EqualTo(1));
        }

        [Test]
        public void GraphQlApiSubscriptionDispatchesConvertedResponse()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<string>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            string? receivedValue = null;
            using StreamBackedGraphQlApiSubscription<string> subscription = CreateStreamBackedSubscription<string>(apiConnection, value => receivedValue = value);

            stream.Emit(new GraphQLResponse<object> { Data = new JObject { ["test"] = "value" } });

            Assert.That(stream.SubscribeCount, Is.EqualTo(1));
            Assert.That(receivedValue, Is.EqualTo("value"));
        }

        [Test]
        public void GraphQlApiSubscriptionDisposesActiveSubscriptionWhenResponseDataIsNull()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<string>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            using StreamBackedGraphQlApiSubscription<string> subscription = CreateStreamBackedSubscription<string>(apiConnection, _ => { });

            stream.Emit(new GraphQLResponse<object> { Data = null! });

            Assert.That(stream.ActiveSubscription!.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void GraphQlApiSubscriptionInvokesExternalExceptionHandlerForCurrentStreamErrors()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<string>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            int exceptionCount = 0;
            using StreamBackedGraphQlApiSubscription<string> subscription = CreateStreamBackedSubscription<string>(apiConnection, _ => { }, _ => exceptionCount++);

            stream.EmitError(new InvalidOperationException("boom"));

            Assert.That(exceptionCount, Is.EqualTo(1));
        }

        [Test]
        public void GraphQlApiSubscriptionIgnoresErrorsAfterDispose()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<string>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            int exceptionCount = 0;
            StreamBackedGraphQlApiSubscription<string> subscription = CreateStreamBackedSubscription<string>(apiConnection, _ => { }, _ => exceptionCount++);

            subscription.Dispose();
            stream.EmitError(new InvalidOperationException("boom"));

            Assert.That(exceptionCount, Is.EqualTo(0));
        }

        [Test]
        public void GraphQlApiSubscriptionDoesNotDispatchAfterDispose()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<string>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            int updateCount = 0;
            StreamBackedGraphQlApiSubscription<string> subscription = CreateStreamBackedSubscription<string>(apiConnection, _ => updateCount++);

            subscription.Dispose();
            stream.Emit(new GraphQLResponse<object> { Data = new JObject { ["test"] = "value" } });

            Assert.That(updateCount, Is.EqualTo(0));
        }

        [Test]
        public void GraphQlApiSubscriptionThrowsForResponseWithoutResultProperty()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<string>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            using StreamBackedGraphQlApiSubscription<string> subscription = CreateStreamBackedSubscription<string>(apiConnection, _ => { });

            Assert.Throws<Exception>(() => stream.Emit(new GraphQLResponse<object> { Data = new JObject() }));
        }

        [Test]
        public void GraphQlApiSubscriptionThrowsForUnconvertibleResult()
        {
            ManualGraphQlObservable stream = new();
            StreamBackedGraphQlApiSubscription<int>.Streams.Enqueue(stream);
            TestApiConnection apiConnection = new();
            using StreamBackedGraphQlApiSubscription<int> subscription = CreateStreamBackedSubscription<int>(apiConnection, _ => { });

            Assert.Throws<FormatException>(() => stream.Emit(new GraphQLResponse<object> { Data = new JObject { ["test"] = "not-an-int" } }));
        }

        private static TestGraphQlApiSubscription<T> CreateSubscription<T>(ApiConnection apiConnection)
        {
            GraphQLHttpClient graphQlClient = new(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new HttpClient());
            return new TestGraphQlApiSubscription<T>(
                apiConnection,
                graphQlClient,
                new GraphQLRequest("subscription Test { test }"),
                _ => { },
                _ => { });
        }

        private static StreamBackedGraphQlApiSubscription<T> CreateStreamBackedSubscription<T>(ApiConnection apiConnection,
            GraphQlApiSubscription<T>.SubscriptionUpdate onUpdate, Action<Exception>? exceptionHandler = null)
        {
            GraphQLHttpClient graphQlClient = new(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new HttpClient());
            return new StreamBackedGraphQlApiSubscription<T>(
                apiConnection,
                graphQlClient,
                new GraphQLRequest("subscription Test { test }"),
                exceptionHandler ?? (_ => { }),
                onUpdate);
        }

        private sealed class TrackingSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }
            public bool DisposedState => IsDisposed;

            internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
            {
                return new TrackingSubscription();
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }
        }

        private sealed class TestGraphQlApiSubscription<T> : GraphQlApiSubscription<T>
        {
            private readonly ApiConnection apiConnection;
            private readonly Action<Exception> exceptionHandler;
            private readonly SubscriptionUpdate onUpdate;

            public int CreateSubscriptionCount { get; private set; }
            public int DisposeCount { get; private set; }

            public TestGraphQlApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request,
                Action<Exception> exceptionHandler, SubscriptionUpdate onUpdate)
                : base(apiConnection, graphQlClient, request, exceptionHandler, onUpdate)
            {
                this.apiConnection = apiConnection;
                this.exceptionHandler = exceptionHandler;
                this.onUpdate = onUpdate;
            }

            protected override void CreateSubscription()
            {
                CreateSubscriptionCount++;
            }

            internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
            {
                return new TestGraphQlApiSubscription<T>(apiConnection, graphQlClient, Request, exceptionHandler, onUpdate);
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
                base.Dispose(disposing);
            }
        }

        private sealed class StreamBackedGraphQlApiSubscription<T> : GraphQlApiSubscription<T>
        {
            public static Queue<ManualGraphQlObservable> Streams { get; } = [];
            private readonly ApiConnection apiConnection;
            private readonly Action<Exception> exceptionHandler;
            private readonly SubscriptionUpdate onUpdate;

            public StreamBackedGraphQlApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request,
                Action<Exception> exceptionHandler, SubscriptionUpdate onUpdate)
                : base(apiConnection, graphQlClient, request, exceptionHandler, onUpdate)
            {
                this.apiConnection = apiConnection;
                this.exceptionHandler = exceptionHandler;
                this.onUpdate = onUpdate;
            }

            protected override IObservable<GraphQLResponse<dynamic>> CreateSubscriptionStream(Action<Exception> exceptionHandler)
            {
                ManualGraphQlObservable stream = Streams.Dequeue();
                stream.ExceptionHandler = exceptionHandler;
                return (IObservable<GraphQLResponse<dynamic>>)(object)stream;
            }

            internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
            {
                return new StreamBackedGraphQlApiSubscription<T>(apiConnection, graphQlClient, Request, exceptionHandler, onUpdate);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                Streams.Clear();
            }
        }

        private sealed class ManualGraphQlObservable : IObservable<GraphQLResponse<object>>
        {
            private IObserver<GraphQLResponse<object>>? observer;

            public int SubscribeCount { get; private set; }
            public ManualObservableSubscription? ActiveSubscription { get; private set; }
            public Action<Exception>? ExceptionHandler { get; set; }

            public IDisposable Subscribe(IObserver<GraphQLResponse<object>> observer)
            {
                SubscribeCount++;
                this.observer = observer;
                ActiveSubscription = new ManualObservableSubscription();
                return ActiveSubscription;
            }

            public void Emit(GraphQLResponse<object> response)
            {
                if (ActiveSubscription?.IsDisposed == false)
                {
                    observer?.OnNext(response);
                }
            }

            public void EmitError(Exception exception)
            {
                ExceptionHandler?.Invoke(exception);
            }
        }

        private sealed class ManualObservableSubscription : IDisposable
        {
            public int DisposeCount { get; private set; }
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                IsDisposed = true;
            }
        }

        private sealed class TestApiConnection : ApiConnection
        {
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new NotImplementedException();
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void SetAuthHeader(string jwt)
            {
                InvokeOnAuthHeaderChanged(this, jwt);
            }

            public override void SetRole(string role)
            { }

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            { }

            public override void SetProperRole(ClaimsPrincipal user, List<string> targetRoleList)
            { }

            public override void SwitchBack()
            { }

            protected override void Dispose(bool disposing)
            { }

            public override void DisposeSubscriptions<T>()
            { }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                throw new NotImplementedException();
            }
        }
    }
}

using FWO.Api.Client;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using NUnit.Framework;
using System.Net;

namespace FWO.Test
{
    [TestFixture]
    internal class GraphQlApiConnectionSubscriptionManagementTest
    {
        [Test]
        public void DisposeSubscriptionsDisposesAndRemovesOnlyExactType()
        {
            TestGraphQlApiConnection connection = new();
            FirstSubscription first = new();
            DerivedFirstSubscription derived = new();
            SecondSubscription second = new();
            connection.AddSubscription(first);
            connection.AddSubscription(derived);
            connection.AddSubscription(second);

            connection.DisposeSubscriptions<FirstSubscription>();

            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(derived.DisposeCount, Is.EqualTo(0));
            Assert.That(second.DisposeCount, Is.EqualTo(0));
            Assert.That(connection.SubscriptionCount, Is.EqualTo(2));
        }

        [Test]
        public void DisposeDisposesRemainingSubscriptions()
        {
            TestGraphQlApiConnection connection = new();
            FirstSubscription first = new();
            SecondSubscription second = new();
            connection.AddSubscription(first);
            connection.AddSubscription(second);

            connection.Dispose();

            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(second.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ReconnectSubscriptionsAsyncSkipsDisposedSubscriptions()
        {
            TestGraphQlApiConnection connection = new();
            TrackingSubscription active = new();
            TrackingSubscription disposed = new();
            connection.AddSubscription(active);
            connection.AddSubscription(disposed);

            disposed.Dispose();

            await connection.ReconnectSubscriptionsAsync("jwt", CancellationToken.None);

            Assert.That(active.RebindCount, Is.EqualTo(1));
            Assert.That(active.DisposeCount, Is.EqualTo(0));
            Assert.That(disposed.RebindCount, Is.EqualTo(0));
            Assert.That(disposed.DisposeCount, Is.EqualTo(1));
            Assert.That(connection.SubscriptionCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ReconnectSubscriptionsAsyncRebindsRebindableSubscriptionsInPlace()
        {
            TestGraphQlApiConnection connection = new();
            RebindableSubscription active = new();
            connection.AddSubscription(active);

            await connection.ReconnectSubscriptionsAsync("jwt", CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(active.RebindCount, Is.EqualTo(1));
                Assert.That(active.DisposeCount, Is.EqualTo(0));
                Assert.That(connection.SubscriptionCount, Is.EqualTo(1));
                Assert.That(connection.FirstSubscription, Is.SameAs(active));
            });

            active.Dispose();

            Assert.That(active.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ReconnectAndDisposeDoNotPublishAClientAfterDisposal()
        {
            RaceTestGraphQlApiConnection connection = new();
            TrackingSubscription subscription = new();
            connection.AddSubscription(subscription);
            connection.BlockReplacementClient();

            Task reconnect = Task.Run(() => connection.ReconnectSubscriptionsAsync("jwt", CancellationToken.None));
            await connection.ReplacementCreationStarted.Task;

            Task dispose = Task.Run(connection.Dispose);
            connection.AllowReplacementCreation();

            await Task.WhenAll(reconnect, dispose);

            Assert.Multiple(() =>
            {
                Assert.That(connection.SubscriptionCount, Is.EqualTo(0));
                Assert.That(subscription.DisposeCount, Is.EqualTo(1));
                Assert.That(connection.ReplacementClientHandler?.WasDisposed, Is.True);
                Assert.That(connection.HasApiClient, Is.False);
                Assert.That(connection.HasSubscriptionClient, Is.False);
            });

            Assert.Throws<ObjectDisposedException>(() => connection.GetSubscription<object>(
                _ => { }, _ => { }, "subscription"));
        }

        private sealed class TestGraphQlApiConnection : GraphQlApiConnection
        {
            public TestGraphQlApiConnection() : base("http://localhost")
            { }

            public int SubscriptionCount => subscriptions.Count;
            public ApiSubscription? FirstSubscription => subscriptions.Count > 0 ? subscriptions[0] : null;

            public void AddSubscription(ApiSubscription subscription)
            {
                subscriptions.Add(subscription);
            }
        }

        private sealed class RaceTestGraphQlApiConnection : GraphQlApiConnection
        {
            private bool blockReplacementClient;
            private readonly TaskCompletionSource<bool> allowReplacementCreation = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public RaceTestGraphQlApiConnection() : base("http://localhost")
            { }

            public TaskCompletionSource<bool> ReplacementCreationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TrackingHandler? ReplacementClientHandler { get; private set; }
            public int SubscriptionCount => subscriptions.Count;
            public bool HasApiClient => GetPrivateClient("graphQlClient") != null;
            public bool HasSubscriptionClient => GetPrivateSubscriptionClient() != null;

            public void AddSubscription(ApiSubscription subscription)
            {
                subscriptions.Add(subscription);
            }

            public void BlockReplacementClient()
            {
                blockReplacementClient = true;
            }

            public void AllowReplacementCreation()
            {
                allowReplacementCreation.SetResult(true);
            }

            protected override GraphQLHttpClient CreateSubscriptionClient(string apiServerUri)
            {
                if (blockReplacementClient)
                {
                    blockReplacementClient = false;
                    ReplacementCreationStarted.SetResult(true);
                    allowReplacementCreation.Task.GetAwaiter().GetResult();
                }

                ReplacementClientHandler = new TrackingHandler();
                return new GraphQLHttpClient(
                    new GraphQLHttpClientOptions
                    {
                        EndPoint = new Uri(apiServerUri),
                        HttpMessageHandler = ReplacementClientHandler
                    },
                    new SystemTextJsonSerializer());
            }

            private GraphQLHttpClient? GetPrivateSubscriptionClient()
            {
                return GetPrivateClient("graphQlSubscriptionClient");
            }

            private GraphQLHttpClient? GetPrivateClient(string fieldName)
            {
                return typeof(GraphQlApiConnection)
                    .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(this) as GraphQLHttpClient;
            }
        }

        private sealed class TrackingHandler : HttpMessageHandler
        {
            public bool WasDisposed { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            protected override void Dispose(bool disposing)
            {
                WasDisposed = true;
                base.Dispose(disposing);
            }
        }

        private class FirstSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }

            internal override void Rebind(GraphQLHttpClient graphQlClient)
            {
                RebindCount++;
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }

            public int RebindCount { get; private set; }
        }

        private sealed class DerivedFirstSubscription : FirstSubscription
        { }

        private sealed class SecondSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }

            internal override void Rebind(GraphQLHttpClient graphQlClient)
            {
                RebindCount++;
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }

            public int RebindCount { get; private set; }
        }

        private sealed class TrackingSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }
            public int RebindCount { get; private set; }

            internal override void Rebind(GraphQLHttpClient graphQlClient)
            {
                RebindCount++;
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }
        }

        private sealed class RebindableSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }
            public int RebindCount { get; private set; }

            internal override void Rebind(GraphQLHttpClient graphQlClient)
            {
                RebindCount++;
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }
        }
    }
}

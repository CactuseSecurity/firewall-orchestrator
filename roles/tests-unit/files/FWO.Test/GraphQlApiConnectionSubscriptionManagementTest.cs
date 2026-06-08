using FWO.Api.Client;
using GraphQL.Client.Http;
using NUnit.Framework;

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

            Assert.That(active.RecreateCount, Is.EqualTo(1));
            Assert.That(active.DisposeCount, Is.EqualTo(1));
            Assert.That(disposed.RecreateCount, Is.EqualTo(0));
            Assert.That(disposed.DisposeCount, Is.EqualTo(1));
            Assert.That(connection.SubscriptionCount, Is.EqualTo(1));
        }

        private sealed class TestGraphQlApiConnection : GraphQlApiConnection
        {
            public TestGraphQlApiConnection() : base("http://localhost")
            { }

            public int SubscriptionCount => subscriptions.Count;

            public void AddSubscription(ApiSubscription subscription)
            {
                subscriptions.Add(subscription);
            }
        }

        private class FirstSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }

            internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
            {
                return new FirstSubscription();
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }
        }

        private sealed class DerivedFirstSubscription : FirstSubscription
        { }

        private sealed class SecondSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }

            internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
            {
                return new SecondSubscription();
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }
        }

        private sealed class TrackingSubscription : ApiSubscription
        {
            public int DisposeCount { get; private set; }
            public int RecreateCount { get; private set; }

            internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
            {
                RecreateCount++;
                return new TrackingSubscription();
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }
        }
    }
}

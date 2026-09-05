using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FWO.Basics.Exceptions;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the bounded startup retry the middleware runs before it starts its web server.
    /// </summary>
    /// <remarks>
    /// The clock and the delay are both injected, so these tests exercise the real budget
    /// arithmetic without waiting for it: the delay the probe awaits advances the virtual
    /// clock by exactly the interval it was asked to sleep for.
    /// </remarks>
    [TestFixture]
    internal class ApiStartupProbeTest
    {
        private const string kApiServerUri = "https://api.example.com:9443/api/v1/graphql";
        private const string kQueryResult = "ldap-connections";
        private const string kFailureReason = "no route to host";

        private static readonly DateTime kStartTime = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

        private DateTime virtualNow;
        private List<TimeSpan> awaitedDelays = [];
        private int attemptsMade;

        [SetUp]
        public void ResetVirtualClock()
        {
            virtualNow = kStartTime;
            awaitedDelays = [];
            attemptsMade = 0;
        }

        /// <summary>Advances the virtual clock instead of sleeping, and records the interval.</summary>
        private Task RecordDelay(TimeSpan interval)
        {
            awaitedDelays.Add(interval);
            virtualNow += interval;
            return Task.CompletedTask;
        }

        /// <summary>A query that fails the first <paramref name="failures"/> times, then succeeds.</summary>
        private Func<Task<string>> QueryFailingTimes(int failures)
        {
            return () =>
            {
                attemptsMade++;
                if (attemptsMade <= failures)
                {
                    throw new InvalidOperationException(kFailureReason);
                }
                return Task.FromResult(kQueryResult);
            };
        }

        private Task<string> RunProbe(Func<Task<string>> query, TimeSpan budget)
        {
            return ApiStartupProbe.RunFirstQueryAsync(query, kApiServerUri, budget, RecordDelay, () => virtualNow);
        }

        [Test]
        public async Task SucceedsOnFirstAttemptWithoutWaiting()
        {
            string result = await RunProbe(QueryFailingTimes(0), ApiStartupProbe.kStartupBudget);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(kQueryResult));
                Assert.That(attemptsMade, Is.EqualTo(1));
                Assert.That(awaitedDelays, Is.Empty);
            });
        }

        [Test]
        public async Task RetriesUntilTheApiAnswers()
        {
            string result = await RunProbe(QueryFailingTimes(3), ApiStartupProbe.kStartupBudget);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(kQueryResult));
                Assert.That(attemptsMade, Is.EqualTo(4));
                Assert.That(awaitedDelays, Has.Count.EqualTo(3));
            });
        }

        [Test]
        public async Task DoublesTheRetryDelayUpToTheCeiling()
        {
            await RunProbe(QueryFailingTimes(6), TimeSpan.FromMinutes(10));

            Assert.Multiple(() =>
            {
                Assert.That(awaitedDelays[0], Is.EqualTo(ApiStartupProbe.kFirstRetryDelay));
                Assert.That(awaitedDelays[1], Is.EqualTo(ApiStartupProbe.kFirstRetryDelay + ApiStartupProbe.kFirstRetryDelay));
                Assert.That(awaitedDelays[^1], Is.EqualTo(ApiStartupProbe.kMaxRetryDelay));
                Assert.That(awaitedDelays, Has.All.LessThanOrEqualTo(ApiStartupProbe.kMaxRetryDelay));
            });
        }

        [Test]
        public void GivesUpOnceTheBudgetIsSpent()
        {
            TimeSpan budget = TimeSpan.FromSeconds(30);

            ApiUnavailableAtStartupException exception = Assert.ThrowsAsync<ApiUnavailableAtStartupException>(
                () => RunProbe(QueryFailingTimes(int.MaxValue), budget))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain(kApiServerUri), "the operator must be told which endpoint failed");
                Assert.That(exception.Message, Does.Contain("503"), "the 503 an operator actually sees must be explained");
                Assert.That(exception.Message, Does.Contain("tls_client_certificate"));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.InnerException!.Message, Is.EqualTo(kFailureReason), "the last real failure must survive");
            });
        }

        [Test]
        public void NeverWaitsLongerThanTheBudget()
        {
            TimeSpan budget = TimeSpan.FromSeconds(30);

            Assert.ThrowsAsync<ApiUnavailableAtStartupException>(() => RunProbe(QueryFailingTimes(int.MaxValue), budget));

            Assert.That(virtualNow - kStartTime, Is.LessThanOrEqualTo(budget),
                "a bounded budget is the whole point: overshooting it is the silent hang again");
        }

        [Test]
        public void GivesUpImmediatelyWhenNoBudgetIsLeft()
        {
            Assert.ThrowsAsync<ApiUnavailableAtStartupException>(
                () => RunProbe(QueryFailingTimes(int.MaxValue), TimeSpan.Zero));

            Assert.Multiple(() =>
            {
                Assert.That(attemptsMade, Is.EqualTo(1), "the query is always tried once, budget or not");
                Assert.That(awaitedDelays, Is.Empty);
            });
        }

        [Test]
        public void ReportsTheAttemptCountAndElapsedTime()
        {
            string message = ApiStartupProbe.BuildFailureMessage(kApiServerUri, 7, TimeSpan.FromSeconds(42));

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("7 attempts"));
                Assert.That(message, Does.Contain("42 seconds"));
            });
        }
    }
}

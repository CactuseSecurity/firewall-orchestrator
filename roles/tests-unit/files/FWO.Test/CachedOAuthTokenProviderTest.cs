using FWO.Basics.Exceptions;
using FWO.Basics.OAuth;
using NUnit.Framework;
using System.Net;

namespace FWO.Test
{
    /// <summary>
    /// Tests that the cached OAuth 2.0 token provider of FWO.Basics keeps a token until shortly before it expires, renews
    /// it only once for parallel callers, and drops a token the resource server refused.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class CachedOAuthTokenProviderTest
    {
        private const string kFirstToken = "token-1";
        private const string kSecondToken = "token-2";
        private const long kOneHourSeconds = 3600;
        private const int kParallelCallers = 5;

        private static readonly DateTimeOffset kNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        private static readonly OAuthClientCredentials kCredentials = new(new Uri("https://sso.example.test/token"), "client", "secret");

        private static CachedOAuthTokenProvider CreateProvider(OAuthTestEndpoint endpoint, OAuthTestClock clock)
        {
            return new CachedOAuthTokenProvider(new OAuthTokenClient(endpoint, clock), kCredentials, clock);
        }

        [Test]
        public async Task GetAccessToken_KeepsTheTokenUntilShortlyBeforeItExpires()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kFirstToken, kOneHourSeconds);
            endpoint.AnswerToken(kSecondToken, kOneHourSeconds);
            OAuthTestClock clock = new(kNow);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, clock);

            string first = await provider.GetAccessTokenAsync(CancellationToken.None);
            clock.Now = kNow.AddSeconds(kOneHourSeconds) - CachedOAuthTokenProvider.kRenewalLeadTime - TimeSpan.FromSeconds(1);
            string beforeRenewal = await provider.GetAccessTokenAsync(CancellationToken.None);
            clock.Now = kNow.AddSeconds(kOneHourSeconds) - CachedOAuthTokenProvider.kRenewalLeadTime;
            string afterRenewal = await provider.GetAccessTokenAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(first, Is.EqualTo(kFirstToken));
                Assert.That(beforeRenewal, Is.EqualTo(kFirstToken));
                Assert.That(afterRenewal, Is.EqualTo(kSecondToken));
                Assert.That(endpoint.Requests, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public async Task GetAccessToken_RenewsAShortLivedTokenAfterHalfItsLifetime()
        {
            const long shortLifetimeSeconds = 20;
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kFirstToken, shortLifetimeSeconds);
            endpoint.AnswerToken(kSecondToken, shortLifetimeSeconds);
            OAuthTestClock clock = new(kNow);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, clock);

            await provider.GetAccessTokenAsync(CancellationToken.None);
            clock.Now = kNow.AddSeconds(shortLifetimeSeconds / 2 - 1);
            string beforeHalf = await provider.GetAccessTokenAsync(CancellationToken.None);
            clock.Now = kNow.AddSeconds(shortLifetimeSeconds / 2);
            string atHalf = await provider.GetAccessTokenAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(beforeHalf, Is.EqualTo(kFirstToken));
                Assert.That(atHalf, Is.EqualTo(kSecondToken));
            }
        }

        [Test]
        public async Task GetAccessToken_KeepsATokenWithoutExpiryForAShortTime()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kFirstToken);
            endpoint.AnswerToken(kSecondToken);
            OAuthTestClock clock = new(kNow);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, clock);

            await provider.GetAccessTokenAsync(CancellationToken.None);
            clock.Now = kNow + CachedOAuthTokenProvider.kLifetimeWithoutExpiry / 4;
            string kept = await provider.GetAccessTokenAsync(CancellationToken.None);
            clock.Now = kNow + CachedOAuthTokenProvider.kLifetimeWithoutExpiry;
            string renewed = await provider.GetAccessTokenAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(kept, Is.EqualTo(kFirstToken));
                Assert.That(renewed, Is.EqualTo(kSecondToken));
            }
        }

        [Test]
        public async Task GetAccessToken_DoesNotKeepATokenThatHasExpiredOnArrival()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kFirstToken, kOneHourSeconds);
            endpoint.AnswerToken(kSecondToken, kOneHourSeconds);
            // the token endpoint computes the expiry with a clock two hours behind the one of the provider
            OAuthTestClock providerClock = new(kNow.AddHours(2));
            using CachedOAuthTokenProvider provider = new(new OAuthTokenClient(endpoint, new OAuthTestClock(kNow)), kCredentials, providerClock);

            await provider.GetAccessTokenAsync(CancellationToken.None);
            string next = await provider.GetAccessTokenAsync(CancellationToken.None);

            Assert.That(next, Is.EqualTo(kSecondToken));
        }

        [Test]
        public async Task GetAccessToken_ParallelCallersShareOneRenewal()
        {
            OAuthTestEndpoint endpoint = new();
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            endpoint.Gate = release.Task;
            endpoint.AnswerToken(kFirstToken, kOneHourSeconds);
            endpoint.AnswerToken(kSecondToken, kOneHourSeconds);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, new OAuthTestClock(kNow));

            List<Task<string>> callers = [.. Enumerable.Range(0, kParallelCallers).Select(_ => provider.GetAccessTokenAsync(CancellationToken.None))];
            release.SetResult();
            string[] tokens = await Task.WhenAll(callers);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(tokens, Is.All.EqualTo(kFirstToken));
                Assert.That(endpoint.Requests, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task Invalidate_DropsTheRefusedToken()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kFirstToken, kOneHourSeconds);
            endpoint.AnswerToken(kSecondToken, kOneHourSeconds);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, new OAuthTestClock(kNow));

            string refused = await provider.GetAccessTokenAsync(CancellationToken.None);
            provider.Invalidate(refused);
            string renewed = await provider.GetAccessTokenAsync(CancellationToken.None);

            Assert.That(renewed, Is.EqualTo(kSecondToken));
        }

        [Test]
        public async Task Invalidate_KeepsATokenRenewedMeanwhile()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.AnswerToken(kFirstToken, kOneHourSeconds);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, new OAuthTestClock(kNow));

            string current = await provider.GetAccessTokenAsync(CancellationToken.None);
            provider.Invalidate("an-older-token");
            string kept = await provider.GetAccessTokenAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(kept, Is.EqualTo(current));
                Assert.That(endpoint.Requests, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void Invalidate_WithoutToken_DoesNothing()
        {
            using CachedOAuthTokenProvider provider = CreateProvider(new OAuthTestEndpoint(), new OAuthTestClock(kNow));

            Assert.That(() => provider.Invalidate(kFirstToken), Throws.Nothing);
        }

        [Test]
        public async Task GetAccessToken_FailedRenewal_IsThrownAndRetriedOnTheNextCall()
        {
            OAuthTestEndpoint endpoint = new();
            endpoint.Answer(HttpStatusCode.ServiceUnavailable, "");
            endpoint.AnswerToken(kFirstToken, kOneHourSeconds);
            using CachedOAuthTokenProvider provider = CreateProvider(endpoint, new OAuthTestClock(kNow));

            Assert.ThrowsAsync<OAuthTokenException>(() => provider.GetAccessTokenAsync(CancellationToken.None));
            string token = await provider.GetAccessTokenAsync(CancellationToken.None);

            Assert.That(token, Is.EqualTo(kFirstToken));
        }

        [Test]
        public void Dispose_DisposesTheTokenClient()
        {
            OAuthTestEndpoint endpoint = new();
            CachedOAuthTokenProvider provider = CreateProvider(endpoint, new OAuthTestClock(kNow));

            provider.Dispose();

            Assert.That(endpoint.IsDisposed, Is.True);
        }

        [Test]
        public void Constructor_RequiresClientAndCredentials()
        {
            using OAuthTokenClient tokenClient = new(new OAuthTestEndpoint());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new CachedOAuthTokenProvider(null!, kCredentials), Throws.ArgumentNullException);
                Assert.That(() => new CachedOAuthTokenProvider(tokenClient, null!), Throws.ArgumentNullException);
            }
        }
    }
}

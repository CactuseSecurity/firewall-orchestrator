using FWO.Middleware.Server;
using NUnit.Framework;
using System.Net;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class LoginThrottleTest
    {
        private static readonly IPAddress kClient = IPAddress.Parse("192.0.2.10");
        private static readonly IPAddress kOtherClient = IPAddress.Parse("192.0.2.11");
        private static readonly IPAddress kUiServer = IPAddress.Parse("198.51.100.5");
        private static readonly List<IPAddress> kTrustedUiServer = [kUiServer];
        private static readonly List<string> kTrustedHosts = ["198.51.100.5", " ", "::ffff:198.51.100.6"];
        private static readonly List<string> kUnresolvableHosts = ["unresolvable.invalid"];

        [Test]
        public void BeginAttempt_NeverLimitsAnonymousRequests()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 1);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(Admits(throttle, null, kClient), Is.True);
                Assert.That(Admits(throttle, "", kClient), Is.True);
            }
        }

        [Test]
        public void BeginAttempt_LimitsCredentialedAttemptsPerClient()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 2, userFailures: 10);

            Assert.That(Admits(throttle, "alice", kClient), Is.True);
            Assert.That(Admits(throttle, "bob", kClient), Is.True);
            Assert.That(Admits(throttle, "carol", kClient), Is.False);
            Assert.That(Admits(throttle, "carol", kOtherClient), Is.True);
        }

        [Test]
        public void BeginAttempt_TreatsIpv4MappedAddressAsSameClient()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 10);

            Assert.That(Admits(throttle, "alice", kClient), Is.True);
            Assert.That(Admits(throttle, "alice", kClient.MapToIPv6()), Is.False);
        }

        [Test]
        public void BeginAttempt_ExemptsTrustedClientsFromClientLimit()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 10, trusted: kTrustedUiServer);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(Admits(throttle, $"user{attempt}", kUiServer), Is.True);
            }
        }

        [Test]
        public void BeginAttempt_BlocksUserAfterFailuresFromSameClientOnly()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 100, userFailures: 2, trusted: kTrustedUiServer);

            Fail(throttle, "Alice", kUiServer);
            Assert.That(Admits(throttle, "alice", kUiServer), Is.True);
            Fail(throttle, " alice ", kUiServer);

            Assert.That(Admits(throttle, "ALICE", kUiServer), Is.False, "trusted clients stay subject to the user limit");
            Assert.That(Admits(throttle, "alice", kClient), Is.True);
            Assert.That(Admits(throttle, "bob", kUiServer), Is.True);
        }

        [Test]
        public void BeginAttempt_SuccessfulAttemptsDoNotCountAsFailures()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 100, userFailures: 1);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(Admits(throttle, "alice", kClient), Is.True);
            }
        }

        [Test]
        public async Task BeginAttempt_AllowsAttemptsAgainAfterWindow()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 1, window: TimeSpan.FromMilliseconds(100));
            Fail(throttle, "alice", kClient);
            Assert.That(Admits(throttle, "alice", kClient), Is.False);

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            while (!Admits(throttle, "alice", kClient))
            {
                await Task.Delay(50, timeout.Token);
            }
        }

        [Test]
        public void BeginAttempt_GroupsUnknownClients()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 10);

            Assert.That(Admits(throttle, "alice", null), Is.True);
            Assert.That(Admits(throttle, "bob", null), Is.False);
        }

        [Test]
        public void BeginAttempt_CountsRunningAttemptsOfTheUserAsPossibleFailures()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 100, userFailures: 2);

            LoginAttempt? first = throttle.BeginAttempt("alice", kClient);
            using LoginAttempt? second = throttle.BeginAttempt("alice", kClient);
            using LoginAttempt? refused = throttle.BeginAttempt("alice", kClient);
            using LoginAttempt? otherUser = throttle.BeginAttempt("bob", kClient);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Not.Null);
                Assert.That(refused, Is.Null, "concurrent guesses must not exceed the failure limit");
                Assert.That(otherUser, Is.Not.Null);
            });
            first!.Dispose();
            first.Dispose();
            Assert.That(Admits(throttle, "alice", kClient), Is.True, "an ended attempt frees its place once");
            using LoginAttempt? third = throttle.BeginAttempt("alice", kClient);
            Assert.That(third, Is.Not.Null);
            Assert.That(Admits(throttle, "alice", kClient), Is.False);
        }

        [Test]
        public void BeginAttempt_FailureOfAnEndedAttemptStaysCounted()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 100, userFailures: 1);

            using (LoginAttempt attempt = throttle.BeginAttempt("alice", kClient)!)
            {
                attempt.RecordFailure();
            }

            Assert.That(Admits(throttle, "alice", kClient), Is.False);
        }

        [Test]
        public void RecordFailure_IgnoresAnonymousRequests()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 10, userFailures: 1);

            Fail(throttle, null, kClient);
            Fail(throttle, "", kClient);

            Assert.That(Admits(throttle, "alice", kClient), Is.True);
        }

        [Test]
        public void ResolveHosts_ParsesLiteralsSkipsBlanksAndNormalizes()
        {
            List<IPAddress> addresses = LoginThrottleSettings.ResolveHosts(kTrustedHosts);

            Assert.That(addresses, Is.EquivalentTo(new List<IPAddress> { kUiServer, IPAddress.Parse("198.51.100.6") }));
        }

        [Test]
        public void ResolveHosts_SkipsUnresolvableHosts()
        {
            Assert.That(LoginThrottleSettings.ResolveHosts(kUnresolvableHosts), Is.Empty);
        }

        [Test]
        public void FromConfigFile_UsesDefaultsAndTrustsLoopback()
        {
            LoginThrottleSettings settings = LoginThrottleSettings.FromConfigFile();

            Assert.Multiple(() =>
            {
                Assert.That(settings.MaxDirectories, Is.EqualTo(LoginThrottleSettings.kDefaultMaxDirectories));
                Assert.That(settings.ClientAttemptsPerMinute, Is.EqualTo(LoginThrottleSettings.kDefaultClientAttemptsPerMinute));
                Assert.That(settings.UserFailuresPerMinute, Is.EqualTo(LoginThrottleSettings.kDefaultUserFailuresPerMinute));
                Assert.That(settings.TrustedClientAddresses, Does.Contain(IPAddress.Loopback));
                Assert.That(settings.TrustedClientAddresses, Does.Contain(IPAddress.IPv6Loopback));
            });
        }

        /// <summary>
        /// Begins an attempt and ends it at once, like a login that did not fail.
        /// </summary>
        private static bool Admits(LoginThrottle throttle, string? userName, IPAddress? client)
        {
            using LoginAttempt? attempt = throttle.BeginAttempt(userName, client);
            return attempt != null;
        }

        /// <summary>
        /// Begins an attempt that fails.
        /// </summary>
        private static void Fail(LoginThrottle throttle, string? userName, IPAddress? client)
        {
            using LoginAttempt attempt = throttle.BeginAttempt(userName, client) ?? throw new AssertionException("attempt refused");
            attempt.RecordFailure();
        }

        private static LoginThrottle CreateThrottle(int clientAttempts, int userFailures, List<IPAddress>? trusted = null, TimeSpan? window = null)
        {
            LoginThrottleSettings settings = new()
            {
                ClientAttemptsPerMinute = clientAttempts,
                UserFailuresPerMinute = userFailures,
                TrustedClientAddresses = trusted ?? []
            };
            return new LoginThrottle(settings, window ?? TimeSpan.FromMinutes(1));
        }
    }
}

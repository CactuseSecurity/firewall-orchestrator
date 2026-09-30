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
        public void TryBeginAttempt_NeverLimitsAnonymousRequests()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 1);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(throttle.TryBeginAttempt(null, kClient), Is.True);
                Assert.That(throttle.TryBeginAttempt("", kClient), Is.True);
            }
        }

        [Test]
        public void TryBeginAttempt_LimitsCredentialedAttemptsPerClient()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 2, userFailures: 10);

            Assert.That(throttle.TryBeginAttempt("alice", kClient), Is.True);
            Assert.That(throttle.TryBeginAttempt("bob", kClient), Is.True);
            Assert.That(throttle.TryBeginAttempt("carol", kClient), Is.False);
            Assert.That(throttle.TryBeginAttempt("carol", kOtherClient), Is.True);
        }

        [Test]
        public void TryBeginAttempt_TreatsIpv4MappedAddressAsSameClient()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 10);

            Assert.That(throttle.TryBeginAttempt("alice", kClient), Is.True);
            Assert.That(throttle.TryBeginAttempt("alice", kClient.MapToIPv6()), Is.False);
        }

        [Test]
        public void TryBeginAttempt_ExemptsTrustedClientsFromClientLimit()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 10, trusted: kTrustedUiServer);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(throttle.TryBeginAttempt($"user{attempt}", kUiServer), Is.True);
            }
        }

        [Test]
        public void TryBeginAttempt_BlocksUserAfterFailuresFromSameClientOnly()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 100, userFailures: 2, trusted: kTrustedUiServer);

            throttle.RecordFailure("Alice", kUiServer);
            Assert.That(throttle.TryBeginAttempt("alice", kUiServer), Is.True);
            throttle.RecordFailure(" alice ", kUiServer);

            Assert.That(throttle.TryBeginAttempt("ALICE", kUiServer), Is.False, "trusted clients stay subject to the user limit");
            Assert.That(throttle.TryBeginAttempt("alice", kClient), Is.True);
            Assert.That(throttle.TryBeginAttempt("bob", kUiServer), Is.True);
        }

        [Test]
        public void TryBeginAttempt_SuccessfulAttemptsDoNotCountAsFailures()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 100, userFailures: 1);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(throttle.TryBeginAttempt("alice", kClient), Is.True);
            }
        }

        [Test]
        public async Task TryBeginAttempt_AllowsAttemptsAgainAfterWindow()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 1, window: TimeSpan.FromMilliseconds(100));
            throttle.RecordFailure("alice", kClient);
            Assert.That(throttle.TryBeginAttempt("alice", kClient), Is.False);

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            while (!throttle.TryBeginAttempt("alice", kClient))
            {
                await Task.Delay(50, timeout.Token);
            }
        }

        [Test]
        public void TryBeginAttempt_GroupsUnknownClients()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 1, userFailures: 10);

            Assert.That(throttle.TryBeginAttempt("alice", null), Is.True);
            Assert.That(throttle.TryBeginAttempt("bob", null), Is.False);
        }

        [Test]
        public void RecordFailure_IgnoresAnonymousRequests()
        {
            using LoginThrottle throttle = CreateThrottle(clientAttempts: 10, userFailures: 1);

            throttle.RecordFailure(null, kClient);
            throttle.RecordFailure("", kClient);

            Assert.That(throttle.TryBeginAttempt("alice", kClient), Is.True);
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

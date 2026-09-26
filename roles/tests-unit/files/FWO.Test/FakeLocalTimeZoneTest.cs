using NUnit.Framework;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Test
{
    /// <summary>
    /// Tests the scope and the test attribute that temporarily put the system time zone back
    /// while the test run fakes the managed local time zone.
    /// </summary>
    [TestFixture]
    [NonParallelizable] // replaces the process-wide local time zone
    internal class FakeLocalTimeZoneTest
    {
        private const string kFakedTimeZoneId = "Pacific/Auckland";
        private const string kFallbackFakedTimeZoneId = "America/Los_Angeles";
        private const string kDaylightSavingTimeZoneId = "Europe/Berlin";
        private const string kCertificateSubject = "CN=fwo-fake-local-time-zone-test";

        /// <summary>
        /// Summer noon, when Europe/Berlin observes daylight saving time: the verification time
        /// whose daylight saving flag glibc 2.35 rejects on a UTC host.
        /// </summary>
        private static readonly DateTime kSummerVerificationTime = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Local);
        private static readonly DateTimeOffset kCertificateNotBefore = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset kCertificateNotAfter = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

        private FakeLocalTimeZone? fakedTimeZone;

        /// <summary>
        /// Fakes a zone other than the system zone, so every assertion below can tell them apart
        /// regardless of the host zone.
        /// </summary>
        [OneTimeSetUp]
        public void FakeZone()
        {
            fakedTimeZone = FakeZoneOtherThanSystem();
        }

        /// <summary>
        /// Restores the local zone the fixture started with.
        /// </summary>
        [OneTimeTearDown]
        public void RestoreFakedZone()
        {
            fakedTimeZone?.Dispose();
        }

        /// <summary>
        /// Inside the scope the managed local zone matches the one the process started with.
        /// </summary>
        [Test]
        public void UseSystemTimeZone_SetsTheSystemTimeZoneAsLocal()
        {
            Assert.That(TimeZoneInfo.Local.Id, Is.Not.EqualTo(FakeLocalTimeZone.SystemTimeZone.Id));

            using FakeLocalTimeZone systemTimeZone = FakeLocalTimeZone.UseSystemTimeZone();

            Assert.That(TimeZoneInfo.Local.Id, Is.EqualTo(FakeLocalTimeZone.SystemTimeZone.Id));
        }

        /// <summary>
        /// Disposing the scope restores whichever local zone was active before it.
        /// </summary>
        [Test]
        public void UseSystemTimeZone_RestoresThePreviousLocalTimeZoneOnDispose()
        {
            string previousTimeZoneId = TimeZoneInfo.Local.Id;
            FakeLocalTimeZone systemTimeZone = FakeLocalTimeZone.UseSystemTimeZone();
            string timeZoneIdInScope = TimeZoneInfo.Local.Id;

            systemTimeZone.Dispose();

            // asserted only after disposal, so a failure cannot leave the system zone active
            Assert.That(timeZoneIdInScope, Is.Not.EqualTo(previousTimeZoneId));
            Assert.That(TimeZoneInfo.Local.Id, Is.EqualTo(previousTimeZoneId));
        }

        /// <summary>
        /// The attribute restores the faked zone once a test has finished, whatever its outcome.
        /// </summary>
        [Test]
        public void UseSystemTimeZoneAttribute_RestoresThePreviousLocalTimeZoneAfterTest()
        {
            string previousTimeZoneId = TimeZoneInfo.Local.Id;
            UseSystemTimeZoneAttribute attribute = new();

            attribute.BeforeTest(null!);
            string timeZoneIdInTest = TimeZoneInfo.Local.Id;
            attribute.AfterTest(null!);

            // asserted only after AfterTest, so a failure cannot leave the system zone active
            Assert.That(timeZoneIdInTest, Is.EqualTo(FakeLocalTimeZone.SystemTimeZone.Id));
            Assert.That(TimeZoneInfo.Local.Id, Is.EqualTo(previousTimeZoneId));
        }

        /// <summary>
        /// Regression test for Ubuntu 22.04: with a daylight saving zone faked, building a chain
        /// for a summer verification time must succeed inside the scope.
        /// </summary>
        [Test]
        public void UseSystemTimeZone_LetsAChainBuildWhileADaylightSavingZoneIsFaked()
        {
            using FakeLocalTimeZone daylightSavingTimeZone = new(TimeZoneInfo.FindSystemTimeZoneById(kDaylightSavingTimeZoneId));
            using X509Certificate2 certificate = CreateSelfSignedCertificate();
            using FakeLocalTimeZone systemTimeZone = FakeLocalTimeZone.UseSystemTimeZone();
            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(certificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationTime = kSummerVerificationTime;

            Assert.That(chain.Build(certificate), Is.True);
        }

        /// <summary>
        /// Creates a self-signed authority valid around <see cref="kSummerVerificationTime"/>.
        /// </summary>
        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using ECDsa key = ECDsa.Create();
            CertificateRequest request = new(kCertificateSubject, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            return request.CreateSelfSigned(kCertificateNotBefore, kCertificateNotAfter);
        }

        /// <summary>
        /// Fakes a local zone that differs from the system zone.
        /// </summary>
        /// <returns>The scope restoring the previously active local time zone on disposal.</returns>
        private static FakeLocalTimeZone FakeZoneOtherThanSystem()
        {
            string fakedTimeZoneId = FakeLocalTimeZone.SystemTimeZone.Id == kFakedTimeZoneId ? kFallbackFakedTimeZoneId : kFakedTimeZoneId;
            return new FakeLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById(fakedTimeZoneId));
        }

        /// <summary>
        /// Applies the attribute to a fixture, the way certificate fixtures use it.
        /// </summary>
        [TestFixture]
        [NonParallelizable] // replaces the process-wide local time zone
        [UseSystemTimeZone]
        internal class OnFixture
        {
            private FakeLocalTimeZone? fakedTimeZone;
            private string localTimeZoneIdInSetUp = "";

            /// <summary>
            /// Fakes a zone other than the system zone before any test action runs.
            /// </summary>
            [OneTimeSetUp]
            public void FakeZone()
            {
                fakedTimeZone = FakeZoneOtherThanSystem();
            }

            /// <summary>
            /// Restores the local zone the fixture started with.
            /// </summary>
            [OneTimeTearDown]
            public void RestoreFakedZone()
            {
                fakedTimeZone?.Dispose();
            }

            /// <summary>
            /// Records the local zone seen by SetUp.
            /// </summary>
            [SetUp]
            public void RecordLocalTimeZone()
            {
                localTimeZoneIdInSetUp = TimeZoneInfo.Local.Id;
            }

            /// <summary>
            /// On a fixture the attribute puts the system zone back before SetUp runs, so
            /// fixtures building chains in SetUp are covered as well.
            /// </summary>
            [Test]
            public void UseSystemTimeZoneAttribute_SetsTheSystemTimeZoneBeforeSetUp()
            {
                Assert.That(localTimeZoneIdInSetUp, Is.EqualTo(FakeLocalTimeZone.SystemTimeZone.Id));
                Assert.That(TimeZoneInfo.Local.Id, Is.EqualTo(FakeLocalTimeZone.SystemTimeZone.Id));
            }
        }
    }
}

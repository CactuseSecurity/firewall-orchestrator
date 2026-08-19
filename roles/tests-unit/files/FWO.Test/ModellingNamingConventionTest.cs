using FWO.Data.Modelling;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingNamingConventionTest
    {
        private const string kNamingConventionWithNulls = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5," +
            "\"networkAreaPattern\":null,\"appRolePattern\":null,\"applicationZone\":null," +
            "\"appServerPrefix\":null,\"networkPrefix\":null,\"ipRangePrefix\":null}";

        /// <summary>
        /// Verifies that stored null patterns are replaced during deserialization.
        /// </summary>
        [Test]
        public void FromJson_WithNullPatterns_ReturnsNormalizedConvention()
        {
            ModellingNamingConvention namingConvention = ModellingNamingConvention.FromJson(kNamingConventionWithNulls);

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.NetworkAreaPattern, Is.Empty);
                Assert.That(namingConvention.AppRolePattern, Is.Empty);
                Assert.That(namingConvention.AppZone, Is.Empty);
                Assert.That(namingConvention.AppServerPrefix, Is.Empty);
                Assert.That(namingConvention.NetworkPrefix, Is.Empty);
                Assert.That(namingConvention.IpRangePrefix, Is.Empty);
                Assert.That(namingConvention.NetworkAreaRequired, Is.True);
                Assert.That(namingConvention.FixedPartLength, Is.EqualTo(4));
                Assert.That(namingConvention.FreePartLength, Is.EqualTo(5));
            });
        }

        /// <summary>
        /// Verifies that a missing or blank config value results in a default convention.
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void FromJson_WithoutContent_ReturnsDefaultConvention(string? json)
        {
            ModellingNamingConvention namingConvention = ModellingNamingConvention.FromJson(json);

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.NetworkAreaPattern, Is.Empty);
                Assert.That(namingConvention.AppRolePattern, Is.Empty);
                Assert.That(namingConvention.FixedPartLength, Is.Zero);
            });
        }

        /// <summary>
        /// Verifies that a stored json null value results in a default convention.
        /// </summary>
        [Test]
        public void FromJson_WithJsonNull_ReturnsDefaultConvention()
        {
            ModellingNamingConvention namingConvention = ModellingNamingConvention.FromJson("null");

            ClassicAssert.AreEqual("", namingConvention.AppRolePattern);
        }

        /// <summary>
        /// Verifies that the settings are preserved for a valid convention.
        /// </summary>
        [Test]
        public void FromJson_WithCompleteConvention_KeepsValues()
        {
            ModellingNamingConvention namingConvention = ModellingNamingConvention.FromJson(
                "{\"networkAreaRequired\":true,\"useAppPart\":true,\"fixedPartLength\":4,\"freePartLength\":5," +
                "\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\",\"applicationZone\":\"AZ\"}");

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.NetworkAreaPattern, Is.EqualTo("NA"));
                Assert.That(namingConvention.AppRolePattern, Is.EqualTo("AR"));
                Assert.That(namingConvention.AppZone, Is.EqualTo("AZ"));
                Assert.That(namingConvention.UseAppPart, Is.True);
                Assert.That(namingConvention.IsFixedPartLengthValid(), Is.True);
            });
        }

        /// <summary>
        /// Verifies that negative lengths of a hand edited config are repaired.
        /// </summary>
        [Test]
        public void Normalize_WithNegativeLengths_ResetsThemToZero()
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = -3,
                FreePartLength = -1
            };

            namingConvention.Normalize();

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.FixedPartLength, Is.Zero);
                Assert.That(namingConvention.FreePartLength, Is.Zero);
            });
        }

        /// <summary>
        /// Verifies that a fixed part shorter than the network area pattern is detected.
        /// </summary>
        [TestCase(0, "", true)]
        [TestCase(0, "NA", false)]
        [TestCase(1, "NA", false)]
        [TestCase(2, "NA", true)]
        [TestCase(4, "NA", true)]
        public void IsFixedPartLengthValid_ChecksNetworkAreaPattern(int fixedPartLength, string networkAreaPattern, bool expectedResult)
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = fixedPartLength,
                NetworkAreaPattern = networkAreaPattern
            };

            ClassicAssert.AreEqual(expectedResult, namingConvention.IsFixedPartLengthValid());
        }

        /// <summary>
        /// Verifies that a null network area pattern does not break the validity check.
        /// </summary>
        [Test]
        public void IsFixedPartLengthValid_WithNullPattern_ReturnsTrue()
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = 0,
                NetworkAreaPattern = null!
            };

            ClassicAssert.IsTrue(namingConvention.IsFixedPartLengthValid());
        }
    }
}

using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data.Modelling;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ManagedIdStringTest
    {
        ModellingManagedIdString IdString1 = new();
        ModellingManagedIdString IdString2 = new("AR5001234-123");

        static readonly ModellingNamingConvention NamingConvention1 = new()
        {
            NetworkAreaRequired = true,
            UseAppPart = false,
            FixedPartLength = 2,
            FreePartLength = 5,
            NetworkAreaPattern = "NA",
            AppRolePattern = "AR"
        };
        static readonly ModellingNamingConvention NamingConvention2 = new()
        {
            NetworkAreaRequired = true,
            UseAppPart = true,
            FixedPartLength = 4,
            FreePartLength = 3,
            NetworkAreaPattern = "NA",
            AppRolePattern = "AR"
        };
        ModellingNamingConvention NamingConvention3 = new()
        {
            NetworkAreaRequired = true,
            UseAppPart = true,
            FixedPartLength = 4,
            FreePartLength = 3,
            NetworkAreaPattern = "",
            AppRolePattern = "A"
        };


        [Test]
        public void TestManagedIdStringStartEmpty()
        {
            ClassicAssert.AreEqual("", IdString1.Whole);
            ClassicAssert.AreEqual("", IdString1.FixedPart);
            ClassicAssert.AreEqual("", IdString1.AppPart);
            ClassicAssert.AreEqual("", IdString1.FreePart);
            ClassicAssert.AreEqual("", IdString1.CombinedFixPart);

            IdString1.SetAppPartFromExtId("APP-0001");
            ClassicAssert.AreEqual("", IdString1.Whole);
            ClassicAssert.AreEqual("", IdString1.FixedPart);
            ClassicAssert.AreEqual("", IdString1.AppPart);
            ClassicAssert.AreEqual("", IdString1.Separator);
            ClassicAssert.AreEqual("", IdString1.FreePart);
            ClassicAssert.AreEqual("", IdString1.CombinedFixPart);

            IdString1.NamingConvention = NamingConvention2;
            IdString1.SetAppPartFromExtId("APP-0001");
            ClassicAssert.AreEqual("    00001-", IdString1.Whole);
            ClassicAssert.AreEqual("    ", IdString1.FixedPart);
            ClassicAssert.AreEqual("00001-", IdString1.AppPart);
            ClassicAssert.AreEqual("-", IdString1.Separator);
            ClassicAssert.AreEqual("", IdString1.FreePart);
            ClassicAssert.AreEqual("    00001", IdString1.CombinedFixPart);

            IdString1.FixedPart = "x";
            ClassicAssert.AreEqual("x???00001-", IdString1.Whole);
            ClassicAssert.AreEqual("x???", IdString1.FixedPart);
            ClassicAssert.AreEqual("00001-", IdString1.AppPart);
            ClassicAssert.AreEqual("-", IdString1.Separator);
            ClassicAssert.AreEqual("", IdString1.FreePart);
            ClassicAssert.AreEqual("x???00001", IdString1.CombinedFixPart);

            IdString1.FixedPart = "muchlonger";
            ClassicAssert.AreEqual("much00001-", IdString1.Whole);
            ClassicAssert.AreEqual("much", IdString1.FixedPart);
            ClassicAssert.AreEqual("00001-", IdString1.AppPart);
            ClassicAssert.AreEqual("-", IdString1.Separator);
            ClassicAssert.AreEqual("", IdString1.FreePart);
            ClassicAssert.AreEqual("much00001", IdString1.CombinedFixPart);
        }

        [Test]
        public void TestManagedIdStringPrefilled()
        {
            ClassicAssert.AreEqual("AR5001234-123", IdString2.Whole);
            ClassicAssert.AreEqual("", IdString2.FixedPart);
            ClassicAssert.AreEqual("", IdString2.AppPart);
            ClassicAssert.AreEqual("", IdString2.Separator);
            ClassicAssert.AreEqual("AR5001234-123", IdString2.FreePart);
            ClassicAssert.AreEqual("", IdString2.CombinedFixPart);

            IdString2.NamingConvention = NamingConvention1;
            ClassicAssert.AreEqual("AR5001234-123", IdString2.Whole);
            ClassicAssert.AreEqual("AR", IdString2.FixedPart);
            ClassicAssert.AreEqual("", IdString2.AppPart);
            ClassicAssert.AreEqual("", IdString2.Separator);
            ClassicAssert.AreEqual("5001234-123", IdString2.FreePart);
            ClassicAssert.AreEqual("AR", IdString2.CombinedFixPart);

            IdString2.NamingConvention = NamingConvention2;
            ClassicAssert.AreEqual("AR5001234-123", IdString2.Whole);
            ClassicAssert.AreEqual("AR50", IdString2.FixedPart);
            ClassicAssert.AreEqual("01234-", IdString2.AppPart);
            ClassicAssert.AreEqual("-", IdString2.Separator);
            ClassicAssert.AreEqual("123", IdString2.FreePart);
            ClassicAssert.AreEqual("AR5001234", IdString2.CombinedFixPart);

            IdString2.SetAppPartFromExtId("COM-99999");
            ClassicAssert.AreEqual("AR50199999-123", IdString2.Whole);
            ClassicAssert.AreEqual("AR50", IdString2.FixedPart);
            ClassicAssert.AreEqual("199999-", IdString2.AppPart);
            ClassicAssert.AreEqual("-", IdString2.Separator);
            ClassicAssert.AreEqual("123", IdString2.FreePart);
            ClassicAssert.AreEqual("AR50199999", IdString2.CombinedFixPart);

            IdString2.NamingConvention = new();
            ClassicAssert.AreEqual("AR50199999-123", IdString2.Whole);
            ClassicAssert.AreEqual("", IdString2.FixedPart);
            ClassicAssert.AreEqual("", IdString2.AppPart);
            ClassicAssert.AreEqual("", IdString2.Separator);
            ClassicAssert.AreEqual("AR50199999-123", IdString2.FreePart);
            ClassicAssert.AreEqual("", IdString2.CombinedFixPart);
        }

        [Test]
        public void TestReconstructAreaIdString()
        {
            ClassicAssert.AreEqual("NA", ModellingManagedIdString.ConvertAppRoleToArea("AR5000001", NamingConvention1));
            ClassicAssert.AreEqual("NA91", ModellingManagedIdString.ConvertAppRoleToArea("AR9104106-001", NamingConvention2));
            ClassicAssert.AreEqual("R91", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
            NamingConvention3.NetworkAreaPattern = "XYZ";
            ClassicAssert.AreEqual("XYZR91", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
            NamingConvention3.AppRolePattern = "AR91";
            ClassicAssert.AreEqual("XYZ", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
            NamingConvention3.AppRolePattern = "AR91123";
            ClassicAssert.AreEqual("XYZ", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
        }

        /// <summary>
        /// Verifies that a stored null network area pattern is treated as an empty pattern.
        /// </summary>
        [Test]
        public void TestConvertAreaToAppRoleWithNullPattern()
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = 4,
                NetworkAreaPattern = null!,
                AppRolePattern = "AR"
            };

            ClassicAssert.AreEqual("ARNA12", ModellingManagedIdString.ConvertAreaToAppRole("NA1234", namingConvention));
        }

        /// <summary>
        /// Verifies that converting a short area returns it unchanged.
        /// </summary>
        [Test]
        public void TestConvertShortAreaToAppRole()
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = 4,
                NetworkAreaPattern = "NA",
                AppRolePattern = "AR"
            };

            ClassicAssert.AreEqual("NA", ModellingManagedIdString.ConvertAreaToAppRole("NA", namingConvention));
        }

        /// <summary>
        /// Verifies that converting an app role back to an area tolerates null patterns of an older config.
        /// </summary>
        [Test]
        public void TestConvertAppRoleToAreaWithNullPatterns()
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = 4,
                NetworkAreaPattern = null!,
                AppRolePattern = null!
            };

            ClassicAssert.AreEqual("AR12", ModellingManagedIdString.ConvertAppRoleToArea("AR1234-001", namingConvention));
        }

        /// <summary>
        /// Verifies that a fixed part shorter than the app role pattern does not break the area conversion.
        /// </summary>
        [Test]
        public void TestConvertAppRoleToAreaWithShortFixedPart()
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = 1,
                NetworkAreaPattern = "NA",
                AppRolePattern = "AR"
            };

            ClassicAssert.AreEqual("NA", ModellingManagedIdString.ConvertAppRoleToArea("AR1234-001", namingConvention));
        }

        /// <summary>
        /// Verifies conversion for both clamped and regular network area patterns.
        /// </summary>
        [TestCase(1, "NA", "NA", "AR")]
        [TestCase(4, "NA", "NA12", "AR12")]
        public void TestConvertAreaToAppRole(int fixedPartLength, string networkAreaPattern, string areaIdString, string expectedAppRole)
        {
            ModellingNamingConvention namingConvention = new()
            {
                FixedPartLength = fixedPartLength,
                NetworkAreaPattern = networkAreaPattern,
                AppRolePattern = "AR"
            };

            ClassicAssert.AreEqual(expectedAppRole, ModellingManagedIdString.ConvertAreaToAppRole(areaIdString, namingConvention));
        }

        /// <summary>
        /// Verifies that every convention accepted by the validation converts an area into an app role identifier
        /// and back without losing the area specific content. A pattern shorter than the network area pattern
        /// would leave the fixed part too short, so that it is padded with a filler and no longer maps back.
        /// </summary>
        [TestCase(4, "NA", "AR", "NA12")]
        [TestCase(5, "NET", "ARO", "NET12")]
        [TestCase(3, "NA", "AR", "NA1")]
        public void TestAreaToAppRoleRoundTrip(int fixedPartLength, string networkAreaPattern, string appRolePattern, string areaIdString)
        {
            ModellingNamingConvention namingConvention = new()
            {
                NetworkAreaRequired = true,
                FixedPartLength = fixedPartLength,
                NetworkAreaPattern = networkAreaPattern,
                AppRolePattern = appRolePattern
            };
            ModellingManagedIdString managedIdString = new() { NamingConvention = namingConvention };

            managedIdString.ConvertAreaToAppRoleFixedPart(areaIdString);

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.IsAreaConversionValid(), Is.True);
                Assert.That(managedIdString.Whole, Does.Not.Contain("?"));
                Assert.That(ModellingManagedIdString.ConvertAppRoleToArea(managedIdString.Whole + "-00001", namingConvention),
                    Is.EqualTo(areaIdString));
            });
        }

        /// <summary>
        /// Verifies that a fixed part consisting of the network area pattern alone is rejected, as it converts
        /// every area into the same app role fixed part and cannot be mapped back to the area it came from.
        /// </summary>
        [Test]
        public void TestFixedPartWithoutAreaContentIsRejected()
        {
            ModellingNamingConvention namingConvention = new()
            {
                NetworkAreaRequired = true,
                FixedPartLength = 2,
                NetworkAreaPattern = "NA",
                AppRolePattern = "AR"
            };
            ModellingManagedIdString firstArea = new() { NamingConvention = namingConvention };
            ModellingManagedIdString secondArea = new() { NamingConvention = namingConvention };

            firstArea.ConvertAreaToAppRoleFixedPart("NA1234");
            secondArea.ConvertAreaToAppRoleFixedPart("NA5678");

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.IsAreaConversionValid(), Is.False);
                Assert.That(firstArea.Whole, Is.EqualTo("AR"));
                Assert.That(secondArea.Whole, Is.EqualTo(firstArea.Whole));
                Assert.That(ModellingManagedIdString.ConvertAppRoleToArea(firstArea.Whole + "-00001", namingConvention),
                    Is.EqualTo("NA"));
            });
        }

        /// <summary>
        /// Verifies that a convention rejected by the validation is exactly the one that would pad the fixed part
        /// with a filler, which is what breaks the way back to the area.
        /// </summary>
        [Test]
        public void TestShorterAppRolePatternIsRejected()
        {
            ModellingNamingConvention namingConvention = new()
            {
                NetworkAreaRequired = true,
                FixedPartLength = 5,
                NetworkAreaPattern = "NET",
                AppRolePattern = "AR"
            };
            ModellingManagedIdString managedIdString = new() { NamingConvention = namingConvention };

            managedIdString.ConvertAreaToAppRoleFixedPart("NET12");

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.IsAreaConversionValid(), Is.False);
                Assert.That(managedIdString.Whole, Is.EqualTo("AR12?"));
                Assert.That(ModellingManagedIdString.ConvertAppRoleToArea(managedIdString.Whole + "-00001", namingConvention),
                    Is.EqualTo("NET12?"));
            });
        }
    }
}

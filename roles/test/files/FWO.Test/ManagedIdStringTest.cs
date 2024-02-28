using NUnit.Framework;
using FWO.Api.Data;

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
            NetworkAreaRequired = true, UseAppPart = false, FixedPartLength = 2, FreePartLength = 5, NetworkAreaPattern = "NA", AppRolePattern = "AR"
        };
        static readonly ModellingNamingConvention NamingConvention2 = new()
        {
            NetworkAreaRequired = true, UseAppPart = true, FixedPartLength = 4, FreePartLength = 3, NetworkAreaPattern = "NA", AppRolePattern = "AR"
        };
        ModellingNamingConvention NamingConvention3 = new()
        {
            NetworkAreaRequired = true, UseAppPart = true, FixedPartLength = 4, FreePartLength = 3, NetworkAreaPattern = "", AppRolePattern = "A"
        };


        [Test]
        public void TestManagedIdStringStartEmpty()
        {
            Assert.AreEqual("", IdString1.Whole);
            Assert.AreEqual("", IdString1.FixedPart);
            Assert.AreEqual("", IdString1.AppPart);
            Assert.AreEqual("", IdString1.FreePart);
            Assert.AreEqual("", IdString1.CombinedFixPart);
            
            IdString1.SetAppPartFromExtId("APP-0001");
            Assert.AreEqual("", IdString1.Whole);
            Assert.AreEqual("", IdString1.FixedPart);
            Assert.AreEqual("", IdString1.AppPart);
            Assert.AreEqual("", IdString1.Separator);
            Assert.AreEqual("", IdString1.FreePart);
            Assert.AreEqual("", IdString1.CombinedFixPart);

            IdString1.NamingConvention = NamingConvention2;
            IdString1.SetAppPartFromExtId("APP-0001");
            Assert.AreEqual("    00001-", IdString1.Whole);
            Assert.AreEqual("    ", IdString1.FixedPart);
            Assert.AreEqual("00001-", IdString1.AppPart);
            Assert.AreEqual("-", IdString1.Separator);
            Assert.AreEqual("", IdString1.FreePart);
            Assert.AreEqual("    00001", IdString1.CombinedFixPart);

            IdString1.FixedPart = "x";
            Assert.AreEqual("x???00001-", IdString1.Whole);
            Assert.AreEqual("x???", IdString1.FixedPart);
            Assert.AreEqual("00001-", IdString1.AppPart);
            Assert.AreEqual("-", IdString1.Separator);
            Assert.AreEqual("", IdString1.FreePart);
            Assert.AreEqual("x???00001", IdString1.CombinedFixPart);

            IdString1.FixedPart = "muchlonger";
            Assert.AreEqual("much00001-", IdString1.Whole);
            Assert.AreEqual("much", IdString1.FixedPart);
            Assert.AreEqual("00001-", IdString1.AppPart);
            Assert.AreEqual("-", IdString1.Separator);
            Assert.AreEqual("", IdString1.FreePart);
            Assert.AreEqual("much00001", IdString1.CombinedFixPart);
        }

        [Test]
        public void TestManagedIdStringPrefilled()
        {
            Assert.AreEqual("AR5001234-123", IdString2.Whole);
            Assert.AreEqual("", IdString2.FixedPart);
            Assert.AreEqual("", IdString2.AppPart);
            Assert.AreEqual("", IdString2.Separator);
            Assert.AreEqual("AR5001234-123", IdString2.FreePart);
            Assert.AreEqual("", IdString2.CombinedFixPart);

            IdString2.NamingConvention = NamingConvention1;
            Assert.AreEqual("AR5001234-123", IdString2.Whole);
            Assert.AreEqual("AR", IdString2.FixedPart);
            Assert.AreEqual("", IdString2.AppPart);
            Assert.AreEqual("", IdString2.Separator);
            Assert.AreEqual("5001234-123", IdString2.FreePart);
            Assert.AreEqual("AR", IdString2.CombinedFixPart);

            IdString2.NamingConvention = NamingConvention2;
            Assert.AreEqual("AR5001234-123", IdString2.Whole);
            Assert.AreEqual("AR50", IdString2.FixedPart);
            Assert.AreEqual("01234-", IdString2.AppPart);
            Assert.AreEqual("-", IdString2.Separator);
            Assert.AreEqual("123", IdString2.FreePart);
            Assert.AreEqual("AR5001234", IdString2.CombinedFixPart);

            IdString2.SetAppPartFromExtId("COM-99999");
            Assert.AreEqual("AR50199999-123", IdString2.Whole);
            Assert.AreEqual("AR50", IdString2.FixedPart);
            Assert.AreEqual("199999-", IdString2.AppPart);
            Assert.AreEqual("-", IdString2.Separator);
            Assert.AreEqual("123", IdString2.FreePart);
            Assert.AreEqual("AR50199999", IdString2.CombinedFixPart);

            IdString2.NamingConvention =  new();
            Assert.AreEqual("AR50199999-123", IdString2.Whole);
            Assert.AreEqual("", IdString2.FixedPart);
            Assert.AreEqual("", IdString2.AppPart);
            Assert.AreEqual("", IdString2.Separator);
            Assert.AreEqual("AR50199999-123", IdString2.FreePart);
            Assert.AreEqual("", IdString2.CombinedFixPart);
        }

        [Test]
        public void TestReconstructAreaIdString()
        {
            Assert.AreEqual("NA", ModellingManagedIdString.ConvertAppRoleToArea("AR5000001", NamingConvention1));
            Assert.AreEqual("NA91", ModellingManagedIdString.ConvertAppRoleToArea("AR9104106-001", NamingConvention2));
            Assert.AreEqual("R91", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
            NamingConvention3.NetworkAreaPattern = "XYZ";
            Assert.AreEqual("XYZR91", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
            NamingConvention3.AppRolePattern = "AR91";
            Assert.AreEqual("XYZ", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
            NamingConvention3.AppRolePattern = "AR91123";
            Assert.AreEqual("XYZ", ModellingManagedIdString.ConvertAppRoleToArea("AR9112345-001", NamingConvention3));
        }
    }
}

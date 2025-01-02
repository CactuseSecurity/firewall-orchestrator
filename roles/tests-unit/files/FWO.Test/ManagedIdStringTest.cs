using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
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

            IdString2.NamingConvention =  new();
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
    }
}

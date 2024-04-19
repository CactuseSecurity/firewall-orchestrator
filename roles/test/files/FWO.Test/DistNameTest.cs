using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.GlobalConstants;
using FWO.Api.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DistNameTest
    {

        static readonly DistName dn1 = new("");
        static readonly DistName dn2 = new("uid=intuser2,ou=users,ou=tenant2,dc=fworch,dc=internal");
        static readonly DistName dn3 = new("cn=usergroup3,ou=groups,dc=somewhere,dc=de");

        [SetUp]
        public void Initialize()
        {}

        [Test]
        public void TestDistName()
        {
            ClassicAssert.AreEqual("", dn1.UserName);
            ClassicAssert.AreEqual("", dn1.Role);
            ClassicAssert.AreEqual("", dn1.Group);
            ClassicAssert.AreEqual(0, dn1.Root.Count);
            ClassicAssert.AreEqual(0, dn1.Path.Count);
            ClassicAssert.AreEqual("", dn1.getTenant());
            ClassicAssert.AreEqual(false, dn1.IsInternal());

            ClassicAssert.AreEqual("intuser2", dn2.UserName);
            ClassicAssert.AreEqual("", dn2.Role);
            ClassicAssert.AreEqual("", dn2.Group);
            ClassicAssert.AreEqual(2, dn2.Root.Count);
            ClassicAssert.AreEqual(4, dn2.Path.Count);
            ClassicAssert.AreEqual("tenant2", dn2.getTenant(3));
            ClassicAssert.AreEqual(true, dn2.IsInternal());

            ClassicAssert.AreEqual("usergroup3", dn3.UserName);
            ClassicAssert.AreEqual("usergroup3", dn3.Role);
            ClassicAssert.AreEqual("usergroup3", dn3.Group);
            ClassicAssert.AreEqual(2, dn3.Root.Count);
            ClassicAssert.AreEqual("somewhere", dn3.Root[0]);
            ClassicAssert.AreEqual(3, dn3.Path.Count);
            ClassicAssert.AreEqual("groups", dn3.Path[0]);
            ClassicAssert.AreEqual("", dn3.getTenant(0));
            ClassicAssert.AreEqual(false, dn3.IsInternal());
        }
    }
}

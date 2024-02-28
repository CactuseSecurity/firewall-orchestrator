using NUnit.Framework;
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
            Assert.AreEqual("", dn1.UserName);
            Assert.AreEqual("", dn1.Role);
            Assert.AreEqual("", dn1.Group);
            Assert.AreEqual(0, dn1.Root.Count);
            Assert.AreEqual(0, dn1.Path.Count);
            Assert.AreEqual("", dn1.getTenant());
            Assert.AreEqual(false, dn1.IsInternal());

            Assert.AreEqual("intuser2", dn2.UserName);
            Assert.AreEqual("", dn2.Role);
            Assert.AreEqual("", dn2.Group);
            Assert.AreEqual(2, dn2.Root.Count);
            Assert.AreEqual(4, dn2.Path.Count);
            Assert.AreEqual("tenant2", dn2.getTenant(3));
            Assert.AreEqual(true, dn2.IsInternal());

            Assert.AreEqual("usergroup3", dn3.UserName);
            Assert.AreEqual("usergroup3", dn3.Role);
            Assert.AreEqual("usergroup3", dn3.Group);
            Assert.AreEqual(2, dn3.Root.Count);
            Assert.AreEqual("somewhere", dn3.Root[0]);
            Assert.AreEqual(3, dn3.Path.Count);
            Assert.AreEqual("groups", dn3.Path[0]);
            Assert.AreEqual("", dn3.getTenant(0));
            Assert.AreEqual(false, dn3.IsInternal());
        }
    }
}

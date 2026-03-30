using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DistNameTest
    {

        static readonly DistName dn1 = new("");
        static readonly DistName dn2 = new("uid=intuser2,ou=users,ou=tenant2,dc=fworch,dc=internal");
        static readonly DistName dn3 = new("cn=usergroup3,ou=groups,dc=somewhere,dc=de");
        static readonly DistName dn4 = new(@"cn=Mustermann\, Max,ou=users,dc=example,dc=com");
        static readonly DistName dn5 = new(@"cn=Mustermann\2C\20Max,ou=users,dc=example,dc=com");
        static readonly DistName dn6 = new(@"cn=Backslash\5CName,ou=users,dc=example,dc=com");
        static readonly DistName dn7 = new(@"cn=M\C3\BCller,ou=users,dc=example,dc=com");

        [SetUp]
        public void Initialize()
        { }

        [Test]
        public void TestDistName()
        {
            ClassicAssert.AreEqual("", dn1.UserName);
            ClassicAssert.AreEqual("", dn1.Role);
            ClassicAssert.AreEqual("", dn1.Group);
            ClassicAssert.AreEqual(0, dn1.Root.Count);
            ClassicAssert.AreEqual(0, dn1.Path.Count);
            ClassicAssert.AreEqual("", dn1.GetTenantNameViaLdapTenantLevel());
            ClassicAssert.AreEqual(false, dn1.IsInternal());

            ClassicAssert.AreEqual("intuser2", dn2.UserName);
            ClassicAssert.AreEqual("", dn2.Role);
            ClassicAssert.AreEqual("", dn2.Group);
            ClassicAssert.AreEqual(2, dn2.Root.Count);
            ClassicAssert.AreEqual(4, dn2.Path.Count);
            ClassicAssert.AreEqual("tenant2", dn2.GetTenantNameViaLdapTenantLevel(3));
            ClassicAssert.AreEqual(true, dn2.IsInternal());

            ClassicAssert.AreEqual("usergroup3", dn3.UserName);
            ClassicAssert.AreEqual("usergroup3", dn3.Role);
            ClassicAssert.AreEqual("usergroup3", dn3.Group);
            ClassicAssert.AreEqual(2, dn3.Root.Count);
            ClassicAssert.AreEqual("somewhere", dn3.Root[0]);
            ClassicAssert.AreEqual(3, dn3.Path.Count);
            ClassicAssert.AreEqual("groups", dn3.Path[0]);
            ClassicAssert.AreEqual("", dn3.GetTenantNameViaLdapTenantLevel(0));
            ClassicAssert.AreEqual(false, dn3.IsInternal());

            ClassicAssert.AreEqual("Mustermann, Max", dn4.UserName);
            ClassicAssert.AreEqual("Mustermann, Max", dn4.Role);
            ClassicAssert.AreEqual("Mustermann, Max", dn4.Group);
            ClassicAssert.AreEqual(2, dn4.Root.Count);
            ClassicAssert.AreEqual("users", dn4.Path[0]);

            ClassicAssert.AreEqual("Mustermann, Max", dn5.UserName);
            ClassicAssert.AreEqual("Mustermann, Max", dn5.Role);
            ClassicAssert.AreEqual("Mustermann, Max", dn5.Group);
            ClassicAssert.AreEqual("users", dn5.Path[0]);

            ClassicAssert.AreEqual(@"Backslash\Name", dn6.UserName);
            ClassicAssert.AreEqual(@"Backslash\Name", dn6.Role);
            ClassicAssert.AreEqual(@"Backslash\Name", dn6.Group);

            ClassicAssert.AreEqual("Müller", dn7.UserName);
            ClassicAssert.AreEqual("Müller", dn7.Role);
            ClassicAssert.AreEqual("Müller", dn7.Group);
        }
    }
}

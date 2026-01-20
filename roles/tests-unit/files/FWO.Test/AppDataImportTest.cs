using System;
using System.Reflection;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class AppDataImportTest
    {
        [Test]
        public void ResolveOwnerGroupPathPrefersWritePath()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "ou=groups,dc=example,dc=com",
                GroupSearchPath = "ou=search,dc=example,dc=com"
            };

            string resolved = InvokeResolveOwnerGroupPath(ldap);

            Assert.That(resolved, Is.EqualTo("ou=groups,dc=example,dc=com"));
        }

        [Test]
        public void ResolveOwnerGroupPathFallsBackToSearchPath()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "",
                GroupSearchPath = "ou=search,dc=example,dc=com"
            };

            string resolved = InvokeResolveOwnerGroupPath(ldap);

            Assert.That(resolved, Is.EqualTo("ou=search,dc=example,dc=com"));
        }

        [Test]
        public void ResolveOwnerGroupPathThrowsWhenMissing()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "",
                GroupSearchPath = ""
            };

            Assert.That(
                () => InvokeResolveOwnerGroupPath(ldap),
                Throws.TypeOf<InvalidOperationException>());
        }

        private static string InvokeResolveOwnerGroupPath(Ldap ldap)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ResolveOwnerGroupPath",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ResolveOwnerGroupPath helper not found.");
            return (string)method.Invoke(null, [ldap])!;
        }
    }
}

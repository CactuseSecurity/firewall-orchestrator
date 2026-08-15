using System;
using System.Threading.Tasks;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LdapConnectionAdapterTest
    {
        private static readonly string[] kNoAttributes = Array.Empty<string>();
        private static readonly LdapModification[] kNoModifications = Array.Empty<LdapModification>();
        private static readonly string kUserDn = "uid=user,ou=users,dc=example,dc=com";

        [Test]
        public void BoundAndConstraintsExposeWrappedConnectionState()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.Multiple(() =>
            {
                Assert.That(adapter.Bound, Is.False);
                Assert.That(adapter.SearchConstraints, Is.InstanceOf<LdapSearchConstraints>());
                Assert.That(adapter.Constraints, Is.InstanceOf<LdapConstraints>());
            });
        }

        [Test]
        public void ConstraintsSetter_ForwardsToWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());
            LdapConstraints constraints = new()
            {
                ReferralFollowing = true
            };

            adapter.Constraints = constraints;

            Assert.That(adapter.Constraints.ReferralFollowing, Is.True);
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.DoesNotThrow(() => adapter.Dispose());
        }

        [Test]
        public async Task BindAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.BindAsync("cn=bind,dc=example,dc=com", "secret"), Throws.Exception);
        }

        [Test]
        public async Task ReadAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.ReadAsync(kUserDn), Throws.Exception);
        }

        [Test]
        public async Task SearchAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(
                async () => await adapter.SearchAsync("ou=users,dc=example,dc=com", LdapConnection.ScopeSub, "(uid=user)", kNoAttributes, false),
                Throws.Exception);
        }

        [Test]
        public async Task AddAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());
            LdapEntry entry = new("uid=user,ou=users,dc=example,dc=com", new LdapAttributeSet());

            Assert.That(async () => await adapter.AddAsync(entry), Throws.Exception);
        }

        [Test]
        public async Task DeleteAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.DeleteAsync(kUserDn), Throws.Exception);
        }

        [Test]
        public async Task ModifyAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.ModifyAsync(kUserDn, kNoModifications), Throws.Exception);
        }

        [Test]
        public async Task RenameAsync_InvokesWrappedConnection()
        {
            NovellLdapConnectionAdapter adapter = new(new LdapConnection());

            Assert.That(async () => await adapter.RenameAsync(kUserDn, "uid=user2", true), Throws.Exception);
        }
    }
}

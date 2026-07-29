using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UserGroupResolverTest
    {
        private static readonly List<Ldap> kNoLdaps = [];

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyForMissingDn()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUserDn("");

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyForWhitespaceDn()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUserDn("   ");

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyWhenNoLdapKnowsTheUser()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUserDn("uid=unknown,ou=users,dc=fworch,dc=internal");

            Assert.That(groups, Is.Empty);
        }
    }
}

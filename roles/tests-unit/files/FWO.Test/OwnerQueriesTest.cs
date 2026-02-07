using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class OwnerQueriesTest
    {
        [Test]
        public void GetOwnersForUserUsesActiveResponsibleTypes()
        {
            string query = OwnerQueries.getOwnersForUser;

            Assert.That(query, Does.Contain("owner_responsible_type: {active: {_eq: true}}"));
        }

        [Test]
        public void GetOwnerResponsibleTypesIncludesAllowWriteAccess()
        {
            string query = OwnerQueries.getOwnerResponsibleTypes;

            Assert.That(query, Does.Contain("allow_modelling"));
            Assert.That(query, Does.Contain("allow_recertification"));
        }
    }
}

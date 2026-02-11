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

        [Test]
        public void GetOwnersForDnsWithModellingUsesAllowModellingFilter()
        {
            string query = OwnerQueries.getOwnersForDnsWithModellingWithConn;

            Assert.That(query, Does.Contain("allow_modelling: {_eq: true}"));
        }

        [Test]
        public void GetOwnersForDnsWithConnUsesOnlyActiveResponsibleTypes()
        {
            string query = OwnerQueries.getOwnersForDnsWithConn;

            Assert.That(query, Does.Contain("owner_responsible_type: {"));
            Assert.That(query, Does.Contain("active: {_eq: true}"));
            Assert.That(query, Does.Not.Contain("allow_modelling: {_eq: true}"));
            Assert.That(query, Does.Not.Contain("allow_recertification: {_eq: true}"));
        }

        [Test]
        public void GetOwnersForDnsWithRecertificationUsesAllowRecertificationFilter()
        {
            string query = OwnerQueries.getOwnersForDnsWithRecertification;

            Assert.That(query, Does.Contain("allow_recertification: {_eq: true}"));
        }
    }
}

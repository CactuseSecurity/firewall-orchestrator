using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class OwnerQueriesTest
    {
        [Test]
        public void GetOwnersForUserIncludesSupportingResponsible()
        {
            string query = OwnerQueries.getOwnersForUser;

            Assert.That(query, Does.Contain("responsible_type: {_in: [1, 2]}"));
            Assert.That(query, Does.Not.Contain("responsible_type: {_in: [1, 3]}"));
        }
    }
}

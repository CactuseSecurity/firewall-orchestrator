using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ModellingAppHandlerRequestTicketTest
    {
        [Test]
        public async Task GetLatestFWRequestTicket_ReturnsLatestTicketWhenAvailable()
        {
            RequestFwChangePopupTestApiConn apiConn = new()
            {
                LatestTicket = new WfTicket { Id = 77 }
            };

            WfTicket? ticket = await ModellingAppHandler.GetLatestFWRequestTicket(new FwoOwner { Id = 7 }, apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(ticket, Is.Not.Null);
                Assert.That(ticket!.Id, Is.EqualTo(77));
                Assert.That(apiConn.Queries, Does.Contain(ExtRequestQueries.getLatestTicketId));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getTicketById));
            });
        }

        [Test]
        public async Task GetLatestFWRequestTicket_ReturnsNullWhenNoTicketExists()
        {
            RequestFwChangePopupTestApiConn apiConn = new();

            WfTicket? ticket = await ModellingAppHandler.GetLatestFWRequestTicket(new FwoOwner { Id = 7 }, apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(ticket, Is.Null);
                Assert.That(apiConn.Queries, Does.Contain(ExtRequestQueries.getLatestTicketId));
                Assert.That(apiConn.Queries, Does.Not.Contain(RequestQueries.getTicketById));
            });
        }
    }
}

using FWO.Api.Client;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ExternalRequestControllerTest
    {
        [Test]
        public async Task Post_ReturnsFalseWhenTicketIdIsNotPositive()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new();
            ExternalRequestController controller = new(apiConnection);

            bool result = await controller.Post(new ExternalRequestAddParameters { TicketId = 0 });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.QueryCount, Is.Zero);
        }

        [Test]
        public async Task Change_ReturnsFalseWhenRequestIdIsNotPositive()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new();
            ExternalRequestController controller = new(apiConnection);

            bool result = await controller.Change(new ExternalRequestPatchStateParameters
            {
                ExtRequestId = 0,
                TicketId = 99,
                TaskNumber = 1,
                ExtRequestState = "done"
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.QueryCount, Is.Zero);
        }

        private sealed class ExternalRequestControllerTestApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}

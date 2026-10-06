using System.Security.Claims;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ExternalRequestControllerTest
    {
        private const long kTicketId = 20;
        private const int kOwnApp = 8;
        private const int kForeignApp = 9;
        private static readonly List<long> kExpectedSentTicketIds = [kTicketId];

        [Test]
        public async Task Post_ReturnsFalseWhenTicketIdIsNotPositive()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new();
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, kOwnApp.ToString());

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = 0 });

            Assert.That(result.Value, Is.False);
            Assert.That(apiConnection.QueryCount, Is.Zero);
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_ModellerOfTicketOwnerStartsRequests()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket(kOwnApp) };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, $"[{kOwnApp}]");

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Value, Is.True);
            Assert.That(sender.SentTicketIds, Is.EqualTo(kExpectedSentTicketIds));
        }

        [Test]
        public async Task Post_ModellerOfOtherOwnerGetsNotFound()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket(kForeignApp) };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, $"[{kOwnApp}]");

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
            Assert.That(apiConnection.OpenRequestQueryCount, Is.Zero);
        }

        [Test]
        public async Task Post_ModellerOwningOnlyPartOfTicketGetsNotFound()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket(kOwnApp, kForeignApp) };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, $"[{kOwnApp}]");

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_ModellerWithoutEditableOwnersGetsNotFound()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket(kOwnApp) };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, null);

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_ModellerGetsNotFoundForTicketWithoutOwners()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket() };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, $"[{kOwnApp}]");

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_NonExistingTicketGetsNotFound()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new();
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Admin, null);

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_AdminStartsRequestsForAnyOwner()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket(kForeignApp) };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Admin, null);

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Value, Is.True);
            Assert.That(sender.SentTicketIds, Is.EqualTo(kExpectedSentTicketIds));
        }

        [Test]
        public async Task Post_CompletedTicketIsRefused()
        {
            WfTicket ticket = CreateTicket(kOwnApp);
            ticket.CompletionDate = DateTime.Now;
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = ticket };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, $"[{kOwnApp}]");

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<ConflictObjectResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_TicketWithOpenRequestIsRefused()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new()
            {
                Ticket = CreateTicket(kOwnApp),
                OpenRequests = [new ExternalRequest { Id = 1, TicketId = kTicketId }]
            };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Admin, null);

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Result, Is.InstanceOf<ConflictObjectResult>());
            Assert.That(sender.SentTicketIds, Is.Empty);
        }

        [Test]
        public async Task Post_OpenRequestOfOtherTicketDoesNotBlockReinit()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new()
            {
                Ticket = CreateTicket(kOwnApp),
                OpenRequests = [new ExternalRequest { Id = 1, TicketId = kTicketId + 1 }]
            };
            RequestSender sender = new();
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Admin, null);

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Value, Is.True);
            Assert.That(sender.SentTicketIds, Is.EqualTo(kExpectedSentTicketIds));
        }

        [Test]
        public async Task Post_ReturnsFalseWhenSendingFails()
        {
            ExternalRequestControllerTestApiConnection apiConnection = new() { Ticket = CreateTicket(kOwnApp) };
            RequestSender sender = new() { Result = false };
            ExternalRequestController controller = CreateController(apiConnection, sender, Roles.Modeller, $"[{kOwnApp}]");

            ActionResult<bool> result = await controller.Post(new ExternalRequestAddParameters { TicketId = kTicketId });

            Assert.That(result.Value, Is.False);
            Assert.That(sender.SentTicketIds, Is.EqualTo(kExpectedSentTicketIds));
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

        private static WfTicket CreateTicket(params int[] ownerIds)
        {
            return new WfTicket
            {
                Id = kTicketId,
                Tasks = [.. ownerIds.Select((ownerId, index) => new WfReqTask
                {
                    TaskNumber = index + 1,
                    Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = ownerId } }]
                })]
            };
        }

        private static ExternalRequestController CreateController(ExternalRequestControllerTestApiConnection apiConnection,
            RequestSender sender, string role, string? editableOwners)
        {
            List<Claim> claims = [new Claim(ClaimTypes.Role, role)];
            if (editableOwners != null)
            {
                claims.Add(new Claim("x-hasura-editable-owners", editableOwners));
            }
            return new ExternalRequestController(apiConnection, sender.Send)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                    }
                }
            };
        }

        private sealed class RequestSender
        {
            public bool Result { get; init; } = true;
            public List<long> SentTicketIds { get; } = [];

            public Task<bool> Send(long ticketId)
            {
                SentTicketIds.Add(ticketId);
                return Task.FromResult(Result);
            }
        }

        private sealed class ExternalRequestControllerTestApiConnection : SimulatedApiConnection
        {
            public WfTicket? Ticket { get; init; }
            public List<ExternalRequest> OpenRequests { get; init; } = [];
            public int QueryCount { get; private set; }
            public int OpenRequestQueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                if (typeof(QueryResponseType) == typeof(WfTicket) && query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((QueryResponseType)(object)Ticket!);
                }
                if (typeof(QueryResponseType) == typeof(List<ExternalRequest>) && query == ExtRequestQueries.getOpenRequests)
                {
                    OpenRequestQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)OpenRequests);
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}

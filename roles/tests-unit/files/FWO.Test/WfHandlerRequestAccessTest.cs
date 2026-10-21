using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerRequestAccessTest
    {
        private const int kCurrentUserId = 77;
        private const int kOtherUserId = 88;
        private const int kResponsibleOwnerId = 5;
        private const int kForeignOwnerId = 9;
        private const long kTicketId = 7;
        private const long kOwnTicketId = 1;
        private const long kAppTicketId = 2;
        private const long kForeignTicketId = 3;
        private static readonly List<int> kResponsibleOwnerIds = [kResponsibleOwnerId];
        private static readonly List<long> kOwnAndAppTicketIds = [kOwnTicketId, kAppTicketId];
        private static readonly List<long> kAllTicketIds = [kOwnTicketId, kAppTicketId, kForeignTicketId];

        private sealed class RequestAccessApiConn : SimulatedApiConnection
        {
            public WfTicket Ticket { get; set; } = new();
            public List<WfTicket> Tickets { get; set; } = [];
            public List<long> RegisteredTicketIds { get; set; } = [];
            public List<string> Queries { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == RequestQueries.getOwnerTicketIds)
                {
                    List<TicketId> ids = RegisteredTicketIds.ConvertAll(id => new TicketId { Id = id });
                    return Task.FromResult((T)(object)ids);
                }
                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((T)(object)Ticket);
                }
                if (query == RequestQueries.getTickets)
                {
                    return Task.FromResult((T)(object)new List<WfTicket>(Tickets));
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class MessageRecorder
        {
            public List<string> Messages { get; } = [];
        }

        private static UiRequestCoverageTest.RequestCoverageUserConfig CreateUserConfig(params string[] roles)
        {
            return UiRequestCoverageTest.CreateUserConfig(roles);
        }

        private static WfHandler CreateHandler(RequestAccessApiConn apiConn, UiRequestCoverageTest.RequestCoverageUserConfig userConfig,
            MessageRecorder? recorder = null, bool restricted = true)
        {
            Action<Exception?, string, string, bool> displayMessage = recorder != null
                ? (_, _, message, _) => recorder.Messages.Add(message)
                : DefaultInit.DoNothing;
            WfHandler handler = new(displayMessage, userConfig, new System.Security.Claims.ClaimsPrincipal(), apiConn, null!, WorkflowPhases.request)
            {
                RestrictToResponsibleRequests = restricted
            };
            ActionHandler actionHandler = new(apiConn, handler);
            WfDbAccess dbAccess = new(displayMessage, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);
            FieldInfo? dbAccField = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(dbAccField, Is.Not.Null);
            dbAccField!.SetValue(handler, dbAccess);
            return handler;
        }

        private static async Task<WfHandler> CreateHandlerWithScope(RequestAccessApiConn apiConn, UiRequestCoverageTest.RequestCoverageUserConfig userConfig,
            MessageRecorder? recorder = null, bool restricted = true)
        {
            WfHandler handler = CreateHandler(apiConn, userConfig, recorder, restricted);
            await handler.LoadOwnerScope(apiConn, kResponsibleOwnerIds);
            return handler;
        }

        private static WfTicket CreateTicket(int requesterId)
        {
            return new WfTicket
            {
                Id = kTicketId,
                StateId = 1,
                Requester = new UiUser { DbId = requesterId }
            };
        }

        private static WfTicket CreateTicketWithTaskOwner(int ownerId)
        {
            WfTicket ticket = CreateTicket(kOtherUserId);
            ticket.Tasks.Add(new WfReqTask
            {
                Id = 11,
                TaskType = WfTaskType.new_interface.ToString(),
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = ownerId } }]
            });
            return ticket;
        }

        private static WfTicket CreateInterfaceTicketRequestedBy(int requestingOwnerId)
        {
            WfTicket ticket = CreateTicketWithTaskOwner(kForeignOwnerId);
            ticket.Tasks[0].SetAddInfo(AdditionalInfoKeys.ReqOwner, requestingOwnerId.ToString());
            return ticket;
        }

        [Test]
        public async Task ResolveTicket_ReturnsOwnTicketAsEditable()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kCurrentUserId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Editable, Is.True);
        }

        [Test]
        public async Task ResolveTicket_HidesTicketOfOtherRequester()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task HandleInjectedTicketId_DoesNotOpenTicketOfOtherRequester()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));
            handler.MasterStateMatrix = new StateMatrix { LowestEndState = 5 };

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, kTicketId);

            Assert.Multiple(() =>
            {
                Assert.That(phase, Is.Empty);
                Assert.That(handler.EditTicketMode, Is.False);
                Assert.That(handler.ActTicket.Id, Is.Not.EqualTo(kTicketId));
            });
        }

        [Test]
        public async Task ResolveTicket_AllowsUsersOfRequestedApp()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicketWithTaskOwner(kResponsibleOwnerId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Editable, Is.True);
        }

        [Test]
        public async Task ResolveTicket_AllowsUsersOfRequestingAppToEditInterfaceRequest()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateInterfaceTicketRequestedBy(kResponsibleOwnerId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Editable, Is.True);
        }

        [Test]
        public async Task ResolveTicket_HidesInterfaceRequestBetweenForeignApps()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateInterfaceTicketRequestedBy(kForeignOwnerId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_KeepsVisibilityGroupsForOwnTickets()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kCurrentUserId) };
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            userConfig.ReqVisibilityBased = true;
            WfHandler handler = await CreateHandlerWithScope(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix { StateVisibilityGroupIds = { [1] = [3] } };

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_AllowsTicketRegisteredForResponsibleApp()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId), RegisteredTicketIds = [kTicketId] };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getOwnerTicketIds));
        }

        [Test]
        public async Task ResolveTicket_OwnerBasedModeDoesNotWidenToAllApps()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicketWithTaskOwner(kForeignOwnerId) };
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            userConfig.ReqOwnerBased = true;
            WfHandler handler = await CreateHandlerWithScope(apiConn, userConfig);
            handler.AllOwners = [new FwoOwner { Id = kResponsibleOwnerId }, new FwoOwner { Id = kForeignOwnerId }];

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_AdminSeesAndChangesAllTickets()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Admin));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(handler.CanChangeActTicket(), Is.True);
        }

        [Test]
        public async Task ResolveTicket_AuditorSeesForeignTicketWithoutChangeRight()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Auditor));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(ticket!.Editable, Is.False);
                Assert.That(handler.CanChangeActTicket(), Is.False);
            });
        }

        [Test]
        public async Task ResolveTicket_WithoutRestrictionKeepsResolvingForeignTickets()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId) };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Modeller), restricted: false);

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(apiConn.Queries, Does.Not.Contain(RequestQueries.getOwnerTicketIds));
        }

        [Test]
        public async Task LoadOwnerScope_SkipsQueryWithoutResponsibleApps()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateTicket(kOtherUserId), RegisteredTicketIds = [kTicketId] };
            WfHandler handler = CreateHandler(apiConn, CreateUserConfig(Roles.Requester));

            await handler.LoadOwnerScope(apiConn, null);
            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Null);
            Assert.That(apiConn.Queries, Does.Not.Contain(RequestQueries.getOwnerTicketIds));
        }

        [Test]
        public void IsResponsibleForRequest_IgnoresTicketsWithoutRequesterForUserWithoutDbId()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            userConfig.User.DbId = 0;
            WfHandler handler = CreateHandler(new RequestAccessApiConn(), userConfig);

            bool responsible = handler.IsResponsibleForRequest(new WfTicket { Requester = new UiUser() });

            Assert.That(responsible, Is.False);
        }

        [Test]
        public async Task SaveTicket_RefusesTicketTheUserMayNotChange()
        {
            RequestAccessApiConn apiConn = new();
            MessageRecorder recorder = new();
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester), recorder);
            handler.ActTicket = CreateTicket(kOtherUserId);
            handler.ActTicket.Editable = false;
            int queryCount = apiConn.Queries.Count;

            long savedId = await handler.SaveTicket(handler.ActTicket);

            Assert.Multiple(() =>
            {
                Assert.That(savedId, Is.Zero);
                Assert.That(apiConn.Queries, Has.Count.EqualTo(queryCount));
                Assert.That(recorder.Messages, Does.Contain("E5114"));
            });
        }

        [Test]
        public async Task PromoteTicketAndTasks_RefusesTicketTheUserMayNotChange()
        {
            RequestAccessApiConn apiConn = new();
            MessageRecorder recorder = new();
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester), recorder);
            handler.ActTicket = CreateTicket(kOtherUserId);
            handler.ActTicket.Editable = false;
            int queryCount = apiConn.Queries.Count;

            bool promoted = await handler.PromoteTicketAndTasks(handler.ActTicket);

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.False);
                Assert.That(apiConn.Queries, Has.Count.EqualTo(queryCount));
                Assert.That(recorder.Messages, Does.Contain("E5114"));
            });
        }

        [Test]
        public async Task RequestTaskChanges_AreRefusedForTicketTheUserMayNotChange()
        {
            RequestAccessApiConn apiConn = new();
            MessageRecorder recorder = new();
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester), recorder);
            handler.ActTicket = CreateTicketWithTaskOwner(kForeignOwnerId);
            handler.ActTicket.Editable = false;
            handler.ActReqTask = handler.ActTicket.Tasks[0];
            int queryCount = apiConn.Queries.Count;

            await handler.ChangeReqTask();
            await handler.ChangeOwner();
            await handler.ConfDeleteReqTask();
            handler.ActReqTask = new WfReqTask { Title = "new task" };
            await handler.AddReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
                Assert.That(apiConn.Queries, Has.Count.EqualTo(queryCount));
                Assert.That(recorder.Messages, Has.Count.EqualTo(4));
            });
        }

        private static List<WfTicket> CreateOwnAppAndForeignTickets()
        {
            WfTicket ownTicket = CreateTicket(kCurrentUserId);
            ownTicket.Id = kOwnTicketId;
            WfTicket appTicket = CreateTicketWithTaskOwner(kResponsibleOwnerId);
            appTicket.Id = kAppTicketId;
            WfTicket foreignTicket = CreateTicketWithTaskOwner(kForeignOwnerId);
            foreignTicket.Id = kForeignTicketId;
            return [ownTicket, appTicket, foreignTicket];
        }

        [Test]
        public async Task FetchTicketList_RequesterGetsOwnAndAppTicketsOnly()
        {
            RequestAccessApiConn apiConn = new() { Tickets = CreateOwnAppAndForeignTickets() };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            List<WfTicket> tickets = await handler.FetchTicketList(kResponsibleOwnerIds, false, false);

            Assert.Multiple(() =>
            {
                Assert.That(tickets.ConvertAll(ticket => ticket.Id), Is.EquivalentTo(kOwnAndAppTicketIds));
                Assert.That(tickets.TrueForAll(ticket => ticket.Editable), Is.True);
            });
        }

        [Test]
        public async Task FetchTicketList_AdminGetsAllTickets()
        {
            RequestAccessApiConn apiConn = new() { Tickets = CreateOwnAppAndForeignTickets() };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Admin));

            List<WfTicket> tickets = await handler.FetchTicketList(null, false, false);

            Assert.Multiple(() =>
            {
                Assert.That(tickets.ConvertAll(ticket => ticket.Id), Is.EquivalentTo(kAllTicketIds));
                Assert.That(tickets.TrueForAll(ticket => ticket.Editable), Is.True);
            });
        }

        [Test]
        public async Task FetchTicketList_AuditorGetsAllTicketsButMayChangeOnlyOwnAndApp()
        {
            RequestAccessApiConn apiConn = new() { Tickets = CreateOwnAppAndForeignTickets() };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Auditor));

            List<WfTicket> tickets = await handler.FetchTicketList(kResponsibleOwnerIds, false, false);

            Assert.Multiple(() =>
            {
                Assert.That(tickets.ConvertAll(ticket => ticket.Id), Is.EquivalentTo(kAllTicketIds));
                Assert.That(tickets.FindAll(ticket => ticket.Editable).ConvertAll(ticket => ticket.Id), Is.EquivalentTo(kOwnAndAppTicketIds));
            });
        }

        [Test]
        public async Task ResolveTicket_OwnerBasedModeLetsRequestingAppEditInterfaceRequest()
        {
            RequestAccessApiConn apiConn = new() { Ticket = CreateInterfaceTicketRequestedBy(kResponsibleOwnerId) };
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            userConfig.ReqOwnerBased = true;
            WfHandler handler = await CreateHandlerWithScope(apiConn, userConfig);

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Editable, Is.True);
        }

        [Test]
        public async Task ResolveTicket_RestrictionNeverMakesTicketEditableAgain()
        {
            WfTicket loadedTicket = CreateTicket(kCurrentUserId);
            loadedTicket.Editable = false;
            RequestAccessApiConn apiConn = new() { Ticket = loadedTicket };
            WfHandler handler = await CreateHandlerWithScope(apiConn, CreateUserConfig(Roles.Requester));

            WfTicket? ticket = await handler.ResolveTicket(kTicketId);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Editable, Is.False);
        }

        [TestCase(StateActionTypes.TrafficPathAnalysis, false, false, true)]
        [TestCase(StateActionTypes.AutoPromote, false, false, false)]
        [TestCase(StateActionTypes.AutoPromote, true, false, true)]
        [TestCase(StateActionTypes.AutoPromote, true, true, false)]
        public void CanPerformActionOnActTicket_NeedsChangeRightForChangingActions(StateActionTypes actionType, bool editable, bool readOnly, bool expected)
        {
            WfHandler handler = CreateHandler(new RequestAccessApiConn(), CreateUserConfig(Roles.Requester, Roles.Auditor));
            handler.ActTicket = new WfTicket { Editable = editable };
            handler.ReadOnlyMode = readOnly;

            bool allowed = handler.CanPerformActionOnActTicket(new WfStateAction { ActionType = actionType.ToString() });

            Assert.That(allowed, Is.EqualTo(expected));
        }

        [Test]
        public void CanChangeActTicket_WithoutRestrictionLeavesDecisionToPhase()
        {
            WfHandler handler = CreateHandler(new RequestAccessApiConn(), CreateUserConfig(Roles.Approver), restricted: false);
            handler.ActTicket = new WfTicket { Editable = false };

            Assert.That(handler.CanChangeActTicket(), Is.True);
        }
    }
}

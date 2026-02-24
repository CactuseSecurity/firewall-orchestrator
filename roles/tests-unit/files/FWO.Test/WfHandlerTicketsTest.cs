using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerTicketsTest
    {
        private sealed class TicketTestApiConn : SimulatedApiConnection
        {
            public WfTicket Ticket { get; set; } = new();

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
            {
                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((T)(object)Ticket);
                }
                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((T)(object)Array.Empty<ConfigItem>());
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class TestGlobalStateMatrix : GlobalStateMatrix
        {
            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master, bool reset = false)
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new StateMatrix { LowestEndState = 5 },
                    [WorkflowPhases.approval] = new StateMatrix { LowestEndState = 10 },
                    [WorkflowPhases.planning] = new StateMatrix { LowestEndState = 20 }
                };
                return Task.CompletedTask;
            }
        }

        private static WfHandler CreateHandlerWithDbAccess(TicketTestApiConn apiConn, UserConfig userConfig)
        {
            WfHandler handler = new(DefaultInit.DoNothing, userConfig, new System.Security.Claims.ClaimsPrincipal(), apiConn, null!, WorkflowPhases.request);
            ActionHandler actionHandler = new(apiConn, handler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);
            FieldInfo? dbAccField = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(dbAccField, Is.Not.Null);
            dbAccField!.SetValue(handler, dbAccess);
            return handler;
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsNewPhase_WhenStateInLaterPhase()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 12 } };
            UserConfig userConfig = new();
            Func<GlobalStateMatrix> originalFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new TestGlobalStateMatrix();
            try
            {
                WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
                handler.MasterStateMatrix.PhaseActive[WorkflowPhases.request] = true;
                handler.MasterStateMatrix.PhaseActive[WorkflowPhases.approval] = true;
                handler.MasterStateMatrix.PhaseActive[WorkflowPhases.planning] = true;
                handler.MasterStateMatrix.IsLastActivePhase = false;

                string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

                Assert.That(phase, Is.EqualTo(WorkflowPhases.planning.ToString()));
            }
            finally
            {
                GlobalStateMatrix.Factory = originalFactory;
            }
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsEmpty_WhenNoDbAccess()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 12 } };
            UserConfig userConfig = new();
            WfHandler handler = new(DefaultInit.DoNothing, userConfig, new System.Security.Claims.ClaimsPrincipal(), apiConn, null!, WorkflowPhases.request);

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

            Assert.That(phase, Is.EqualTo(""));
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsEmpty_WhenStateBeforeEnd()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 5 } };
            UserConfig userConfig = new();
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix.LowestEndState = 10;
            handler.MasterStateMatrix.IsLastActivePhase = false;

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

            Assert.That(phase, Is.EqualTo(""));
            Assert.That(handler.DisplayTicketMode, Is.True);
            Assert.That(handler.EditTicketMode, Is.True);
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsEmpty_WhenLastActivePhase()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 12 } };
            UserConfig userConfig = new();
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix.LowestEndState = 10;
            handler.MasterStateMatrix.IsLastActivePhase = true;

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

            Assert.That(phase, Is.EqualTo(""));
            Assert.That(handler.DisplayTicketMode, Is.True);
            Assert.That(handler.EditTicketMode, Is.False);
        }

        [Test]
        public void SetTicketOpt_SetsModes()
        {
            WfHandler handler = new();

            handler.SetTicketOpt(ObjAction.add);
            Assert.That(handler.DisplayTicketMode, Is.True);
            Assert.That(handler.EditTicketMode, Is.True);
            Assert.That(handler.AddTicketMode, Is.True);

            handler.ResetTicketActions();
            Assert.That(handler.DisplayTicketMode, Is.False);
            Assert.That(handler.EditTicketMode, Is.False);
            Assert.That(handler.AddTicketMode, Is.False);
        }

        [Test]
        public async Task ConfAddCommentToTicket_AddsComment()
        {
            WfHandler handler = new();
            handler.ActTicket = new WfTicket();

            await handler.ConfAddCommentToTicket("comment");

            Assert.That(handler.ActTicket.Comments, Has.Count.EqualTo(1));
            Assert.That(handler.ActTicket.Comments[0].Comment.CommentText, Is.EqualTo("comment"));
        }
    }
}

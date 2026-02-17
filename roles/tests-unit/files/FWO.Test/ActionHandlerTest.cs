using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ActionHandlerTest
    {
        private sealed class ActionHandlerTestApiConn : SimulatedApiConnection
        {
            public List<WfState> States { get; set; } = [];
            public List<string> Queries { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
            {
                Queries.Add(query);
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((T)(object)States);
                }
                if (query == MonitorQueries.addAlert)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper());
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private static WfStateActionDataHelper CreateAction(string eventName, string actionType, string scope, string phase = "")
        {
            return new WfStateActionDataHelper
            {
                Action = new WfStateAction
                {
                    Event = eventName,
                    ActionType = actionType,
                    Scope = scope,
                    Phase = phase
                }
            };
        }

        [Test]
        public async Task GetOfferedActions_FiltersByEventAndPhase()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState
                {
                    Id = 1,
                    Actions =
                    [
                        CreateAction(StateActionEvents.OfferButton.ToString(), StateActionTypes.DoNothing.ToString(), WfObjectScopes.Ticket.ToString()),
                        CreateAction(StateActionEvents.OfferButton.ToString(), StateActionTypes.DoNothing.ToString(), WfObjectScopes.Ticket.ToString(), WorkflowPhases.planning.ToString()),
                        CreateAction(StateActionEvents.OnSet.ToString(), StateActionTypes.DoNothing.ToString(), WfObjectScopes.Ticket.ToString())
                    ]
                }
            ];
            WfHandler wfHandler = new();
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };

            List<WfStateAction> actions = handler.GetOfferedActions(ticket, WfObjectScopes.Ticket, WorkflowPhases.request);

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Event, Is.EqualTo(StateActionEvents.OfferButton.ToString()));
        }

        [Test]
        public async Task DoStateChangeActions_RunsOnSetAndOnLeave_AndResetsStateChanged()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState
                {
                    Id = 1,
                    Actions =
                    [
                        CreateAction(StateActionEvents.OnSet.ToString(), StateActionTypes.SetAlert.ToString(), WfObjectScopes.Ticket.ToString())
                    ]
                },
                new WfState
                {
                    Id = 0,
                    Actions =
                    [
                        CreateAction(StateActionEvents.OnLeave.ToString(), StateActionTypes.SetAlert.ToString(), WfObjectScopes.Ticket.ToString())
                    ]
                }
            ];
            WfHandler wfHandler = new();
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = new();
            ticket.StateId = 1;

            await handler.DoStateChangeActions(ticket, WfObjectScopes.Ticket);

            Assert.That(apiConn.Queries.Count(q => q == MonitorQueries.addAlert), Is.EqualTo(2));
            Assert.That(ticket.StateChanged(), Is.False);
        }

        [Test]
        public async Task DoOwnerChangeActions_ExecutesOwnerChangeActions()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState
                {
                    Id = 1,
                    Actions =
                    [
                        CreateAction(StateActionEvents.OwnerChange.ToString(), StateActionTypes.SetAlert.ToString(), WfObjectScopes.None.ToString())
                    ]
                }
            ];
            WfHandler wfHandler = new();
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };

            await handler.DoOwnerChangeActions(ticket, new FwoOwner { Id = 1 }, 123);

            Assert.That(apiConn.Queries.Count(q => q == MonitorQueries.addAlert), Is.EqualTo(1));
        }

        [Test]
        public async Task DoOnAssignmentActions_ExecutesAssignmentActions()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState
                {
                    Id = 1,
                    Actions =
                    [
                        CreateAction(StateActionEvents.OnAssignment.ToString(), StateActionTypes.SetAlert.ToString(), WfObjectScopes.None.ToString())
                    ]
                }
            ];
            WfHandler wfHandler = new();
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };

            await handler.DoOnAssignmentActions(ticket, "dn=test");

            Assert.That(apiConn.Queries.Count(q => q == MonitorQueries.addAlert), Is.EqualTo(1));
        }

    }
}

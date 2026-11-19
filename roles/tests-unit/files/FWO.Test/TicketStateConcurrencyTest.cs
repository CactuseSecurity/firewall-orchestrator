using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the guarded ticket state write: users in the UI, the workflow action endpoint and the "While set" job change
    /// ticket states without waiting for each other, and a state one of them has set must not be overwritten by another,
    /// nor may the actions of the refused change run.
    /// </summary>
    [TestFixture]
    internal class TicketStateConcurrencyTest
    {
        private const long kTicketId = 42;
        private const long kReqTaskId = 7;
        private const int kReadState = 49;
        private const int kTargetState = 60;
        private const int kFollowUpState = 99;
        private const int kConcurrentState = 610;
        private const int kCompletedState = 200;
        private const int kPriority = 3;
        private const string kUpdateFailedTextKey = "E8002";

        private static readonly DateTime kDeadline = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Unspecified);
        private static readonly List<string> kConcurrentChangeMessages = [WfDbAccess.kTicketChangedConcurrentlyTextKey];
        private static readonly List<WfState> kReadAndTargetStates = [new() { Id = kReadState }, new() { Id = kTargetState }];

        [Test]
        public async Task UpdateTicketStateInDb_WritesTheReadStateAsConditionTogetherWithAllFields()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfDbAccess dbAccess = CreateDbAccess(apiConn, messages, new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);
            ticket.StateId = kTargetState;
            ticket.CompletionDate = kDeadline.AddDays(-1);

            bool stored = await dbAccess.UpdateTicketStateInDb(ticket);

            object? variables = apiConn.TicketStateWriteVariables.Single();
            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.True);
                Assert.That(GetVariable<long>(variables, "id"), Is.EqualTo(kTicketId));
                Assert.That(GetVariable<int>(variables, "expectedState"), Is.EqualTo(kReadState));
                Assert.That(GetVariable<int>(variables, "state"), Is.EqualTo(kTargetState));
                Assert.That(GetVariable<DateTime?>(variables, "closed"), Is.EqualTo(kDeadline.AddDays(-1)));
                Assert.That(GetVariable<DateTime?>(variables, "deadline"), Is.EqualTo(kDeadline));
                Assert.That(GetVariable<int?>(variables, "priority"), Is.EqualTo(kPriority));
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kTargetState));
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.EqualTo(1), "the actions of the written state run");
                Assert.That(ticket.StateChanged(), Is.False);
                Assert.That(messages, Is.Empty);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task UpdateTicketStateInDb_RefusesAndKeepsTheConcurrentStateWhenTheTicketWasMovedMeanwhile()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            apiConn.BeforeTicketStateWrite = () => apiConn.StoredStateId = kConcurrentState;
            List<string> messages = [];
            WfDbAccess dbAccess = CreateDbAccess(apiConn, messages, new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);
            ticket.StateId = kTargetState;

            bool stored = await dbAccess.UpdateTicketStateInDb(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.False);
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kConcurrentState), "the concurrent state change must survive");
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.Zero, "the actions of the refused state must not run");
                Assert.That(apiConn.HistoryEntryCount, Is.Zero, "a refused change is no change");
                Assert.That(ticket.StateId, Is.EqualTo(kConcurrentState), "the ticket in memory holds the stored state");
                Assert.That(ticket.StateChanged(), Is.False);
                Assert.That(messages, Is.EqualTo(kConcurrentChangeMessages));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task UpdateTicketStateInDb_TreatsATicketAlreadyMovedIntoTheRequestedStateAsDone()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            apiConn.BeforeTicketStateWrite = () => apiConn.StoredStateId = kTargetState;
            List<string> messages = [];
            WfDbAccess dbAccess = CreateDbAccess(apiConn, messages, new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);
            ticket.StateId = kTargetState;

            bool stored = await dbAccess.UpdateTicketStateInDb(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.True);
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.Zero, "whoever moved the ticket has run the actions");
                Assert.That(ticket.StateId, Is.EqualTo(kTargetState));
                Assert.That(ticket.StateChanged(), Is.False);
                Assert.That(messages, Is.Empty);
            });
        }

        [Test]
        public async Task UpdateTicketStateInDb_ReportsAFailedUpdateWhenTheRefusingTicketCannotBeReadAgain()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            apiConn.BeforeTicketStateWrite = () =>
            {
                apiConn.StoredStateId = kConcurrentState;
                apiConn.TicketReadFails = true;
            };
            List<string> messages = [];
            WfDbAccess dbAccess = CreateDbAccess(apiConn, messages, new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);
            ticket.StateId = kTargetState;

            bool stored = await dbAccess.UpdateTicketStateInDb(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.False);
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.Zero);
                Assert.That(ticket.StateId, Is.EqualTo(kReadState), "the ticket in memory falls back to the state it was read in");
                Assert.That(ticket.StateChanged(), Is.False);
                Assert.That(messages, Does.Contain(kUpdateFailedTextKey));
            });
        }

        [Test]
        public async Task UpdateTicketStateInDb_ExpectsTheWrittenStateInTheNextWriteOfTheSameTicket()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            WfDbAccess dbAccess = CreateDbAccess(apiConn, new List<string>(), new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);

            ticket.StateId = kTargetState;
            bool firstStored = await dbAccess.UpdateTicketStateInDb(ticket);
            ticket.StateId = kFollowUpState;
            bool secondStored = await dbAccess.UpdateTicketStateInDb(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(firstStored && secondStored, Is.True);
                Assert.That(GetVariable<int>(apiConn.TicketStateWriteVariables[1], "expectedState"), Is.EqualTo(kTargetState));
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kFollowUpState));
            });
        }

        [Test]
        public async Task UpdateTicketStateInDb_ExpectsTheStoredStateForACreatedTicketWhoseActionsHaveNotRun()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            WfDbAccess dbAccess = CreateDbAccess(apiConn, new List<string>(), new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);
            ticket.MarkCreatedStateChanged(kReadState);
            ticket.StateId = kTargetState;

            bool stored = await dbAccess.UpdateTicketStateInDb(ticket, false);

            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.True);
                Assert.That(GetVariable<int>(apiConn.TicketStateWriteVariables.Single(), "expectedState"), Is.EqualTo(kReadState));
            });
        }

        [Test]
        public async Task CheckTicketStateUnchanged_ReportsATicketMovedMeanwhile()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kConcurrentState };
            List<string> messages = [];
            WfDbAccess dbAccess = CreateDbAccess(apiConn, messages, new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);

            (bool unchanged, WfTicket? storedTicket) = await dbAccess.CheckTicketStateUnchanged(ticket, kTargetState);

            Assert.Multiple(() =>
            {
                Assert.That(unchanged, Is.False);
                Assert.That(storedTicket?.StateId, Is.EqualTo(kConcurrentState));
                Assert.That(ticket.StateId, Is.EqualTo(kConcurrentState));
                Assert.That(messages, Is.EqualTo(kConcurrentChangeMessages));
            });
        }

        [Test]
        public async Task CheckTicketStateUnchanged_AcceptsATicketStillInTheReadState()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfDbAccess dbAccess = CreateDbAccess(apiConn, messages, new WfHandler());
            WfTicket ticket = LoadedTicket(kReadState);
            ticket.StateId = kTargetState;

            (bool unchanged, WfTicket? storedTicket) = await dbAccess.CheckTicketStateUnchanged(ticket, kTargetState);

            Assert.Multiple(() =>
            {
                Assert.That(unchanged, Is.True);
                Assert.That(storedTicket?.StateId, Is.EqualTo(kReadState));
                Assert.That(ticket.StateId, Is.EqualTo(kTargetState), "an unchanged ticket keeps the requested state");
                Assert.That(messages, Is.Empty);
            });
        }

        [Test]
        public async Task PromoteTicket_StoresTheStateAndCompletionDateOfAnUnchangedTicket()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.request);
            handler.SetTicketEnv(LoadedTicket(kReadState));

            bool promoted = await handler.PromoteTicket(new WfStatefulObject { StateId = kCompletedState });

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.True);
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kCompletedState));
                Assert.That(apiConn.StoredCompletionDate, Is.Not.Null, "a completed ticket gets its completion date");
                Assert.That(GetVariable<DateTime?>(apiConn.TicketStateWriteVariables.Single(), "deadline"), Is.EqualTo(kDeadline));
                Assert.That(GetVariable<int?>(apiConn.TicketStateWriteVariables.Single(), "priority"), Is.EqualTo(kPriority));
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.EqualTo(1));
                Assert.That(messages, Is.Empty);
            });
        }

        [Test]
        public async Task PromoteTicketAndTasks_LeavesTheTasksAloneWhenTheTicketWasMovedMeanwhile()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            apiConn.BeforeTicketStateWrite = () => apiConn.StoredStateId = kConcurrentState;
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.approval);
            handler.SetTicketEnv(LoadedTicketWithTask(kReadState));

            bool promoted = await handler.PromoteTicketAndTasks(new WfStatefulObject { StateId = kTargetState });

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.False);
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kConcurrentState));
                Assert.That(apiConn.RequestTaskStateWriteCount, Is.Zero, "the tasks must not follow a refused ticket promotion");
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.Zero);
                Assert.That(handler.ActTicket.StateId, Is.EqualTo(kConcurrentState));
                Assert.That(messages, Is.EqualTo(kConcurrentChangeMessages));
            });
        }

        [Test]
        public async Task PromoteTasksAndTicket_MovesNothingWhenTheTicketWasMovedBeforehand()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kConcurrentState };
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.approval);
            handler.SetTicketEnv(LoadedTicketWithTask(kReadState));

            bool promoted = await handler.PromoteTasksAndTicket(new WfStatefulObject { StateId = kTargetState });

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.False);
                Assert.That(apiConn.TicketStateWriteVariables, Is.Empty);
                Assert.That(apiConn.RequestTaskStateWriteCount, Is.Zero, "the tasks are checked before they are moved ahead of the ticket");
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.Zero);
                Assert.That(handler.ActTicket.StateId, Is.EqualTo(kConcurrentState));
                Assert.That(messages, Is.EqualTo(kConcurrentChangeMessages));
            });
        }

        [Test]
        public async Task PromoteTasksAndTicket_MovesTasksAndTicketOfAnUnchangedTicket()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.approval);
            handler.SetTicketEnv(LoadedTicketWithTask(kReadState));

            bool promoted = await handler.PromoteTasksAndTicket(new WfStatefulObject { StateId = kTargetState });

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.True);
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kTargetState));
                Assert.That(apiConn.RequestTaskStateWriteCount, Is.EqualTo(1));
                Assert.That(GetVariable<int>(apiConn.TicketStateWriteVariables.Single(), "expectedState"), Is.EqualTo(kReadState));
                Assert.That(messages, Is.Empty);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task AutoPromote_ReportsARefusedTicketPromotionAndLeavesTheTicketInTheConcurrentState()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            apiConn.BeforeTicketStateWrite = () => apiConn.StoredStateId = kConcurrentState;
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.request);
            WfTicket ticket = LoadedTicket(kReadState);
            handler.SetTicketEnv(ticket);

            bool promoted = await handler.AutoPromote(ticket, WfObjectScopes.Ticket, kTargetState);

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.False);
                Assert.That(ticket.StateId, Is.EqualTo(kConcurrentState), "the job sees that the ticket has left its state");
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kConcurrentState));
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.Zero);
                Assert.That(messages, Is.EqualTo(kConcurrentChangeMessages));
            });
        }

        [Test]
        public async Task AutoPromote_PromotesAnUnchangedTicket()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.request);
            WfTicket ticket = LoadedTicket(kReadState);
            handler.SetTicketEnv(ticket);

            bool promoted = await handler.AutoPromote(ticket, WfObjectScopes.Ticket, kTargetState);

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.True);
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kTargetState));
                Assert.That(apiConn.RecordedStateChangeExecutions, Is.EqualTo(1));
                Assert.That(messages, Is.Empty);
            });
        }

        [Test]
        public async Task PerformAutoPromoteAction_RefusedPromotion_ShowsNoConfirmMessage()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            apiConn.BeforeTicketStateWrite = () => apiConn.StoredStateId = kConcurrentState;
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.request);
            await handler.ActionHandler!.Init(kReadAndTargetStates);
            WfTicket ticket = LoadedTicket(kReadState);
            handler.SetTicketEnv(ticket);

            await handler.ActionHandler.PerformAction(CreateConfirmedAutoPromoteAction(), ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kConcurrentState));
                Assert.That(messages, Is.EqualTo(kConcurrentChangeMessages), "only the refusal is reported, not the forward");
                Assert.That(ticket.Comments, Is.Empty);
            });
        }

        [Test]
        public async Task PerformAutoPromoteAction_UnchangedTicket_ShowsConfirmMessage()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.request);
            await handler.ActionHandler!.Init(kReadAndTargetStates);
            WfTicket ticket = LoadedTicket(kReadState);
            handler.SetTicketEnv(ticket);

            await handler.ActionHandler.PerformAction(CreateConfirmedAutoPromoteAction(), ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kTargetState));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages, Does.Not.Contain(WfDbAccess.kTicketChangedConcurrentlyTextKey));
            });
        }

        [Test]
        public async Task ChangeTicketStateForMonitoring_GuardsAgainstTheStateShownForATicketReadWithoutTrackingReset()
        {
            StoredTicketApiConn apiConn = new() { StoredStateId = kReadState };
            List<string> messages = [];
            WfHandler handler = CreateHandler(apiConn, messages, WorkflowPhases.request);
            // read like the monitoring page does: deserialized, without resetting the change tracking
            WfTicket ticket = new() { Id = kTicketId, StateId = kReadState };

            await handler.ChangeTicketStateForMonitoring(ticket, kTargetState, MonitoringStateChangeMode.LocalOnly);

            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<int>(apiConn.TicketStateWriteVariables.Single(), "expectedState"), Is.EqualTo(kReadState));
                Assert.That(apiConn.StoredStateId, Is.EqualTo(kTargetState));
                Assert.That(messages, Is.Empty);
            });
        }

        private static WfStateAction CreateConfirmedAutoPromoteAction()
        {
            return new WfStateAction
            {
                Name = "Forward",
                ActionType = nameof(StateActionTypes.AutoPromote),
                Scope = nameof(WfObjectScopes.Ticket),
                Event = nameof(StateActionEvents.WhileSet),
                ExternalParams = System.Text.Json.JsonSerializer.Serialize(new SimpleAutoPromoteParams { ToStateId = kTargetState, ConfirmUiMessage = true })
            };
        }

        private static WfTicket LoadedTicket(int stateId)
        {
            WfTicket ticket = new() { Id = kTicketId, StateId = stateId, Deadline = kDeadline, Priority = kPriority };
            ticket.ResetStateChangeTracking();
            return ticket;
        }

        private static WfTicket LoadedTicketWithTask(int stateId)
        {
            WfTicket ticket = LoadedTicket(stateId);
            ticket.Tasks.Add(new WfReqTask { Id = kReqTaskId, TicketId = kTicketId, StateId = stateId, TaskType = WfTaskType.access.ToString() });
            ticket.ResetStateChangeTracking();
            return ticket;
        }

        private static WfDbAccess CreateDbAccess(ApiConnection apiConnection, List<string> messages, WfHandler handler)
        {
            ActionHandler actionHandler = new(apiConnection, handler, null, true);
            List<WfState> noStates = [];
            actionHandler.Init(noStates).GetAwaiter().GetResult();
            handler.ActionHandler = actionHandler;
            return new WfDbAccess((_, _, message, _) => messages.Add(message), new SimulatedUserConfig(), apiConnection, actionHandler,
                true, WorkflowPhases.request, false);
        }

        private static WfHandler CreateHandler(ApiConnection apiConnection, List<string> messages, WorkflowPhases phase)
        {
            SimulatedUserConfig userConfig = new();
            WfHandler handler = new(userConfig, apiConnection, phase, null, null, (_, _, message, _) => messages.Add(message))
            {
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 0,
                    LowestStartedState = kTargetState,
                    LowestEndState = kCompletedState,
                    MinTicketCompleted = kCompletedState,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                }
            };
            SetTaskMatrix(handler, new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = kTargetState,
                LowestEndState = kCompletedState,
                MinImplTasksNeeded = kCompletedState,
                PhaseActive = new() { { WorkflowPhases.approval, false }, { WorkflowPhases.planning, false } }
            });
            WfDbAccess dbAccess = CreateDbAccess(apiConnection, messages, handler);
            typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(handler, dbAccess);
            return handler;
        }

        private static void SetTaskMatrix(WfHandler handler, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[WfTaskType.access.ToString()] = matrix;
        }

        private static T GetVariable<T>(object? variables, string name)
        {
            object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
            return value == null ? default! : (T)value;
        }

        /// <summary>
        /// Simulates the stored ticket. The guarded state write only changes it while it is in the expected state, as the
        /// where condition of the mutation does in the database, and every read returns a new object, as the api does.
        /// </summary>
        private sealed class StoredTicketApiConn : SimulatedApiConnection
        {
            public int StoredStateId { get; set; }
            public DateTime? StoredCompletionDate { get; set; }
            public bool TicketReadFails { get; set; }

            /// <summary>
            /// Runs right before the guarded write is evaluated, so that a concurrent writer can move the ticket.
            /// </summary>
            public Action? BeforeTicketStateWrite { get; set; }
            public List<object?> TicketStateWriteVariables { get; } = [];
            public int RequestTaskStateWriteCount { get; private set; }
            public int RecordedStateChangeExecutions { get; private set; }
            public int HistoryEntryCount { get; private set; }
            public List<string> UnexpectedQueries { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((T)(object)ReadStoredTicket());
                }
                if (query == RequestQueries.updateTicketStateIfUnchanged)
                {
                    return Task.FromResult((T)(object)WriteTicketState(variables));
                }
                if (query == RequestQueries.updateRequestTaskState)
                {
                    RequestTaskStateWriteCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = GetVariable<long>(variables, "id") });
                }
                if (query == RequestQueries.recordStateChangeExecution)
                {
                    RecordedStateChangeExecutions++;
                    return Task.FromResult((T)(object)new ReturnId { AffectedRows = 1 });
                }
                if (query == ModellingQueries.addHistoryEntry || query == ModellingQueries.addHistoryEntryAsService)
                {
                    HistoryEntryCount++;
                    return Task.FromResult((T)(object)new ReturnIdWrapper());
                }
                UnexpectedQueries.Add(query);
                throw new AssertionException($"Unexpected query: {query}");
            }

            private WfTicket ReadStoredTicket()
            {
                if (TicketReadFails)
                {
                    throw new InvalidOperationException("ticket could not be read");
                }
                return new WfTicket
                {
                    Id = kTicketId,
                    StateId = StoredStateId,
                    CompletionDate = StoredCompletionDate,
                    Deadline = kDeadline,
                    Priority = kPriority,
                    Tasks = [new WfReqTask { Id = kReqTaskId, TicketId = kTicketId, StateId = StoredStateId, TaskType = WfTaskType.access.ToString() }]
                };
            }

            private ReturnId WriteTicketState(object? variables)
            {
                TicketStateWriteVariables.Add(variables);
                BeforeTicketStateWrite?.Invoke();
                if (GetVariable<int>(variables, "expectedState") != StoredStateId)
                {
                    return new ReturnId { AffectedRows = 0 };
                }
                StoredStateId = GetVariable<int>(variables, "state");
                StoredCompletionDate = GetVariable<DateTime?>(variables, "closed");
                return new ReturnId { AffectedRows = 1 };
            }
        }
    }
}

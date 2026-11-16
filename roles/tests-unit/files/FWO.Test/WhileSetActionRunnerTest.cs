using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Jobs;
using FWO.Middleware.Server.Services;
using FWO.Services.Workflow;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Quartz;

namespace FWO.Test
{
    [TestFixture]
    internal class WhileSetActionRunnerTest
    {
        private const int kCheckedState = 249;
        private const int kOtherState = 300;
        private const int kFirstActionId = 11;
        private const int kSecondActionId = 12;
        private const int kSendEmailActionId = 21;
        private const int kSetAlertActionId = 22;
        private static readonly List<int> kCheckedStateOnly = [kCheckedState];
        private static readonly List<string> kStatesQueryOnly = [RequestQueries.getStates];
        private static readonly List<int> kBothActionsInSortOrder = [kFirstActionId, kSecondActionId];
        private static readonly List<int> kFirstActionOnly = [kFirstActionId];
        private const string kGerman = "German";
        private static readonly List<string> kGermanOnly = [kGerman];
        private static readonly List<string> kConfigAndStatesQueries = [ConfigQueries.getConfigItemsByUser, RequestQueries.getStates];
        private static readonly ConfigItem[] kChangedDefaultLanguage = [new() { Key = "DefaultLanguage", Value = kGerman, User = 0 }];

        private static WfStateAction CreateAction(int id, string actionEvent = nameof(StateActionEvents.WhileSet), string scope = nameof(WfObjectScopes.Ticket),
            string actionType = nameof(StateActionTypes.AutoPromote))
        {
            return new WfStateAction { Id = id, Name = $"Action {id}", ActionType = actionType, Event = actionEvent, Scope = scope };
        }

        /// <summary>
        /// A state with "While set" ticket actions of an allowed type (auto-forward) and of two types not allowing "While set".
        /// </summary>
        private static List<WfState> CreateStateWithMixedActionTypes()
        {
            WfState checkedState = new() { Id = kCheckedState, Name = "Implemented" };
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 1, Action = CreateAction(kSendEmailActionId, actionType: nameof(StateActionTypes.SendEmail)) });
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 2, Action = CreateAction(kFirstActionId) });
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 3, Action = CreateAction(kSetAlertActionId, actionType: nameof(StateActionTypes.SetAlert)) });
            return [checkedState];
        }

        private static string CaptureLog(Action action)
        {
            using StringWriter logOutput = new();
            TextWriter originalOutput = Console.Out;
            try
            {
                Console.SetOut(logOutput);
                action();
            }
            finally
            {
                Console.SetOut(originalOutput);
            }
            return logOutput.ToString();
        }

        private static List<WfState> CreateStates()
        {
            WfState checkedState = new() { Id = kCheckedState, Name = "Implemented" };
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 3, Action = CreateAction(kSecondActionId) });
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 1, Action = CreateAction(kFirstActionId) });
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 2, Action = CreateAction(13, nameof(StateActionEvents.OnSet)) });
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 4, Action = CreateAction(14, scope: nameof(WfObjectScopes.RequestTask)) });
            WfState otherState = new() { Id = kOtherState, Name = "Review" };
            otherState.Actions.Add(new WfStateActionDataHelper { SortOrder = 1, Action = CreateAction(15, nameof(StateActionEvents.OnLeave)) });
            return [checkedState, otherState];
        }

        /// <summary>
        /// A state with one "While set" action that names its phase, so the executor needs no state matrix.
        /// </summary>
        private static List<WfState> CreateStateWithPhaseAction()
        {
            WfStateAction action = CreateAction(kFirstActionId);
            action.Phase = nameof(WorkflowPhases.review);
            WfState checkedState = new() { Id = kCheckedState, Name = "Implemented" };
            checkedState.Actions.Add(new WfStateActionDataHelper { SortOrder = 1, Action = action });
            return [checkedState];
        }

        private static List<WfTicket> CreateTickets(params long[] ticketIds)
        {
            return [.. ticketIds.Select(ticketId => new WfTicket { Id = ticketId, StateId = kCheckedState })];
        }

        [Test]
        public void GetWhileSetTicketActions_KeepsTicketScopeWhileSetActionsInSortOrder()
        {
            Dictionary<int, List<WfStateAction>> actionsByState = WhileSetActionRunner.GetWhileSetTicketActions(CreateStates());

            Assert.That(actionsByState.Keys, Is.EqualTo(kCheckedStateOnly));
            Assert.That(actionsByState[kCheckedState].Select(action => action.Id), Is.EqualTo(kBothActionsInSortOrder));
        }

        [Test]
        [NonParallelizable]
        public void GetWhileSetTicketActions_WarnsAboutWhileSetActionsWithOtherScope()
        {
            using StringWriter logOutput = new();
            TextWriter originalOutput = Console.Out;
            try
            {
                Console.SetOut(logOutput);
                WhileSetActionRunner.GetWhileSetTicketActions(CreateStates());
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            string log = logOutput.ToString();
            Assert.Multiple(() =>
            {
                Assert.That(log, Does.Contain("Warning - While Set Actions"));
                Assert.That(log, Does.Contain($"Action 'Action 14' (14) of state {kCheckedState} has event 'While set' and scope {nameof(WfObjectScopes.RequestTask)}"));
                Assert.That(log, Does.Not.Contain("'Action 11'"));
                Assert.That(log, Does.Not.Contain("'Action 13'"));
            });
        }

        [Test]
        [NonParallelizable]
        public void GetWhileSetTicketActions_SkipsActionTypesNotAllowingWhileSetWithWarning()
        {
            Dictionary<int, List<WfStateAction>> actionsByState = [];

            string log = CaptureLog(() => actionsByState = WhileSetActionRunner.GetWhileSetTicketActions(CreateStateWithMixedActionTypes()));

            Assert.Multiple(() =>
            {
                Assert.That(actionsByState[kCheckedState].Select(action => action.Id), Is.EqualTo(kFirstActionOnly));
                Assert.That(log, Does.Contain($"Action 'Action {kSendEmailActionId}' ({kSendEmailActionId}) of state {kCheckedState} has event 'While set' " +
                    $"and action type {nameof(StateActionTypes.SendEmail)}; it is never performed"));
                Assert.That(log, Does.Contain($"Action 'Action {kSetAlertActionId}' ({kSetAlertActionId}) of state {kCheckedState} has event 'While set' " +
                    $"and action type {nameof(StateActionTypes.SetAlert)}; it is never performed"));
                Assert.That(log, Does.Not.Contain($"'Action {kFirstActionId}'"));
            });
        }

        [Test]
        public async Task Run_PerformsOnlyActionTypesAllowingWhileSet()
        {
            RunnerApiConnection apiConnection = new() { States = CreateStateWithMixedActionTypes(), Tickets = CreateTickets(1) };
            RecordingExecutor executor = new();

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty);
                Assert.That(executor.ActionIdsOf(1), Is.EqualTo(kFirstActionOnly));
            });
        }

        [Test]
        public async Task Run_StateWithOnlyDisallowedActionTypes_DoesNotLoadTickets()
        {
            WfState state = new() { Id = kCheckedState, Name = "Implemented" };
            state.Actions.Add(new WfStateActionDataHelper { SortOrder = 1, Action = CreateAction(kSendEmailActionId, actionType: nameof(StateActionTypes.SendEmail)) });
            RunnerApiConnection apiConnection = new() { States = new List<WfState> { state }, Tickets = CreateTickets(1) };
            RecordingExecutor executor = new();

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty);
                Assert.That(apiConnection.Queries, Is.EqualTo(kStatesQueryOnly));
                Assert.That(executor.Calls, Is.Empty);
            });
        }

        [Test]
        public async Task Run_WithoutWhileSetActions_DoesNotLoadTickets()
        {
            RunnerApiConnection apiConnection = new() { States = [new WfState { Id = kOtherState }] };
            RecordingExecutor executor = new();

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.That(failures, Is.Empty);
            Assert.That(apiConnection.Queries, Is.EqualTo(kStatesQueryOnly));
            Assert.That(executor.Calls, Is.Empty);
        }

        [Test]
        public async Task Run_PerformsActionsOfEveryTicketInSortOrder()
        {
            RunnerApiConnection apiConnection = new() { States = CreateStates(), Tickets = CreateTickets(1, 2) };
            RecordingExecutor executor = new();

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty);
                Assert.That(apiConnection.TicketStateIds, Is.EqualTo(kCheckedStateOnly));
                Assert.That(executor.ActionIdsOf(1), Is.EqualTo(kBothActionsInSortOrder));
                Assert.That(executor.ActionIdsOf(2), Is.EqualTo(kBothActionsInSortOrder));
                Assert.That(executor.Calls.All(call => call.StateId == kCheckedState), Is.True);
            });
        }

        [TestCase(WhileSetActionResult.Moved)]
        [TestCase(WhileSetActionResult.Skipped)]
        public async Task Run_TicketLeavingTheState_StopsItsRemainingActions(WhileSetActionResult firstResult)
        {
            RunnerApiConnection apiConnection = new() { States = CreateStates(), Tickets = CreateTickets(1) };
            RecordingExecutor executor = new() { Results = { [(1, kFirstActionId)] = firstResult } };

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.That(failures, Is.Empty);
            Assert.That(executor.ActionIdsOf(1), Is.EqualTo(kFirstActionOnly));
        }

        [Test]
        public async Task Run_FailedAction_IsReportedAndOtherTicketsContinue()
        {
            RunnerApiConnection apiConnection = new() { States = CreateStates(), Tickets = CreateTickets(1, 2) };
            RecordingExecutor executor = new() { Results = { [(1, kFirstActionId)] = WhileSetActionResult.Failed } };

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(failures[0], Does.Contain("Ticket 1"));
            Assert.That(executor.ActionIdsOf(1), Is.EqualTo(kFirstActionOnly));
            Assert.That(executor.ActionIdsOf(2), Is.EqualTo(kBothActionsInSortOrder));
        }

        [Test]
        public async Task Run_ThrowingAction_IsReportedAndOtherTicketsContinue()
        {
            RunnerApiConnection apiConnection = new() { States = CreateStates(), Tickets = CreateTickets(1, 2) };
            RecordingExecutor executor = new() { ThrowForTicketId = 1 };

            List<string> failures = await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(failures[0], Does.Contain("execution failed"));
            Assert.That(executor.ActionIdsOf(2), Is.EqualTo(kBothActionsInSortOrder));
        }

        [Test]
        public async Task Run_TicketInStateWithoutActions_IsIgnored()
        {
            RunnerApiConnection apiConnection = new() { States = CreateStates(), Tickets = [new WfTicket { Id = 5, StateId = kOtherState }] };
            RecordingExecutor executor = new();

            await new WhileSetActionRunner(apiConnection, executor).Run();

            Assert.That(executor.Calls, Is.Empty);
        }

        [Test]
        public void Run_Cancelled_Throws()
        {
            RunnerApiConnection apiConnection = new() { States = CreateStates(), Tickets = CreateTickets(1) };
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await new WhileSetActionRunner(apiConnection, new RecordingExecutor()).Run(cancellation.Token));
        }

        [TestCase(0, WorkflowPhases.request)]
        [TestCase(60, WorkflowPhases.request)]
        [TestCase(120, WorkflowPhases.implementation)]
        [TestCase(249, WorkflowPhases.review)]
        [TestCase(-1, WorkflowWhileSetActionExecutor.kDefaultPhase)]
        public void FindPhaseOfState_UsesLastActivePhaseStartingAtOrBelowTheState(int stateId, WorkflowPhases expectedPhase)
        {
            Dictionary<WorkflowPhases, StateMatrix> phaseMatrices = new()
            {
                [WorkflowPhases.review] = new StateMatrix { Active = true, LowestInputState = 200 },
                [WorkflowPhases.request] = new StateMatrix { Active = true, LowestInputState = 0 },
                [WorkflowPhases.approval] = new StateMatrix { Active = false, LowestInputState = 50 },
                [WorkflowPhases.implementation] = new StateMatrix { Active = true, LowestInputState = 100 }
            };

            Assert.That(WorkflowWhileSetActionExecutor.FindPhaseOfState(phaseMatrices, stateId), Is.EqualTo(expectedPhase));
        }

        [Test]
        public void Executor_AfterDispose_RefusesToExecute()
        {
            WorkflowWhileSetActionExecutor executor = new(new RunnerApiConnection(), new SimulatedGlobalConfig(), new WorkflowTicketLocks());
            executor.Dispose();
            executor.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await executor.Execute(1, kCheckedState, CreateAction(kFirstActionId)));
        }

        [Test]
        public async Task Executor_TicketLockedByActionRequest_SkipsTicketWithoutQueries()
        {
            const long lockedTicketId = 42;
            WorkflowTicketLocks ticketLocks = new();
            RunnerApiConnection apiConnection = new();
            using WorkflowWhileSetActionExecutor executor = new(apiConnection, new SimulatedGlobalConfig(), ticketLocks);
            IDisposable? requestLock = await ticketLocks.TryAcquireAsync(lockedTicketId, "uid=requester,ou=users,dc=test");
            WhileSetActionResult result;
            try
            {
                result = await executor.Execute(lockedTicketId, kCheckedState, CreateAction(kFirstActionId));
                Assert.That(ticketLocks.Count, Is.EqualTo(1));
            }
            finally
            {
                requestLock?.Dispose();
            }

            Assert.Multiple(() =>
            {
                Assert.That(requestLock, Is.Not.Null);
                Assert.That(result, Is.EqualTo(WhileSetActionResult.Skipped));
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(ticketLocks.Count, Is.Zero);
            });
        }

        [Test]
        public void Executor_HoldsTicketLockWhileExecutingAndReleasesItOnFailure()
        {
            const long ticketId = 43;
            WorkflowTicketLocks ticketLocks = new();
            LockProbingApiConnection apiConnection = new(ticketLocks, ticketId);
            using WorkflowWhileSetActionExecutor executor = new(apiConnection, new SimulatedGlobalConfig(), ticketLocks);
            WfStateAction action = CreateAction(kFirstActionId);
            action.Phase = WorkflowPhases.review.ToString();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await executor.Execute(ticketId, kCheckedState, action));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.TicketLockedDuringQuery, Is.True);
                Assert.That(ticketLocks.Count, Is.Zero);
            });
        }

        [Test]
        public async Task Job_WithoutWhileSetActions_ReadsTheCurrentConfigAndCompletes()
        {
            RunnerApiConnection apiConnection = new();
            WhileSetActionJob job = new(apiConnection, new SimulatedGlobalConfig(), new WorkflowTicketLocks());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Is.EqualTo(kConfigAndStatesQueries));
        }

        [Test]
        public void Job_ApiFailure_IsReportedAsJobExecutionException()
        {
            WhileSetActionJob job = new(new ThrowingJobApiConnection(), new SimulatedGlobalConfig(), new WorkflowTicketLocks());

            JobExecutionException? exception = Assert.ThrowsAsync<JobExecutionException>(async () => await job.Execute(null!));

            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        public void Job_ConfigReadFailure_FailsTheRunWithAlertBeforeLoadingStates()
        {
            RunnerApiConnection apiConnection = new() { ConfigReadFails = true, States = CreateStateWithPhaseAction(), Tickets = CreateTickets(1) };
            WhileSetActionJob job = new(apiConnection, new SimulatedGlobalConfig(), new WorkflowTicketLocks());

            Assert.ThrowsAsync<JobExecutionException>(async () => await job.Execute(null!));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries[0], Is.EqualTo(ConfigQueries.getConfigItemsByUser));
                Assert.That(apiConnection.Queries, Does.Not.Contain(RequestQueries.getStates));
                Assert.That(apiConnection.Queries, Does.Contain(MonitorQueries.addAlert));
            });
        }

        [Test]
        public void Job_WorkflowEnvironmentUsesConfigChangedAfterMiddlewareStart()
        {
            SimulatedGlobalConfig startupConfig = new() { DefaultLanguage = GlobalConst.kEnglish };
            RunnerApiConnection apiConnection = new()
            {
                ConfigItems = kChangedDefaultLanguage,
                States = CreateStateWithPhaseAction(),
                Tickets = CreateTickets(1, 2)
            };
            WhileSetActionJob job = new(apiConnection, startupConfig, new WorkflowTicketLocks());

            // without an internal LDAP the actions fail after the user config of the workflow environment was built
            Assert.ThrowsAsync<JobExecutionException>(async () => await job.Execute(null!));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries[0], Is.EqualTo(ConfigQueries.getConfigItemsByUser), "config read at the start of the run");
                Assert.That(apiConnection.CustomTextLanguages, Is.EqualTo(kGermanOnly), "one user config for the run, built from the changed config");
                Assert.That(startupConfig.DefaultLanguage, Is.EqualTo(GlobalConst.kEnglish), "injected config unchanged");
            });
        }

        [Test]
        public void SchedulerService_UsesConfiguredMinutes()
        {
            SimulatedGlobalConfig globalConfig = new() { WhileSetActionSleepTime = 15 };
            WhileSetActionSchedulerService service = new(null!, new RunnerApiConnection(), globalConfig, new NotStartingLifetime());

            Assert.Multiple(() =>
            {
                Assert.That(GetProtected<int>(service, "SleepTime"), Is.EqualTo(15));
                Assert.That(GetProtected<TimeSpan>(service, "Interval"), Is.EqualTo(TimeSpan.FromMinutes(15)));
                Assert.That(GetProtected<string>(service, "IntervalLogSuffix"), Is.EqualTo("m"));
            });
        }

        [Test]
        public void ConfigData_DefaultsToTenMinutes()
        {
            Assert.That(new FWO.Config.Api.Data.ConfigData().WhileSetActionSleepTime, Is.EqualTo(10));
        }

        private static TValue GetProtected<TValue>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(instance.GetType().Name, propertyName);
            return (TValue)property.GetValue(instance)!;
        }

        private sealed class NotStartingLifetime : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;

            public void StopApplication()
            { }
        }

        private sealed record ExecutorCall(long TicketId, int StateId, int ActionId);

        private sealed class RecordingExecutor : IWhileSetActionExecutor
        {
            public List<ExecutorCall> Calls { get; } = [];
            public Dictionary<(long TicketId, int ActionId), WhileSetActionResult> Results { get; } = [];
            public long? ThrowForTicketId { get; init; }

            public Task<WhileSetActionResult> Execute(long ticketId, int stateId, WfStateAction action)
            {
                Calls.Add(new ExecutorCall(ticketId, stateId, action.Id));
                if (ThrowForTicketId == ticketId)
                {
                    throw new InvalidOperationException("execution failed");
                }
                return Task.FromResult(Results.TryGetValue((ticketId, action.Id), out WhileSetActionResult result) ? result : WhileSetActionResult.Stayed);
            }

            public List<int> ActionIdsOf(long ticketId)
            {
                return [.. Calls.Where(call => call.TicketId == ticketId).Select(call => call.ActionId)];
            }
        }

        /// <summary>
        /// Fails every query, after recording whether the ticket lock was held by someone else at that moment.
        /// </summary>
        private sealed class LockProbingApiConnection(WorkflowTicketLocks ticketLocks, long ticketId) : JobTestApiConnectionBase
        {
            public bool? TicketLockedDuringQuery { get; private set; }

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables,
                string? operationName, QueryChunkingOptions? chunkingOptions)
            {
                using IDisposable? probeLock = ticketLocks.TryAcquireImmediately(ticketId, "probe");
                TicketLockedDuringQuery ??= probeLock == null;
                throw new InvalidOperationException("query failed");
            }
        }

        private sealed class RunnerApiConnection : JobTestApiConnectionBase
        {
            private static readonly List<string> kAlertQueries = [MonitorQueries.addLogEntry, MonitorQueries.getOpenAlerts, MonitorQueries.addAlert];

            public List<WfState> States { get; init; } = [];
            public List<WfTicket> Tickets { get; init; } = [];
            public ConfigItem[] ConfigItems { get; init; } = [];
            public bool ConfigReadFails { get; init; }
            public List<int>? TicketStateIds { get; private set; }
            public List<string> CustomTextLanguages { get; } = [];

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables,
                string? operationName, QueryChunkingOptions? chunkingOptions)
            {
                if (query == ConfigQueries.getConfigItemsByUser && ConfigReadFails)
                {
                    throw new InvalidOperationException("config read failed");
                }
                if (query == ConfigQueries.getCustomTextsPerLanguage)
                {
                    CustomTextLanguages.Add(GetVariable<string>(variables, "language"));
                }
                if (kAlertQueries.Contains(query))
                {
                    return ReturnEmptyOrDefault<QueryResponseType>();
                }
                return Task.FromResult((QueryResponseType)GetResponse(query, variables));
            }

            private object GetResponse(string query, object? variables)
            {
                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return ConfigItems;
                }
                if (query == ConfigQueries.getCustomTextsPerLanguage)
                {
                    return new List<UiText>();
                }
                if (query == AuthQueries.getLdapConnections)
                {
                    // no internal LDAP, so no workflow handler can be built
                    return new List<Ldap>();
                }
                if (query == RequestQueries.getStates)
                {
                    return States;
                }
                if (query == RequestQueries.getTicketIdsByStates)
                {
                    TicketStateIds = GetVariable<List<int>>(variables, "stateIds");
                    return Tickets;
                }
                throw new AssertionException($"Unexpected query: {query}");
            }

            private static TValue GetVariable<TValue>(object? variables, string name)
            {
                return (TValue)variables!.GetType().GetProperty(name)!.GetValue(variables)!;
            }
        }
    }
}

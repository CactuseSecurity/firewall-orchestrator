using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the conditional auto-forward with error conditions, the rollout check and the target "remain in state".
    /// </summary>
    [TestFixture]
    internal class ActionHandlerConditionalAutoPromoteTest
    {
        private const int kCheckedState = 1;
        private const int kPassedState = 2;
        private const int kErrorState = 3;
        private const int kNotPassedState = 4;
        private const long kTicketId = 42;
        private const int kTimeoutMinutes = 60;
        private const string kResultLabel = "rollout_ok";
        private const string kActionName = "Rollout check";
        private const string kDateFormat = "yyyy-MM-dd HH:mm:ss";
        private const ToBeCalled kUnsupportedCheck = (ToBeCalled)999;
        private static readonly DateTimeOffset kNow = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateTime kImplementationDone = kNow.LocalDateTime.AddMinutes(-30);
        private static readonly List<int> kSuperManagementOnly = [7];
        private static readonly List<int> kRuleImportTypes = [FWO.Basics.ImportType.RULE, FWO.Basics.ImportType.ADMIN_VIA_REINITIALIZE_BTN];
        private static readonly List<WfState> kStates =
        [
            new() { Id = kCheckedState },
            new() { Id = kPassedState },
            new() { Id = kErrorState },
            new() { Id = kNotPassedState }
        ];

        private static string CreateParams(ToBeCalled toBeCalled, int ifNotPassedState, int? errorState = kErrorState,
            int? timeoutMinutes = kTimeoutMinutes, string checkResultLabel = "", int ifPassedState = kPassedState)
        {
            return JsonSerializer.Serialize(new ConditionalAutoPromoteParams
            {
                ToBeCalled = toBeCalled,
                CheckResultLabel = checkResultLabel,
                IfCompliantState = ifPassedState,
                IfNotCompliantState = ifNotPassedState,
                ErrorState = errorState,
                ErrorConditions = new ConditionalErrorConditions { TimeoutMinutes = timeoutMinutes }
            });
        }

        private static WfTicket CreateTicket(int? managementId = 1)
        {
            WfReqTask reqTask = new() { Id = 5, TicketId = kTicketId, ManagementId = managementId, StateId = kCheckedState };
            reqTask.ImplementationTasks.Add(new WfImplTask { Id = 6, ManagementId = managementId, Stop = kImplementationDone, StateId = kCheckedState });
            WfTicket ticket = new() { Id = kTicketId, StateId = kCheckedState };
            ticket.Tasks.Add(reqTask);
            return ticket;
        }

        private static WfTicket CreatePolicyCheckTicket()
        {
            WfReqTask reqTask = new() { Id = 5, TicketId = kTicketId, ManagementId = 1, StateId = kCheckedState };
            reqTask.Elements.Add(new WfReqElement { Field = nameof(ElemFieldType.source), IpString = "10.0.0.1/32", Name = "src" });
            reqTask.Elements.Add(new WfReqElement { Field = nameof(ElemFieldType.destination), IpString = "10.0.1.1/32", Name = "dst" });
            reqTask.Elements.Add(new WfReqElement { Field = nameof(ElemFieldType.service), Port = 443, ProtoId = 6, Name = "https" });
            WfTicket ticket = new() { Id = kTicketId, StateId = kCheckedState };
            ticket.Tasks.Add(reqTask);
            return ticket;
        }

        private static WfStateChangeExecution EnteredMinutesAgo(int minutes, int toStateId = kCheckedState)
        {
            return new WfStateChangeExecution
            {
                ObjectScope = nameof(WfObjectScopes.Ticket),
                ObjectId = kTicketId,
                ToStateId = toStateId,
                ExecutedAt = kNow.UtcDateTime.AddMinutes(-minutes)
            };
        }

        private static ImportControl ImportAfterImplementation()
        {
            DateTime start = kImplementationDone.AddMinutes(5);
            return new ImportControl { MgmId = 1, StartTime = start, StopTime = start.AddMinutes(1), SuccessfulImport = true };
        }

        private static async Task<WfTicket> Perform(ConditionalApiConnection apiConnection, string externalParams, WfTicket? ticket = null,
            IRequestedRulePolicyChecker? policyChecker = null)
        {
            WfHandler wfHandler = new((_, _, _, _) => { }, new ConditionalTextConfig(), new ClaimsPrincipal(), apiConnection,
                new MiddlewareClient("http://localhost/"), WorkflowPhases.request);
            ActionHandler handler = new(apiConnection, wfHandler, null, true, policyChecker) { Clock = new FixedTimeProvider(kNow) };
            await handler.Init();
            WfTicket checkedTicket = ticket ?? CreateTicket();
            WfStateAction action = new()
            {
                Name = kActionName,
                ActionType = nameof(StateActionTypes.AutoPromote),
                Scope = nameof(WfObjectScopes.Ticket),
                Event = nameof(StateActionEvents.WhileSet),
                ExternalParams = externalParams
            };

            await handler.PerformAction(action, checkedTicket, WfObjectScopes.Ticket, null, checkedTicket.Id);
            return checkedTicket;
        }

        private static string Format(DateTime time)
        {
            return time.ToString(kDateFormat, CultureInfo.InvariantCulture);
        }

        private static string TimeoutComment()
        {
            string enteredAt = Format(kNow.UtcDateTime.AddMinutes(-(kTimeoutMinutes + 1)).ToLocalTime());
            return $"timeout {kTimeoutMinutes} since {enteredAt}";
        }

        [Test]
        public async Task TimedOut_AllImported_MovesIntoPassedState()
        {
            ConditionalApiConnection apiConnection = new()
            {
                StateChanges = [EnteredMinutesAgo(kTimeoutMinutes + 1)],
                Imports = [ImportAfterImplementation()]
            };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kPassedState));
                Assert.That(ticket.Comments.Single().Comment.CommentText, Is.EqualTo($"ok {Format(kImplementationDone)}: MDS"));
            });
        }

        [Test]
        public async Task TimedOut_ImportsMissing_MovesIntoErrorStateWithTimeoutAndMissingManagements()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(kTimeoutMinutes + 1)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kErrorState));
                Assert.That(ticket.Comments.Single().Comment.CommentText,
                    Is.EqualTo($"{TimeoutComment()} missing {Format(kImplementationDone)}: MDS"));
                Assert.That(apiConnection.Queries, Does.Contain(ImportQueries.getLatestSuccessfulImports));
                Assert.That(apiConnection.StateChangeScope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(apiConnection.StateChangeObjectId, Is.EqualTo(kTicketId));
            });
        }

        [Test]
        public async Task TimedOut_PolicyCheckCompliant_MovesIntoPassedState()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(kTimeoutMinutes + 1)] };
            FixedPolicyChecker policyChecker = new(true);

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.PolicyCheck, kNotPassedState),
                CreatePolicyCheckTicket(), policyChecker);

            Assert.Multiple(() =>
            {
                Assert.That(policyChecker.Called, Is.True);
                Assert.That(ticket.StateId, Is.EqualTo(kPassedState));
                Assert.That(ticket.Comments, Is.Empty);
            });
        }

        [Test]
        public async Task TimedOut_PolicyCheckNotCompliant_MovesIntoErrorStateWithTimeout()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(kTimeoutMinutes + 1)] };
            FixedPolicyChecker policyChecker = new(false);

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.PolicyCheck, kNotPassedState),
                CreatePolicyCheckTicket(), policyChecker);

            Assert.Multiple(() =>
            {
                Assert.That(policyChecker.Called, Is.True);
                Assert.That(ticket.StateId, Is.EqualTo(kErrorState));
                Assert.That(ticket.Comments.Single().Comment.CommentText, Is.EqualTo(TimeoutComment()));
            });
        }

        [Test]
        public async Task TimedOut_ErrorStateEqualToCurrentState_RemainsWithoutComment()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(kTimeoutMinutes + 1)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, kNotPassedState, errorState: kCheckedState));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kCheckedState));
                Assert.That(ticket.Comments, Is.Empty);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AutomaticConditionalTarget_RemainsInStateAndLogsWarning(bool allImported)
        {
            List<ImportControl> imports = [];
            if (allImported)
            {
                imports.Add(ImportAfterImplementation());
            }
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(5)], Imports = imports };
            string externalParams = allImported
                ? CreateParams(ToBeCalled.RolloutCheck, kNotPassedState, ifPassedState: WfStateAction.AutomaticStateId)
                : CreateParams(ToBeCalled.RolloutCheck, WfStateAction.AutomaticStateId);
            WfTicket? ticket = null;

            string log = await ConsoleOutput.CaptureAsync(async () => ticket = await Perform(apiConnection, externalParams));

            Assert.Multiple(() =>
            {
                Assert.That(ticket!.StateId, Is.EqualTo(kCheckedState));
                Assert.That(ticket.Comments, Is.Empty);
                Assert.That(log, Does.Contain("Warning - AutoPromote"));
                Assert.That(log, Does.Contain($"Action '{kActionName}' has the conditional target \"automatic\""));
            });
        }

        [Test]
        public async Task TimeoutNotReached_AllImported_MovesIntoPassedStateWithCommentAndLabel()
        {
            ConditionalApiConnection apiConnection = new()
            {
                StateChanges = [EnteredMinutesAgo(kTimeoutMinutes - 1)],
                Imports = [ImportAfterImplementation()]
            };

            WfTicket ticket = await Perform(apiConnection,
                CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId, checkResultLabel: $" {kResultLabel} "));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kPassedState));
                Assert.That(ticket.Comments.Single().Comment.CommentText, Is.EqualTo($"ok {Format(kImplementationDone)}: MDS"));
                Assert.That(ticket.Tasks[0].GetAddInfoValue(kResultLabel), Is.EqualTo("true"));
            });
        }

        [Test]
        public async Task ImportsMissing_RemainTarget_StaysWithoutComment()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(5)] };

            WfTicket ticket = await Perform(apiConnection,
                CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId, checkResultLabel: kResultLabel));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kCheckedState));
                Assert.That(ticket.Comments, Is.Empty);
                Assert.That(ticket.Tasks[0].GetAddInfoValue(kResultLabel), Is.EqualTo("false"));
            });
        }

        [Test]
        public async Task ImportsMissing_NotPassedTarget_MovesWithListOfMissingManagements()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(5)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, kNotPassedState));

            Assert.That(ticket.StateId, Is.EqualTo(kNotPassedState));
            Assert.That(ticket.Comments.Single().Comment.CommentText, Is.EqualTo($"missing {Format(kImplementationDone)}: MDS"));
        }

        [Test]
        public async Task TargetEqualToCurrentState_RemainsWithoutComment()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(5)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, kCheckedState));

            Assert.That(ticket.StateId, Is.EqualTo(kCheckedState));
            Assert.That(ticket.Comments, Is.Empty);
        }

        [Test]
        public async Task StateChangeIntoOtherState_TimeoutDoesNotApply()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(10 * kTimeoutMinutes, toStateId: 99)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId));

            Assert.That(ticket.StateId, Is.EqualTo(kCheckedState));
        }

        [Test]
        public async Task StateEntryCannotBeRead_TimeoutDoesNotApplyAndCheckRuns()
        {
            ConditionalApiConnection apiConnection = new() { ThrowOnStateChange = true, Imports = [ImportAfterImplementation()] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId));

            Assert.That(ticket.StateId, Is.EqualTo(kPassedState));
        }

        [Test]
        public async Task WithoutErrorState_ErrorConditionsAreNotChecked()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(10 * kTimeoutMinutes)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(kUnsupportedCheck, kNotPassedState, errorState: null));

            Assert.That(ticket.StateId, Is.EqualTo(kNotPassedState));
            Assert.That(apiConnection.Queries, Does.Not.Contain(RequestQueries.getStateChangeExecution));
        }

        [Test]
        public async Task WithoutTimeout_StateEntryIsNotRead()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(10 * kTimeoutMinutes)] };

            WfTicket ticket = await Perform(apiConnection, CreateParams(kUnsupportedCheck, kNotPassedState, timeoutMinutes: null));

            Assert.That(ticket.StateId, Is.EqualTo(kNotPassedState));
            Assert.That(apiConnection.Queries, Does.Not.Contain(RequestQueries.getStateChangeExecution));
        }

        [Test]
        public async Task FailingRolloutCheck_IsTreatedAsNotPassedWithoutComment()
        {
            ConditionalApiConnection apiConnection = new() { StateChanges = [EnteredMinutesAgo(5)], ThrowOnManagements = true };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, kNotPassedState));

            Assert.That(ticket.StateId, Is.EqualTo(kNotPassedState));
            Assert.That(ticket.Comments, Is.Empty);
        }

        [Test]
        public async Task TicketWithoutManagements_PassesWithoutLoadingImports()
        {
            ConditionalApiConnection apiConnection = new();

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId), CreateTicket(null));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kPassedState));
                Assert.That(ticket.Comments.Single().Comment.CommentText, Is.EqualTo("ok without managements"));
                Assert.That(apiConnection.Queries, Does.Not.Contain(DeviceQueries.getManagementHierarchy));
                Assert.That(apiConnection.Queries, Does.Not.Contain(ImportQueries.getLatestSuccessfulImports));
            });
        }

        [Test]
        public async Task RolloutCheck_LoadsImportsOfSuperManagementForRuleImports()
        {
            ConditionalApiConnection apiConnection = new()
            {
                Managements = [new() { Id = 1, Name = "Domain A", SuperManagerId = 7 }],
                Imports = [new ImportControl { MgmId = 7, StartTime = kImplementationDone.AddMinutes(1), SuccessfulImport = true }]
            };

            WfTicket ticket = await Perform(apiConnection, CreateParams(ToBeCalled.RolloutCheck, WfStateAction.RemainStateId));

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(kPassedState));
                Assert.That(apiConnection.ImportManagementIds, Is.EqualTo(kSuperManagementOnly));
                Assert.That(apiConnection.ImportTypeIds, Is.EqualTo(kRuleImportTypes));
            });
        }

        [TestCase(null, false)]
        [TestCase(0, false)]
        [TestCase(-5, false)]
        [TestCase(1, true)]
        public void ErrorConditions_HasTimeoutOnlyForPositiveMinutes(int? timeoutMinutes, bool expected)
        {
            Assert.That(new ConditionalErrorConditions { TimeoutMinutes = timeoutMinutes }.HasTimeout, Is.EqualTo(expected));
        }

        [Test]
        public void ConditionalParams_ParseErrorStateAndConditions_AndDefaultMissingConditions()
        {
            string withConditions = "{\"to_be_called\":\"RolloutCheck\",\"if_compliant_state\":2,\"if_not_compliant_state\":-3,"
                + "\"error_state\":3,\"error_conditions\":{\"timeout_minutes\":240}}";
            string withNullConditions = "{\"to_be_called\":\"PolicyCheck\",\"if_compliant_state\":2,\"if_not_compliant_state\":4,\"error_conditions\":null}";

            Assert.That(WfStateAction.TryParseAutoPromoteParams(withConditions, out _, out ConditionalAutoPromoteParams? parsed), Is.True);
            Assert.That(WfStateAction.TryParseAutoPromoteParams(withNullConditions, out _, out ConditionalAutoPromoteParams? parsedWithoutConditions), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(parsed!.ToBeCalled, Is.EqualTo(ToBeCalled.RolloutCheck));
                Assert.That(parsed.IfNotCompliantState, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(parsed.ErrorState, Is.EqualTo(3));
                Assert.That(parsed.ErrorConditions.TimeoutMinutes, Is.EqualTo(240));
                Assert.That(parsedWithoutConditions!.ErrorState, Is.Null);
                Assert.That(parsedWithoutConditions.ErrorConditions.HasTimeout, Is.False);
            });
        }

        [Test]
        public void Decision_RemainsOnlyForRemainTarget()
        {
            Assert.That(AutoPromoteDecision.Remain.RemainsInState, Is.True);
            Assert.That(new AutoPromoteDecision(kPassedState).RemainsInState, Is.False);
            Assert.That(new AutoPromoteDecision(null).RemainsInState, Is.False);
        }

        [Test]
        public void Decision_AutomaticTargetRemainsAndIsMarked()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AutoPromoteDecision.RemainForAutomaticTarget.RemainsInState, Is.True);
                Assert.That(AutoPromoteDecision.RemainForAutomaticTarget.HasAutomaticTarget, Is.True);
                Assert.That(AutoPromoteDecision.Remain.HasAutomaticTarget, Is.False);
            });
        }

        private sealed class FixedPolicyChecker(bool compliant) : IRequestedRulePolicyChecker
        {
            public bool Called { get; private set; }

            public Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
            {
                Called = true;
                return Task.FromResult(compliant);
            }
        }

        private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return utcNow;
            }
        }

        private sealed class ConditionalTextConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key switch
                {
                    ActionHandler.kAutoPromoteTimeoutTextKey => "timeout {0} since {1}",
                    RolloutCheckComment.kPassedTextKey => "ok {0}: {1}",
                    RolloutCheckComment.kPassedWithoutManagementsTextKey => "ok without managements",
                    RolloutCheckComment.kMissingTextKey => "missing {0}: {1}",
                    _ => base.GetText(key)
                };
            }
        }

        private sealed class ConditionalApiConnection : JobTestApiConnectionBase
        {
            public List<WfStateChangeExecution> StateChanges { get; init; } = [];
            public List<Management> Managements { get; init; } = [new() { Id = 1, Name = "MDS" }];
            public List<ImportControl> Imports { get; init; } = [];
            public bool ThrowOnStateChange { get; init; }
            public bool ThrowOnManagements { get; init; }
            public string? StateChangeScope { get; private set; }
            public long? StateChangeObjectId { get; private set; }
            public List<int>? ImportManagementIds { get; private set; }
            public List<int>? ImportTypeIds { get; private set; }

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables,
                string? operationName, QueryChunkingOptions? chunkingOptions)
            {
                if (query == RequestQueries.getStates)
                {
                    return Respond<QueryResponseType>(kStates);
                }
                if (query == RequestQueries.getStateChangeExecution)
                {
                    if (ThrowOnStateChange)
                    {
                        throw new InvalidOperationException("state change record not available");
                    }
                    StateChangeScope = GetVariable<string>(variables, "objectScope");
                    StateChangeObjectId = GetVariable<long>(variables, "objectId");
                    return Respond<QueryResponseType>(StateChanges);
                }
                if (query == DeviceQueries.getManagementHierarchy)
                {
                    if (ThrowOnManagements)
                    {
                        throw new InvalidOperationException("management hierarchy not available");
                    }
                    return Respond<QueryResponseType>(Managements);
                }
                if (query == ImportQueries.getLatestSuccessfulImports)
                {
                    ImportManagementIds = GetVariable<List<int>>(variables, "mgmIds");
                    ImportTypeIds = GetVariable<List<int>>(variables, "importTypeIds");
                    return Respond<QueryResponseType>(Imports);
                }
                return ReturnEmptyOrDefault<QueryResponseType>();
            }

            private static Task<QueryResponseType> Respond<QueryResponseType>(object response)
            {
                return Task.FromResult((QueryResponseType)response);
            }

            private static TValue GetVariable<TValue>(object? variables, string propertyName)
            {
                return (TValue)variables!.GetType().GetProperty(propertyName)!.GetValue(variables)!;
            }
        }
    }
}

using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ActionHandlerTest
    {
        private sealed class ActionHandlerTestApiConn : SimulatedApiConnection
        {
            public List<WfState> States { get; set; } = [];
            public List<string> Queries { get; } = [];
            private readonly List<Management> managements = [new() { Id = 1, Name = "Mgmt1" }];
            private readonly List<Rule> rules =
            [
                new Rule
                {
                    Id = 1,
                    Uid = "rule-1",
                    MgmtId = 1,
                    Action = "accept",
                    Services = [new ServiceWrapper { Content = new NetworkService { Uid = "svc-pass", Name = "SvcPass" } }]
                }
            ];
            private readonly CompliancePolicy compliantPolicy = new()
            {
                Id = 5,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion { Id = 1, Name = "PassPolicy", CriterionType = nameof(CriterionType.ForbiddenService), Content = "svc-deny" }
                    }
                ]
            };
            private readonly CompliancePolicy nonCompliantPolicy = new()
            {
                Id = 9,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion { Id = 2, Name = "FailPolicy", CriterionType = nameof(CriterionType.ForbiddenService), Content = "svc-pass" }
                    }
                ]
            };

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
                if (query == DeviceQueries.getManagementNames)
                {
                    return Task.FromResult((T)(object)managements);
                }
                if (query == RuleQueries.countActiveRules)
                {
                    return Task.FromResult((T)(object)new AggregateCount { Aggregate = new Aggregate { Count = rules.Count } });
                }
                if (query == RuleQueries.getRulesForSelectedManagements || query == RuleQueries.getRuleDetailsById)
                {
                    return Task.FromResult((T)(object)rules);
                }
                if (query == ImportQueries.getMaxImportId)
                {
                    return Task.FromResult((T)(object)new Import());
                }
                if (query == ComplianceQueries.getPolicyById)
                {
                    int policyId = GetVariable<int>(variables, "id");
                    return Task.FromResult((T)(object)(policyId == compliantPolicy.Id ? compliantPolicy : nonCompliantPolicy));
                }
                throw new AssertionException($"Unexpected query: {query}");
            }

            private static TValue GetVariable<TValue>(object? variables, string propertyName)
            {
                PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
                return property != null ? (TValue)property.GetValue(variables)! : default!;
            }
        }

        private sealed class ActionHandlerTestPolicyChecker : IRequestedRulePolicyChecker
        {
            public bool Result { get; set; }
            public List<int>? PolicyIds { get; private set; }
            public List<long>? RequestTaskIds { get; private set; }

            public Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
            {
                PolicyIds = policyIds.ToList();
                RequestTaskIds = requestTasks.Select(task => task.Id).ToList();
                return Task.FromResult(Result);
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

        private static WfTicket CreateTicket(params WfReqTask[] tasks)
        {
            return new WfTicket
            {
                StateId = 1,
                Tasks = [.. tasks]
            };
        }

        private static WfReqTask CreateEligibleRequestTask(long id, int managementId = 1, string title = "Allow request")
        {
            return new WfReqTask
            {
                Id = id,
                ManagementId = managementId,
                Title = title,
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "src" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1/32", Name = "dst" },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "https" },
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = $"rule-{id}" }
                ]
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

        [Test]
        public async Task PerformAction_AutoPromoteConditionalPolicyCheckConfigured_PromotesToOkState()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState { Id = 1 },
                new WfState { Id = 2 },
                new WfState { Id = 3 }
            ];
            SimulatedGlobalConfig globalConfig = new() { ComplianceCheckRelevantManagements = "1" };
            ActionHandlerTestPolicyChecker policyChecker = new() { Result = true };
            WfHandler wfHandler = new((_, _, _, _) => { }, new UserConfig(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, policyChecker);
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = CreateTicket(CreateEligibleRequestTask(11));
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[5,9],\"if_compliant_state\":2,\"if_not_compliant_state\":3}"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ticket.StateId, Is.EqualTo(2));
            Assert.That(policyChecker.PolicyIds, Is.EqualTo(new List<int> { 5, 9 }));
            Assert.That(policyChecker.RequestTaskIds, Is.EqualTo(new List<long> { 11 }));
        }

        [Test]
        public async Task PerformAction_AutoPromoteConditionalPolicyCheckMissing_PromotesToNotOkState()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState { Id = 1 },
                new WfState { Id = 2 },
                new WfState { Id = 3 }
            ];
            SimulatedGlobalConfig globalConfig = new() { ComplianceCheckRelevantManagements = "1" };
            ActionHandlerTestPolicyChecker policyChecker = new() { Result = false };
            WfHandler wfHandler = new((_, _, _, _) => { }, new UserConfig(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, policyChecker);
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = CreateTicket(CreateEligibleRequestTask(12));
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[9],\"check_result_label\":\"policy_check\",\"if_compliant_state\":2,\"if_not_compliant_state\":3}"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ticket.StateId, Is.EqualTo(3));
            Assert.That(ticket.Tasks[0].GetAddInfoValue("policy_check"), Is.EqualTo("false"));
            Assert.That(policyChecker.RequestTaskIds, Is.EqualTo(new List<long> { 12 }));
        }

        [Test]
        public async Task PerformAction_AutoPromoteConditionalPolicyCheckWithLabel_PersistsTrueResult()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States = [new WfState { Id = 1 }, new WfState { Id = 2 }, new WfState { Id = 3 }];
            SimulatedGlobalConfig globalConfig = new() { ComplianceCheckRelevantManagements = "1" };
            WfHandler wfHandler = new((_, _, _, _) => { }, new UserConfig(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, new ActionHandlerTestPolicyChecker() { Result = true });
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = CreateTicket(CreateEligibleRequestTask(13));
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[5],\"check_result_label\":\"policy_check\",\"if_compliant_state\":2,\"if_not_compliant_state\":3}"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ticket.StateId, Is.EqualTo(2));
            Assert.That(ticket.Tasks[0].GetAddInfoValue("policy_check"), Is.EqualTo("true"));
        }

        [Test]
        public async Task PerformAction_AutoPromoteConditionalPolicyCheckWithoutLabel_DoesNotWriteAdditionalInfo()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States = [new WfState { Id = 1 }, new WfState { Id = 2 }, new WfState { Id = 3 }];
            SimulatedGlobalConfig globalConfig = new() { ComplianceCheckRelevantManagements = "1" };
            WfHandler wfHandler = new((_, _, _, _) => { }, new UserConfig(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, new ActionHandlerTestPolicyChecker() { Result = true });
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfTicket ticket = CreateTicket(CreateEligibleRequestTask(14));
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[5],\"check_result_label\":\"\",\"if_compliant_state\":2,\"if_not_compliant_state\":3}"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ticket.Tasks[0].AdditionalInfo, Is.Null.Or.Empty);
        }

        [Test]
        public async Task PerformAction_AutoPromoteConditionalPolicyCheck_OnlyEligibleTasksAreCheckedAndLabelled()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States = [new WfState { Id = 1 }, new WfState { Id = 2 }, new WfState { Id = 3 }];
            SimulatedGlobalConfig globalConfig = new() { ComplianceCheckRelevantManagements = "1" };
            ActionHandlerTestPolicyChecker policyChecker = new() { Result = true };
            WfHandler wfHandler = new((_, _, _, _) => { }, new UserConfig(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, policyChecker);
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfReqTask eligibleTask = CreateEligibleRequestTask(15);
            WfReqTask ineligibleTask = new()
            {
                Id = 16,
                ManagementId = 1,
                Title = "Incomplete request",
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "src" },
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-incomplete" }
                ]
            };
            WfTicket ticket = CreateTicket(eligibleTask, ineligibleTask);
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[5],\"check_result_label\":\"policy_check\",\"if_compliant_state\":2,\"if_not_compliant_state\":3}"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(policyChecker.RequestTaskIds, Is.EqualTo(new List<long> { 15 }));
            Assert.That(eligibleTask.GetAddInfoValue("policy_check"), Is.EqualTo("true"));
            Assert.That(ineligibleTask.GetAddInfoValue("policy_check"), Is.EqualTo(""));
        }

        [Test]
        public async Task PerformAction_AutoPromoteConditionalPolicyCheck_WorksWithoutMiddlewareClient()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States = [new WfState { Id = 1 }, new WfState { Id = 2 }, new WfState { Id = 3 }];
            SimulatedGlobalConfig globalConfig = new() { ComplianceCheckRelevantManagements = "1" };
            WfHandler wfHandler = new(new UserConfig(globalConfig, false), apiConn, WorkflowPhases.request, null, new ActionHandlerTestPolicyChecker() { Result = true });
            ActionHandler handler = new(apiConn, wfHandler, null, true);
            await handler.Init();
            WfTicket ticket = CreateTicket(CreateEligibleRequestTask(17));
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[5],\"check_result_label\":\"policy_check\",\"if_compliant_state\":2,\"if_not_compliant_state\":3}"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ticket.StateId, Is.EqualTo(2));
            Assert.That(ticket.Tasks[0].GetAddInfoValue("policy_check"), Is.EqualTo("true"));
        }

    }
}

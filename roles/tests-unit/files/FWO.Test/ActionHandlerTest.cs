using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class ActionHandlerTest
    {
        private sealed class ActionHandlerTestApiConn : SimulatedApiConnection
        {
            public List<WfState> States { get; set; } = [];
            public List<FwoNotification> Notifications { get; set; } = [];
            public List<ModellingConnection> ConnectionsByTicket { get; set; } = [];
            public Dictionary<int, ModellingConnection> ConnectionsById { get; set; } = [];
            public List<string> Queries { get; } = [];
            public List<object?> Variables { get; } = [];
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
                Variables.Add(variables);
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((T)(object)States);
                }
                if (query == MonitorQueries.addAlert)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper());
                }
                if (query == NotificationQueries.getNotifications)
                {
                    return Task.FromResult((T)(object)Notifications);
                }
                if (query == ModellingQueries.getConnectionsByTicketId)
                {
                    return Task.FromResult((T)(object)ConnectionsByTicket);
                }
                if (query == ModellingQueries.getConnectionById)
                {
                    int id = GetVariable<int>(variables, "id");
                    return Task.FromResult((T)(object)(ConnectionsById.TryGetValue(id, out ModellingConnection? connection)
                        ? new List<ModellingConnection> { connection }
                        : new List<ModellingConnection>()));
                }
                if (query == ModellingQueries.updateConnectionProperties
                    || query == ModellingQueries.updateProposedConnectionOwner
                    || query == ModellingQueries.updateConnectionPublish
                    || query == ModellingQueries.updateNwGroupComment
                    || query == ModellingQueries.updateServiceGroupComment)
                {
                    return Task.FromResult((T)(object)new ReturnId());
                }
                if (query == ModellingQueries.addHistoryEntry)
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

        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(ActionHandler).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ActionHandler).FullName, name);
        }

        private static string? GetScopedUser(ActionHandler handler, string propertyName)
        {
            PropertyInfo? property = typeof(ActionHandler).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            return property != null ? (string?)property.GetValue(handler) : throw new MissingMemberException(typeof(ActionHandler).FullName, propertyName);
        }

        private static void SetMatrix(WfHandler handler, string taskType)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = new StateMatrix();
        }

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            return property != null ? (TValue)property.GetValue(variables)! : default!;
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
        public async Task ResolveActionNotifications_ReturnsInlineNotification_WhenNoNotificationIdsAreConfigured()
        {
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());
            EmailActionParams actionParams = new()
            {
                RecipientTo = EmailRecipientOption.Requester,
                RecipientCC = EmailRecipientOption.CurrentHandler,
                Subject = "subject",
                Body = "body"
            };

            Task<List<FwoNotification>> task = (Task<List<FwoNotification>>)GetPrivateMethod("ResolveActionNotifications").Invoke(handler, [actionParams])!;
            List<FwoNotification> notifications = await task;

            Assert.Multiple(() =>
            {
                Assert.That(notifications, Has.Count.EqualTo(1));
                Assert.That(notifications[0].NotificationClient, Is.EqualTo(NotificationClient.WfAction));
                Assert.That(notifications[0].RecipientTo, Is.EqualTo(EmailRecipientOption.Requester));
                Assert.That(apiConn.Queries, Has.No.Member(NotificationQueries.getNotifications));
            });
        }

        [Test]
        public async Task ResolveActionNotifications_LoadsDistinctReferencedWorkflowNotifications()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                Notifications =
                [
                    new() { Id = 3, NotificationClient = NotificationClient.WfAction, EmailSubject = "third" },
                    new() { Id = 5, NotificationClient = NotificationClient.WfAction, EmailSubject = "fifth" },
                    new() { Id = 8, NotificationClient = NotificationClient.WfAction, EmailSubject = "ignored" }
                ]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            EmailActionParams actionParams = new()
            {
                NotificationIds = [5, 3, 5, 0, -1]
            };

            Task<List<FwoNotification>> task = (Task<List<FwoNotification>>)GetPrivateMethod("ResolveActionNotifications").Invoke(handler, [actionParams])!;
            List<FwoNotification> notifications = await task;

            Assert.Multiple(() =>
            {
                Assert.That(notifications.Select(notification => notification.Id), Is.EqualTo(new[] { 3, 5 }));
                Assert.That(apiConn.Queries, Has.Member(NotificationQueries.getNotifications));
            });
        }

        [Test]
        public void ResolveActionNotifications_Throws_WhenReferencedNotificationIsMissing()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                Notifications = [new() { Id = 3, NotificationClient = NotificationClient.WfAction }]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            EmailActionParams actionParams = new()
            {
                NotificationIds = [3, 7]
            };

            Task<List<FwoNotification>> task = (Task<List<FwoNotification>>)GetPrivateMethod("ResolveActionNotifications").Invoke(handler, [actionParams])!;

            JsonException? exception = Assert.ThrowsAsync<JsonException>(async () => await task);
            Assert.That(exception?.Message, Does.Contain("7"));
        }

        [Test]
        public async Task SetScope_SetsRequesterForToCcAndBcc()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler());
            WfTicket ticket = new()
            {
                Requester = new UiUser { Dn = "uid=requester,ou=users,dc=example,dc=com" }
            };
            FwoNotification notification = new()
            {
                RecipientTo = EmailRecipientOption.Requester,
                RecipientCc = EmailRecipientOption.Requester,
                RecipientBcc = EmailRecipientOption.Requester
            };

            Task task = (Task)GetPrivateMethod("SetScope").Invoke(handler, [ticket, WfObjectScopes.Ticket, notification])!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(GetScopedUser(handler, "ScopedUserTo"), Is.EqualTo("uid=requester,ou=users,dc=example,dc=com"));
                Assert.That(GetScopedUser(handler, "ScopedUserCc"), Is.EqualTo("uid=requester,ou=users,dc=example,dc=com"));
                Assert.That(GetScopedUser(handler, "ScopedUserBcc"), Is.EqualTo("uid=requester,ou=users,dc=example,dc=com"));
            });
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

        [Test]
        public async Task PerformAction_UpdateModelling_UpdatesAccessConnectionsAndGroupTasks()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                ConnectionsByTicket =
                [
                    new() { Id = 41, Properties = "{\"existing\":\"\"}" },
                    new() { Id = 42 }
                ]
            };
            WfHandler wfHandler = new() { AuthUser = new System.Security.Claims.ClaimsPrincipal() };
            SetMatrix(wfHandler, WfTaskType.access.ToString());
            ActionHandler handler = new(apiConn, wfHandler);
            WfReqTask accessTask = new() { Id = 1, TaskType = WfTaskType.access.ToString() };
            accessTask.SetAddInfo(AdditionalInfoKeys.ConnId, "41");
            WfReqTask ignoredConnectionTask = new() { Id = 2, TaskType = WfTaskType.new_interface.ToString() };
            ignoredConnectionTask.SetAddInfo(AdditionalInfoKeys.ConnId, "42");
            WfReqTask appRoleTask = new() { Id = 3, TaskType = WfTaskType.group_create.ToString() };
            appRoleTask.SetAddInfo(AdditionalInfoKeys.AppRoleId, "501");
            WfReqTask serviceGroupTask = new() { Id = 4, TaskType = WfTaskType.group_modify.ToString() };
            serviceGroupTask.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "601");
            WfReqTask ignoredGroupTask = new() { Id = 5, TaskType = WfTaskType.access.ToString() };
            ignoredGroupTask.SetAddInfo(AdditionalInfoKeys.AppRoleId, "502");
            WfTicket ticket = CreateTicket(accessTask, ignoredConnectionTask, appRoleTask, serviceGroupTask, ignoredGroupTask);
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.UpdateModelling.ToString(),
                ExternalParams = "Implemented"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket, ticketId: 77);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getConnectionsByTicketId), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateConnectionProperties), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateNwGroupComment), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateServiceGroupComment), Is.EqualTo(1));
            object? firstConnectionVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateConnectionProperties)];
            Assert.That(GetVariable<int>(firstConnectionVars, "id"), Is.EqualTo(41));
            Assert.That(GetVariable<string>(firstConnectionVars, "connProp"), Does.Contain("\"ImplementationState\":\"Implemented | "));
            object? appRoleVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateNwGroupComment)];
            Assert.That(GetVariable<long>(appRoleVars, "id"), Is.EqualTo(501));
            Assert.That(GetVariable<string>(appRoleVars, "comment"), Does.StartWith("ImplementationState: Implemented | "));
            object? serviceGroupVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateServiceGroupComment)];
            Assert.That(GetVariable<int>(serviceGroupVars, "id"), Is.EqualTo(601));
            Assert.That(GetVariable<string>(serviceGroupVars, "comment"), Does.StartWith("ImplementationState: Implemented | "));
        }

        [Test]
        public async Task PerformAction_UpdateModellingWithRequestTaskScope_OnlyUpdatesSelectedTask()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                ConnectionsById = new()
                {
                    [41] = new ModellingConnection { Id = 41 },
                    [42] = new ModellingConnection { Id = 42 }
                }
            };
            WfHandler wfHandler = new() { AuthUser = new System.Security.Claims.ClaimsPrincipal() };
            SetMatrix(wfHandler, WfTaskType.access.ToString());
            ActionHandler handler = new(apiConn, wfHandler);
            WfReqTask selectedTask = new() { Id = 1, TaskType = WfTaskType.access.ToString() };
            selectedTask.SetAddInfo(AdditionalInfoKeys.ConnId, "41");
            WfReqTask otherTask = new() { Id = 2, TaskType = WfTaskType.access.ToString() };
            otherTask.SetAddInfo(AdditionalInfoKeys.ConnId, "42");
            wfHandler.ActTicket = CreateTicket(selectedTask, otherTask);
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.UpdateModelling.ToString(),
                ExternalParams = "Implemented"
            };

            await handler.PerformAction(action, selectedTask, WfObjectScopes.RequestTask);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getConnectionById), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateConnectionProperties), Is.EqualTo(1));
            object? connectionVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateConnectionProperties)];
            Assert.That(GetVariable<int>(connectionVars, "id"), Is.EqualTo(41));
        }

        [Test]
        public async Task PerformAction_UpdateModellingWithoutState_DoesNotUpdateModel()
        {
            ActionHandlerTestApiConn apiConn = new();
            WfHandler wfHandler = new() { AuthUser = new System.Security.Claims.ClaimsPrincipal() };
            ActionHandler handler = new(apiConn, wfHandler);
            WfReqTask reqTask = new() { Id = 1 };
            reqTask.SetAddInfo(AdditionalInfoKeys.ConnId, "41");
            WfTicket ticket = CreateTicket(reqTask);
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.UpdateModelling.ToString(),
                ExternalParams = ""
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(apiConn.Queries, Has.No.Member(ModellingQueries.updateConnectionProperties));
            Assert.That(apiConn.Queries, Has.No.Member(ModellingQueries.updateNwGroupComment));
            Assert.That(apiConn.Queries, Has.No.Member(ModellingQueries.updateServiceGroupComment));
        }

        [Test]
        public async Task UpdateConnectionOwner_UpdatesOnlyRequestedConnectionsAndWritesHistory()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                ConnectionsByTicket =
                [
                    new() { Id = 41, Name = "Requested", IsRequested = true },
                    new() { Id = 42, Name = "Published", IsRequested = false }
                ]
            };
            WfHandler wfHandler = new() { AuthUser = new System.Security.Claims.ClaimsPrincipal() };
            ActionHandler handler = new(apiConn, wfHandler);

            await handler.UpdateConnectionOwner(new FwoOwner { Id = 7 }, 77);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getConnectionsByTicketId), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateProposedConnectionOwner), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.addHistoryEntry), Is.EqualTo(1));
            object? updateVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateProposedConnectionOwner)];
            Assert.That(GetVariable<int>(updateVars, "id"), Is.EqualTo(41));
            Assert.That(GetVariable<int>(updateVars, "propAppId"), Is.EqualTo(7));
            object? historyVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.addHistoryEntry)];
            Assert.That(GetVariable<int?>(historyVars, "appId"), Is.EqualTo(7));
            Assert.That(GetVariable<int>(historyVars, "changeType"), Is.EqualTo((int)ModellingTypes.ChangeType.Update));
            Assert.That(GetVariable<long>(historyVars, "objectId"), Is.EqualTo(41));
        }

        [Test]
        public async Task UpdateConnectionPublish_PublishesRequestedUnpublishedConnectionAndWritesHistory()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                ConnectionsByTicket =
                [
                    new() { Id = 41, Name = "Requested", IsRequested = true, IsPublished = false, ProposedAppId = 7 },
                    new() { Id = 42, Name = "Already published", IsRequested = true, IsPublished = true },
                    new() { Id = 43, Name = "Not requested", IsRequested = false, IsPublished = false }
                ]
            };
            WfHandler wfHandler = new() { AuthUser = new System.Security.Claims.ClaimsPrincipal() };
            ActionHandler handler = new(apiConn, wfHandler);

            await handler.UpdateConnectionPublish(new FwoOwner { Id = 7 }, 77);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateConnectionPublish), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.addHistoryEntry), Is.EqualTo(1));
            object? publishVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateConnectionPublish)];
            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<int>(publishVars, "id"), Is.EqualTo(41));
                Assert.That(GetVariable<bool>(publishVars, "isRequested"), Is.False);
                Assert.That(GetVariable<bool>(publishVars, "isPublished"), Is.True);
                Assert.That(GetVariable<int?>(publishVars, "appId"), Is.EqualTo(7));
                Assert.That(GetVariable<int?>(publishVars, "proposedAppId"), Is.Null);
            });
            object? historyVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.addHistoryEntry)];
            Assert.That(GetVariable<int>(historyVars, "changeType"), Is.EqualTo((int)ModellingTypes.ChangeType.Publish));
            Assert.That(GetVariable<long>(historyVars, "objectId"), Is.EqualTo(41));
        }

        [Test]
        public async Task UpdateConnectionReject_MarksOnlyRequestedConnectionsRejectedAndWritesHistory()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                ConnectionsByTicket =
                [
                    new() { Id = 41, Name = "Requested", IsRequested = true },
                    new() { Id = 42, Name = "Not requested", IsRequested = false }
                ]
            };
            WfHandler wfHandler = new() { AuthUser = new System.Security.Claims.ClaimsPrincipal() };
            ActionHandler handler = new(apiConn, wfHandler);

            await handler.UpdateConnectionReject(new FwoOwner { Id = 7 }, 77);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateConnectionProperties), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.addHistoryEntry), Is.EqualTo(1));
            object? rejectVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateConnectionProperties)];
            Assert.That(GetVariable<int>(rejectVars, "id"), Is.EqualTo(41));
            Assert.That(GetVariable<string>(rejectVars, "connProp"), Does.Contain(ConState.Rejected.ToString()));
            object? historyVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.addHistoryEntry)];
            Assert.That(GetVariable<int>(historyVars, "changeType"), Is.EqualTo((int)ModellingTypes.ChangeType.Reject));
            Assert.That(GetVariable<long>(historyVars, "objectId"), Is.EqualTo(41));
        }

    }
}

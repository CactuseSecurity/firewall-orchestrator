using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using NetTools;
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
            public WfTicket FullTicket { get; set; } = new();
            public List<ModellingConnection> ConnectionsByTicket { get; set; } = [];
            public Dictionary<int, ModellingConnection> ConnectionsById { get; set; } = [];
            public Dictionary<long, ModellingAppRole> AppRolesById { get; set; } = [];
            public Dictionary<int, ModellingServiceGroup> ServiceGroupsById { get; set; } = [];
            public List<ComplianceNetworkZone> MatrixNetworkZones { get; set; } = [];
            public int UpdateNotificationsLastSentAffectedRows { get; set; }
            public List<int> UpdatedNotificationLastSentIds { get; private set; } = [];
            public bool ThrowOnAddAlert { get; set; }
            public bool ThrowOnGetTicketById { get; set; }
            public bool ThrowOnUpdateNotificationsLastSent { get; set; }
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
            private readonly CompliancePolicy matrixPolicy = new()
            {
                Id = 13,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion { Id = 1301, Name = "Matrix", CriterionType = nameof(CriterionType.Matrix) }
                    }
                ]
            };

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((T)(object)States);
                }
                if (query == MonitorQueries.addAlert)
                {
                    if (ThrowOnAddAlert)
                    {
                        throw new InvalidOperationException("alert failed");
                    }
                    return Task.FromResult((T)(object)new ReturnIdWrapper());
                }
                if (query == NotificationQueries.getNotifications)
                {
                    return Task.FromResult((T)(object)Notifications);
                }
                if (query == NotificationQueries.updateNotificationsLastSent)
                {
                    if (ThrowOnUpdateNotificationsLastSent)
                    {
                        throw new InvalidOperationException("last_sent update failed");
                    }
                    UpdatedNotificationLastSentIds = GetVariable<List<int>>(variables, "ids");
                    return Task.FromResult((T)(object)new ReturnId { AffectedRows = UpdateNotificationsLastSentAffectedRows });
                }
                if (query == RequestQueries.getTicketById)
                {
                    if (ThrowOnGetTicketById)
                    {
                        throw new InvalidOperationException("ticket loading failed");
                    }
                    return Task.FromResult((T)(object)FullTicket);
                }
                if (query == ModellingQueries.getConnectionsByTicketId
                    || query == ModellingQueries.getWorkflowConnectionsByTicketId)
                {
                    return Task.FromResult((T)(object)ConnectionsByTicket);
                }
                if (query == ModellingQueries.getConnectionById
                    || query == ModellingQueries.getWorkflowConnectionById)
                {
                    int id = GetVariable<int>(variables, "id");
                    return Task.FromResult((T)(object)(ConnectionsById.TryGetValue(id, out ModellingConnection? connection)
                        ? new List<ModellingConnection> { connection }
                        : new List<ModellingConnection>()));
                }
                if (query == ModellingQueries.getAppRoleById)
                {
                    long id = GetVariable<long>(variables, "id");
                    return Task.FromResult((T)(object)(AppRolesById.TryGetValue(id, out ModellingAppRole? appRole) ? appRole : new ModellingAppRole()));
                }
                if (query == ModellingQueries.getServiceGroupById)
                {
                    int id = GetVariable<int>(variables, "id");
                    return Task.FromResult((T)(object)(ServiceGroupsById.TryGetValue(id, out ModellingServiceGroup? serviceGroup) ? serviceGroup : new ModellingServiceGroup()));
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
                    return Task.FromResult((T)(object)(policyId == compliantPolicy.Id
                        ? compliantPolicy
                        : policyId == matrixPolicy.Id ? matrixPolicy : nonCompliantPolicy));
                }
                if (query == ComplianceQueries.getNetworkZonesForMatrix)
                {
                    return Task.FromResult((T)(object)MatrixNetworkZones);
                }
                if (query == FlowQueries.getFlowSyncNwObjects)
                {
                    return Task.FromResult((T)(object)new List<FlowNwObject>());
                }
                if (query == FlowQueries.getFlowSyncNwGroups)
                {
                    return Task.FromResult((T)(object)new List<FlowNwGroup>());
                }
                if (query == FlowQueries.getFlowSyncSvcObjects)
                {
                    return Task.FromResult((T)(object)new List<FlowSvcObject>());
                }
                if (query == FlowQueries.getFlowSyncSvcGroups)
                {
                    return Task.FromResult((T)(object)new List<FlowSvcGroup>());
                }
                if (query == FlowQueries.getFlowSyncTimeObjects)
                {
                    return Task.FromResult((T)(object)new List<FlowTimeObject>());
                }
                if (query == FlowQueries.getFlowSyncAccesses)
                {
                    return Task.FromResult((T)(object)new List<FlowAccess>());
                }
                if (query == StmQueries.getIpProtocols)
                {
                    return Task.FromResult((T)(object)new List<IpProtocol> { new() { Id = 6, Name = "tcp" }, new() { Id = 17, Name = "udp" } });
                }
                if (query == StmQueries.getRuleActions)
                {
                    return Task.FromResult((T)(object)new List<RuleAction> { new() { Id = 1, Name = "accept", Allowed = true } });
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

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(ActionHandler).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
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
            public bool ThrowOnCheck { get; set; }
            public List<int>? PolicyIds { get; private set; }
            public List<long>? RequestTaskIds { get; private set; }

            public Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
            {
                if (ThrowOnCheck)
                {
                    throw new InvalidOperationException("policy check failed");
                }
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
                Id = 1,
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

        private static WfReqTask CreateBundleRequestTask(long id, string sourceIp, string destinationIp, int port = 443)
        {
            return new()
            {
                Id = id,
                TicketId = 7,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                RuleAction = 1,
                ManagementId = 2,
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 5 } }],
                Elements =
                [
                    CreateBundleNetworkElement(id, ElemFieldType.source, sourceIp),
                    CreateBundleNetworkElement(id, ElemFieldType.destination, destinationIp),
                    new()
                    {
                        TaskId = id,
                        Field = ElemFieldType.service.ToString(),
                        ProtoId = 6,
                        Port = port,
                        PortEnd = port,
                        RequestAction = RequestAction.create.ToString()
                    }
                ]
            };
        }

        private static WfReqElement CreateBundleNetworkElement(long taskId, ElemFieldType field, string ip)
        {
            return new()
            {
                TaskId = taskId,
                Field = field.ToString(),
                Cidr = new(ip),
                CidrEnd = new(ip),
                IpString = ip,
                IpEnd = ip,
                RequestAction = RequestAction.create.ToString()
            };
        }

        private static List<ComplianceNetworkZone> CreateBundleNetworkZones()
        {
            return
            [
                new()
                {
                    Id = 1,
                    Name = "Source Zone",
                    IPRanges = [IPAddressRange.Parse("10.0.0.0/24")]
                },
                new()
                {
                    Id = 2,
                    Name = "Destination Zone",
                    IPRanges = [IPAddressRange.Parse("10.0.1.0/24")]
                },
                new()
                {
                    Id = 3,
                    Name = "Other Destination Zone",
                    IPRanges = [IPAddressRange.Parse("10.0.2.0/24")]
                }
            ];
        }

        private static WfImplTask CreateEligibleImplementationTask(long id, string title = "Implement request")
        {
            return new WfImplTask
            {
                Id = id,
                TaskNumber = 4,
                Title = title,
                ImplAction = RequestAction.create.ToString(),
                ImplElements =
                [
                    new WfImplElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "impl-src" },
                    new WfImplElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1/32", Name = "impl-dst" },
                    new WfImplElement { Field = ElemFieldType.service.ToString(), Port = 8443, ProtoId = 6, Name = "impl-https" }
                ]
            };
        }

        [Test]
        public async Task CreateWorkflowEmailContent_ReloadsFullTicketForTicketScope()
        {
            WfReqTask overviewTask = new()
            {
                Id = 12,
                TaskNumber = 2,
                Title = "Open web",
                RequestAction = RequestAction.create.ToString()
            };
            WfReqTask fullTask = CreateEligibleRequestTask(12, title: "Open web");
            fullTask.TaskNumber = overviewTask.TaskNumber;
            WfTicket overviewTicket = CreateTicket(overviewTask);
            overviewTicket.Id = 42;
            WfTicket fullTicket = CreateTicket(fullTask);
            fullTicket.Id = overviewTicket.Id;
            ActionHandlerTestApiConn apiConn = new()
            {
                FullTicket = fullTicket
            };
            WfHandler wfHandler = new()
            {
                userConfig = new SimulatedUserConfig()
            };
            ActionHandler handler = new(apiConn, wfHandler);

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, overviewTicket, WfObjectScopes.Ticket])!;
            WorkflowEmailContent? content = await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Has.Member(RequestQueries.getTicketById));
                Assert.That(content?.PlainText, Does.Contain("Requested Connections"));
                Assert.That(content?.PlainText, Does.Contain("2 | Open web | create | src | dst | https"));
            });
        }

        [Test]
        public async Task CreateWorkflowEmailContent_ReturnsNull_WhenNoAttachedContentIsConfigured()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.None, CreateTicket(CreateEligibleRequestTask(12)), WfObjectScopes.Ticket])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content, Is.Null);
        }

        [Test]
        public async Task CreateWorkflowEmailContent_FallsBackToCurrentTicket_WhenFullTicketReloadFails()
        {
            WfReqTask overviewTask = CreateEligibleRequestTask(12, title: "Fallback task");
            overviewTask.TaskNumber = 3;
            WfTicket overviewTicket = CreateTicket(overviewTask);
            overviewTicket.Id = 42;
            ActionHandlerTestApiConn apiConn = new()
            {
                ThrowOnGetTicketById = true
            };
            ActionHandler handler = new(apiConn, new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, overviewTicket, WfObjectScopes.Ticket])!;
            WorkflowEmailContent? content = await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Has.Member(RequestQueries.getTicketById));
                Assert.That(content?.PlainText, Does.Contain("3 | Fallback task |"));
            });
        }

        [Test]
        public async Task CreateWorkflowEmailContent_UsesRequestTaskForRequestTaskScope()
        {
            WfReqTask reqTask = CreateEligibleRequestTask(12, title: "Request scope task");
            reqTask.TaskNumber = 5;
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, reqTask, WfObjectScopes.RequestTask])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content?.PlainText, Does.Contain("5 | Request scope task |"));
        }

        [Test]
        public async Task CreateWorkflowEmailContent_UsesProtocolNameForUnnamedRequestService()
        {
            WfReqTask reqTask = CreateEligibleRequestTask(12, title: "Request scope task");
            reqTask.TaskNumber = 5;
            WfReqElement serviceElement = reqTask.Elements.First(element => element.Field == ElemFieldType.service.ToString());
            serviceElement.Name = null;
            serviceElement.Port = 443;
            serviceElement.ProtoId = 6;
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, reqTask, WfObjectScopes.RequestTask])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content?.PlainText, Does.Contain("443/tcp"));
        }

        [Test]
        public async Task CreateWorkflowEmailContent_UsesImplementationTaskForImplementationTaskScope()
        {
            WfImplTask implTask = CreateEligibleImplementationTask(22, "Implementation scope task");
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, implTask, WfObjectScopes.ImplementationTask])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content?.PlainText, Does.Contain("4 | Implementation scope task | create | impl-src | impl-dst | impl-https"));
        }

        [Test]
        public async Task CreateWorkflowEmailContent_UsesProtocolNameForUnnamedImplementationService()
        {
            WfImplTask implTask = CreateEligibleImplementationTask(22, "Implementation scope task");
            WfImplElement serviceElement = implTask.ImplElements.First(element => element.Field == ElemFieldType.service.ToString());
            serviceElement.Name = null;
            serviceElement.Port = 8443;
            serviceElement.ProtoId = 6;
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, implTask, WfObjectScopes.ImplementationTask])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content?.PlainText, Does.Contain("8443/tcp"));
        }

        [Test]
        public async Task CreateWorkflowEmailContent_UsesActiveRequestTaskForApprovalScope()
        {
            WfReqTask reqTask = CreateEligibleRequestTask(12, title: "Approval scope task");
            reqTask.TaskNumber = 6;
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler
            {
                userConfig = new SimulatedUserConfig(),
                ActReqTask = reqTask
            });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, new WfApproval { Id = 3 }, WfObjectScopes.Approval])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content?.PlainText, Does.Contain("6 | Approval scope task |"));
        }

        [Test]
        public async Task CreateWorkflowEmailContent_ReturnsNull_ForUnsupportedScope()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() });

            Task<WorkflowEmailContent?> task = (Task<WorkflowEmailContent?>)GetPrivateMethod("CreateWorkflowEmailContent")
                .Invoke(handler, [EmailAttachedContent.RequestedConnections, new WfTicket(), WfObjectScopes.None])!;
            WorkflowEmailContent? content = await task;

            Assert.That(content, Is.Null);
        }

        [Test]
        public void WorkflowPlaceholderObject_UsesActiveTicket_WhenAvailable()
        {
            WfTicket activeTicket = new() { Id = 77 };
            WfReqTask requestTask = new() { Id = 12 };
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { ActTicket = activeTicket });

            WfStatefulObject placeholder = (WfStatefulObject)GetPrivateMethod("WorkflowPlaceholderObject").Invoke(handler, [requestTask])!;

            Assert.That(placeholder, Is.SameAs(activeTicket));
        }

        [Test]
        public void WorkflowPlaceholderObject_UsesStatefulObject_WhenNoActiveTicketExists()
        {
            WfReqTask requestTask = new() { Id = 12 };
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler());

            WfStatefulObject placeholder = (WfStatefulObject)GetPrivateMethod("WorkflowPlaceholderObject").Invoke(handler, [requestTask])!;

            Assert.That(placeholder, Is.SameAs(requestTask));
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
        public async Task SendEmail_DisplaysError_WhenReferencedNotificationIsMissingAndConfirmationIsEnabled()
        {
            List<(Exception? Exception, string Title, bool ErrorFlag)> messages = [];
            WfHandler wfHandler = new(new SimulatedUserConfig(), new ActionHandlerTestApiConn(), WorkflowPhases.request, null,
                displayMessage: (exception, title, _, errorFlag) => messages.Add((exception, title, errorFlag)));
            ActionHandler handler = new(new ActionHandlerTestApiConn(), wfHandler);
            WfStateAction action = new()
            {
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    NotificationIds = [7],
                    ConfirmSentMail = true
                })
            };

            await handler.SendEmail(action, new WfTicket(), WfObjectScopes.Ticket, null);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Exception, Is.TypeOf<JsonException>());
                Assert.That(messages[0].Title, Is.EqualTo("Send Email"));
                Assert.That(messages[0].ErrorFlag, Is.True);
            });
        }

        [Test]
        public void DisplaySentEmailConfirmation_SuppressesZeroSentEmails()
        {
            List<(string Title, string Message, bool ErrorFlag)> messages = [];
            WfHandler wfHandler = new(new SimulatedUserConfig(), new ActionHandlerTestApiConn(), WorkflowPhases.request, null,
                displayMessage: (_, title, message, errorFlag) => messages.Add((title, message, errorFlag)));
            ActionHandler handler = new(new ActionHandlerTestApiConn(), wfHandler);
            EmailActionParams actionParams = new()
            {
                ConfirmSentMail = true
            };

            GetPrivateMethod("DisplaySentEmailConfirmation").Invoke(handler, [actionParams, 0]);

            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void DisplaySentEmailConfirmation_ShowsPositiveSentEmails()
        {
            List<(string Title, string Message, bool ErrorFlag)> messages = [];
            WfHandler wfHandler = new(new SimulatedUserConfig(), new ActionHandlerTestApiConn(), WorkflowPhases.request, null,
                displayMessage: (_, title, message, errorFlag) => messages.Add((title, message, errorFlag)));
            ActionHandler handler = new(new ActionHandlerTestApiConn(), wfHandler);
            EmailActionParams actionParams = new()
            {
                ConfirmSentMail = true
            };

            GetPrivateMethod("DisplaySentEmailConfirmation").Invoke(handler, [actionParams, 2]);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Send Email"));
                Assert.That(messages[0].Message, Is.EqualTo("2 emails sent"));
                Assert.That(messages[0].ErrorFlag, Is.False);
            });
        }

        [Test]
        public void DisplaySentEmailConfirmation_SuppressesConfirmationWhenDisabled()
        {
            List<(string Title, string Message, bool ErrorFlag)> messages = [];
            WfHandler wfHandler = new(new SimulatedUserConfig(), new ActionHandlerTestApiConn(), WorkflowPhases.request, null,
                displayMessage: (_, title, message, errorFlag) => messages.Add((title, message, errorFlag)));
            ActionHandler handler = new(new ActionHandlerTestApiConn(), wfHandler);
            EmailActionParams actionParams = new()
            {
                ConfirmSentMail = false
            };

            GetPrivateMethod("DisplaySentEmailConfirmation").Invoke(handler, [actionParams, 2]);

            Assert.That(messages, Is.Empty);
        }

        [Test]
        public async Task UpdateSentNotificationTimestamps_DeduplicatesPositiveNotificationIds()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                UpdateNotificationsLastSentAffectedRows = 2
            };
            ActionHandler handler = new(apiConn, new WfHandler());

            Task task = (Task)GetPrivateMethod("UpdateSentNotificationTimestamps").Invoke(handler, [new List<int> { 7, 0, 7, -1, 9 }])!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries.Count(query => query == NotificationQueries.updateNotificationsLastSent), Is.EqualTo(1));
                Assert.That(apiConn.UpdatedNotificationLastSentIds, Is.EqualTo(new List<int> { 7, 9 }));
            });
        }

        [Test]
        public async Task UpdateSentNotificationTimestamps_SkipsWhenNoPositiveNotificationIds()
        {
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());

            Task task = (Task)GetPrivateMethod("UpdateSentNotificationTimestamps").Invoke(handler, [new List<int> { 0, -1, 0 }])!;
            await task;

            Assert.That(apiConn.Queries, Has.No.Member(NotificationQueries.updateNotificationsLastSent));
        }

        [Test]
        public void UpdateSentNotificationTimestamps_SwallowsUpdateFailure()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                ThrowOnUpdateNotificationsLastSent = true
            };
            ActionHandler handler = new(apiConn, new WfHandler());

            Assert.DoesNotThrowAsync(async () =>
            {
                Task task = (Task)GetPrivateMethod("UpdateSentNotificationTimestamps").Invoke(handler, [new List<int> { 7 }])!;
                await task;
            });
            Assert.That(apiConn.Queries.Count(query => query == NotificationQueries.updateNotificationsLastSent), Is.EqualTo(1));
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
        public async Task InitWithPreloadedStates_DoesNotFetchStates()
        {
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());

            await handler.Init([new WfState { Id = 1 }]);

            Assert.That(apiConn.Queries, Does.Not.Contain(RequestQueries.getStates));
        }

        [Test]
        public async Task GetRelevantActions_FiltersRequestTaskTypeAndHandlesUnknownState()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States =
                [
                    new WfState
                    {
                        Id = 1,
                        Actions =
                        [
                            new WfStateActionDataHelper
                            {
                                Action = new WfStateAction
                                {
                                    Scope = WfObjectScopes.RequestTask.ToString(),
                                    TaskType = WfTaskType.access.ToString()
                                }
                            },
                            new WfStateActionDataHelper
                            {
                                Action = new WfStateAction
                                {
                                    Scope = WfObjectScopes.RequestTask.ToString(),
                                    TaskType = WfTaskType.group_create.ToString()
                                }
                            }
                        ]
                    }
                ]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();

            List<WfStateAction> matchingActions = (List<WfStateAction>)GetPrivateMethod("GetRelevantActions")
                .Invoke(handler, [new WfReqTask { StateId = 1, TaskType = WfTaskType.access.ToString() }, WfObjectScopes.RequestTask, true])!;
            List<WfStateAction> missingStateActions = (List<WfStateAction>)GetPrivateMethod("GetRelevantActions")
                .Invoke(handler, [new WfTicket { StateId = 99 }, WfObjectScopes.Ticket, true])!;

            Assert.Multiple(() =>
            {
                Assert.That(matchingActions, Has.Count.EqualTo(1));
                Assert.That(matchingActions[0].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
                Assert.That(missingStateActions, Is.Empty);
            });
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
        public async Task DoStateChangeActions_DoesNothingWhenStateDidNotChange()
        {
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());
            WfTicket ticket = new() { StateId = 1 };

            await handler.DoStateChangeActions(ticket, WfObjectScopes.Ticket);

            Assert.That(apiConn.Queries, Is.Empty);
        }

        [Test]
        public void BuildWorkflowActionParameters_IncludesCreationStateChangeFlag()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler());
            WfTicket ticket = new() { Id = 42 };
            ticket.MarkCreatedStateChanged(1);

            WorkflowActionParameters parameters = (WorkflowActionParameters)GetPrivateMethod("BuildWorkflowActionParameters")
                .Invoke(handler, [ticket, WfObjectScopes.Ticket, null, 0])!;

            Assert.Multiple(() =>
            {
                Assert.That(parameters.OldStateId, Is.EqualTo(0));
                Assert.That(parameters.NewStateId, Is.EqualTo(1));
                Assert.That(parameters.StateChangedByCreation, Is.True);
            });
        }

        [Test]
        public void BuildWorkflowActionParameters_UsesScopeSpecificIds()
        {
            WfHandler wfHandler = new() { ActTicket = new WfTicket { Id = 400 } };
            ActionHandler handler = new(new ActionHandlerTestApiConn(), wfHandler);
            WfReqTask reqTask = new() { Id = 11, TicketId = 101, StateId = 2 };
            WfImplTask implTask = new() { Id = 12, TicketId = 102, StateId = 3 };
            WfApproval approval = new() { Id = 13, StateId = 4 };

            WorkflowActionParameters reqParams = (WorkflowActionParameters)GetPrivateMethod("BuildWorkflowActionParameters")
                .Invoke(handler, [reqTask, WfObjectScopes.RequestTask, null, 5])!;
            WorkflowActionParameters implParams = (WorkflowActionParameters)GetPrivateMethod("BuildWorkflowActionParameters")
                .Invoke(handler, [implTask, WfObjectScopes.ImplementationTask, null, 6])!;
            WorkflowActionParameters approvalParams = (WorkflowActionParameters)GetPrivateMethod("BuildWorkflowActionParameters")
                .Invoke(handler, [approval, WfObjectScopes.Approval, null, 7])!;
            WorkflowActionParameters explicitTicketParams = (WorkflowActionParameters)GetPrivateMethod("BuildWorkflowActionParameters")
                .Invoke(handler, [reqTask, WfObjectScopes.RequestTask, 999L, 8])!;

            Assert.Multiple(() =>
            {
                Assert.That(reqParams.ObjectId, Is.EqualTo(11));
                Assert.That(reqParams.TicketId, Is.EqualTo(101));
                Assert.That(reqParams.ActionId, Is.EqualTo(5));
                Assert.That(implParams.ObjectId, Is.EqualTo(12));
                Assert.That(implParams.TicketId, Is.EqualTo(102));
                Assert.That(approvalParams.ObjectId, Is.EqualTo(13));
                Assert.That(approvalParams.TicketId, Is.EqualTo(400));
                Assert.That(explicitTicketParams.TicketId, Is.EqualTo(999));
            });
        }

        [Test]
        public void DisplayWorkflowActionMessages_ForwardsMiddlewareMessagesToUi()
        {
            List<(string Title, string Message, bool ErrorFlag)> messages = [];
            WfHandler wfHandler = new(new SimulatedUserConfig(), new ActionHandlerTestApiConn(), WorkflowPhases.request, null,
                displayMessage: (_, title, message, errorFlag) => messages.Add((title, message, errorFlag)));
            ActionHandler handler = new(new ActionHandlerTestApiConn(), wfHandler);
            List<WorkflowActionMessage> middlewareMessages =
            [
                new() { Title = "Info", Message = "ok", ErrorFlag = false },
                new() { Title = "Warning", Message = "check", ErrorFlag = true }
            ];

            GetPrivateMethod("DisplayWorkflowActionMessages").Invoke(handler, [middlewareMessages]);
            GetPrivateMethod("DisplayWorkflowActionMessages").Invoke(handler, [null]);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(2));
                Assert.That(messages[0].Title, Is.EqualTo("Info"));
                Assert.That(messages[1].ErrorFlag, Is.True);
            });
        }

        [Test]
        public void MiddlewareDelegationKey_UsesHasuraUuidIdentityOrFallback()
        {
            WorkflowActionParameters parameters = new()
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                ActionId = 9,
                ObjectId = 10,
                TicketId = 11,
                OldStateId = 1,
                NewStateId = 2,
                Phase = WorkflowPhases.request.ToString()
            };
            System.Security.Claims.ClaimsPrincipal uuidUser = new(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("x-hasura-uuid", "uuid-1")], "test"));
            System.Security.Claims.ClaimsPrincipal namedUser = new(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "named-user")], "test"));

            string uuidKey = (string)GetPrivateMethod("BuildMiddlewareDelegationKey")
                .Invoke(new ActionHandler(new ActionHandlerTestApiConn(), new WfHandler { AuthUser = uuidUser }), [parameters])!;
            string namedKey = (string)GetPrivateMethod("BuildMiddlewareDelegationKey")
                .Invoke(new ActionHandler(new ActionHandlerTestApiConn(), new WfHandler { AuthUser = namedUser }), [parameters])!;
            string fallbackKey = (string)GetPrivateMethod("BuildMiddlewareDelegationKey")
                .Invoke(new ActionHandler(new ActionHandlerTestApiConn(), new WfHandler()), [parameters])!;

            Assert.Multiple(() =>
            {
                Assert.That(uuidKey, Does.StartWith("uuid-1|"));
                Assert.That(namedKey, Does.StartWith("named-user|"));
                Assert.That(fallbackKey, Does.StartWith("No Auth User|"));
            });
        }

        [Test]
        public void MiddlewareDelegationRegistration_DeduplicatesActiveKeys()
        {
            string key = $"test-{Guid.NewGuid()}";

            bool firstRegistration = (bool)GetPrivateStaticMethod("TryRegisterMiddlewareDelegation").Invoke(null, [key])!;
            bool secondRegistration = (bool)GetPrivateStaticMethod("TryRegisterMiddlewareDelegation").Invoke(null, [key])!;
            GetPrivateStaticMethod("MarkMiddlewareDelegationDone").Invoke(null, [key]);

            Assert.Multiple(() =>
            {
                Assert.That(firstRegistration, Is.True);
                Assert.That(secondRegistration, Is.False);
            });
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
        public async Task DoOnAssignmentActions_ExecutesScopedAndLegacyAssignmentActions()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState
                {
                    Id = 1,
                    Actions =
                    [
                        CreateAction(StateActionEvents.OnAssignment.ToString(), StateActionTypes.SetAlert.ToString(), WfObjectScopes.RequestTask.ToString()),
                        CreateAction(StateActionEvents.OnAssignment.ToString(), StateActionTypes.SetAlert.ToString(), WfObjectScopes.None.ToString())
                    ]
                }
            ];
            WfHandler wfHandler = new();
            ActionHandler handler = new(apiConn, wfHandler);
            await handler.Init();
            WfReqTask task = new() { StateId = 1 };

            await handler.DoOnAssignmentActions(task, WfObjectScopes.RequestTask, "dn=test");

            Assert.That(apiConn.Queries.Count(q => q == MonitorQueries.addAlert), Is.EqualTo(2));
        }

        [Test]
        public async Task DoOnAssignmentActions_DeduplicatesSamePersistedScopedAndLegacyAction()
        {
            ActionHandlerTestApiConn apiConn = new();
            apiConn.States =
            [
                new WfState
                {
                    Id = 1,
                    Actions =
                    [
                        new WfStateActionDataHelper
                        {
                            Action = new WfStateAction
                            {
                                Id = 77,
                                Event = StateActionEvents.OnAssignment.ToString(),
                                ActionType = StateActionTypes.SetAlert.ToString(),
                                Scope = WfObjectScopes.RequestTask.ToString()
                            }
                        },
                        new WfStateActionDataHelper
                        {
                            Action = new WfStateAction
                            {
                                Id = 77,
                                Event = StateActionEvents.OnAssignment.ToString(),
                                ActionType = StateActionTypes.SetAlert.ToString(),
                                Scope = WfObjectScopes.None.ToString()
                            }
                        }
                    ]
                }
            ];
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfReqTask task = new() { StateId = 1 };

            await handler.DoOnAssignmentActions(task, WfObjectScopes.RequestTask, "dn=test");

            Assert.That(apiConn.Queries.Count(q => q == MonitorQueries.addAlert), Is.EqualTo(1));
        }

        [Test]
        public void IsActionInCurrentPhase_AllowsEmptyOrMatchingPhaseOnly()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { Phase = WorkflowPhases.request });

            bool emptyPhase = (bool)GetPrivateMethod("IsActionInCurrentPhase").Invoke(handler, [new WfStateAction { Phase = "" }])!;
            bool matchingPhase = (bool)GetPrivateMethod("IsActionInCurrentPhase").Invoke(handler, [new WfStateAction { Phase = WorkflowPhases.request.ToString() }])!;
            bool otherPhase = (bool)GetPrivateMethod("IsActionInCurrentPhase").Invoke(handler, [new WfStateAction { Phase = WorkflowPhases.planning.ToString() }])!;

            Assert.Multiple(() =>
            {
                Assert.That(emptyPhase, Is.True);
                Assert.That(matchingPhase, Is.True);
                Assert.That(otherPhase, Is.False);
            });
        }

        [Test]
        public async Task PerformActionById_ReturnsFalseWhenActionIsNotOffered()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States =
                [
                    new WfState
                    {
                        Id = 1,
                        Actions =
                        [
                            new WfStateActionDataHelper
                            {
                                Action = new WfStateAction
                                {
                                    Id = 5,
                                    Event = StateActionEvents.OfferButton.ToString(),
                                    ActionType = StateActionTypes.SetAlert.ToString(),
                                    Scope = WfObjectScopes.Ticket.ToString()
                                }
                            }
                        ]
                    }
                ]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };

            bool result = await handler.PerformActionById(6, ticket, WfObjectScopes.Ticket);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task PerformActionById_ExecutesOfferedAction()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States =
                [
                    new WfState
                    {
                        Id = 1,
                        Actions =
                        [
                            new WfStateActionDataHelper
                            {
                                Action = new WfStateAction
                                {
                                    Id = 5,
                                    Event = StateActionEvents.OfferButton.ToString(),
                                    ActionType = StateActionTypes.SetAlert.ToString(),
                                    Scope = WfObjectScopes.Ticket.ToString(),
                                    ExternalParams = "alert"
                                }
                            }
                        ]
                    }
                ]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };

            bool result = await handler.PerformActionById(5, ticket, WfObjectScopes.Ticket);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConn.Queries.Count(query => query == MonitorQueries.addAlert), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task PerformAction_AutoPromoteSkipsUnknownTargetState()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States = [new WfState { Id = 1 }]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "99"
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ticket.StateId, Is.EqualTo(1));
        }

        [Test]
        public async Task PerformAction_IgnoresUnknownActionType()
        {
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());

            await handler.PerformAction(new WfStateAction { ActionType = "UnknownAction" }, new WfTicket(), WfObjectScopes.Ticket);

            Assert.That(apiConn.Queries, Is.Empty);
        }

        [Test]
        public async Task GetAutoPromoteTargetState_ReturnsConfiguredFixedState()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler());

            Task<int?> task = (Task<int?>)GetPrivateMethod("GetAutoPromoteTargetState").Invoke(handler, ["5", new WfTicket(), WfObjectScopes.Ticket])!;
            int? targetState = await task;

            Assert.That(targetState, Is.EqualTo(5));
        }

        [Test]
        public void GetAutoPromoteTargetState_ThrowsForInvalidExternalParams()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler());

            Task<int?> task = (Task<int?>)GetPrivateMethod("GetAutoPromoteTargetState").Invoke(handler, ["{invalid", new WfTicket(), WfObjectScopes.Ticket])!;

            Assert.ThrowsAsync<JsonException>(async () => await task);
        }

        [Test]
        public async Task EvaluateConditionalAutoPromote_ReturnsFalseForUnsupportedAction()
        {
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler());
            ConditionalAutoPromoteParams conditionalParams = new()
            {
                ToBeCalled = (ToBeCalled)999
            };

            Task<bool> task = (Task<bool>)GetPrivateMethod("EvaluateConditionalAutoPromote").Invoke(handler, [conditionalParams, new WfTicket(), WfObjectScopes.Ticket])!;
            bool result = await task;

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ExecutePolicyCheck_ReturnsFalseWhenNoCallingTicketExists()
        {
            ActionHandlerTestPolicyChecker policyChecker = new() { Result = true };
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler(), null, true, policyChecker);

            Task<bool> task = (Task<bool>)GetPrivateMethod("ExecutePolicyCheck").Invoke(handler, [new List<int> { 5 }, "policy_check", new WfTicket(), WfObjectScopes.None])!;
            bool result = await task;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(policyChecker.RequestTaskIds, Is.Null);
            });
        }

        [Test]
        public async Task ExecutePolicyCheck_ReturnsFalseWhenPolicyCheckerThrows()
        {
            ActionHandlerTestPolicyChecker policyChecker = new()
            {
                ThrowOnCheck = true
            };
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler(), null, true, policyChecker);
            WfTicket ticket = CreateTicket(CreateEligibleRequestTask(18));

            Task<bool> task = (Task<bool>)GetPrivateMethod("ExecutePolicyCheck").Invoke(handler, [new List<int> { 5 }, "policy_check", ticket, WfObjectScopes.Ticket])!;
            bool result = await task;

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetCallingTicket_UsesActiveTicketBeforeScopedFallbacks()
        {
            WfTicket activeTicket = CreateTicket(CreateEligibleRequestTask(18));
            WfReqTask scopedTask = CreateEligibleRequestTask(19);
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { ActTicket = activeTicket });

            WfTicket? ticket = (WfTicket?)GetPrivateMethod("GetCallingTicket").Invoke(handler, [scopedTask, WfObjectScopes.RequestTask]);

            Assert.That(ticket, Is.SameAs(activeTicket));
        }

        [Test]
        public void GetCallingTicket_UsesActiveRequestTaskForImplementationAndApprovalScopes()
        {
            WfReqTask activeRequestTask = CreateEligibleRequestTask(20);
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { ActReqTask = activeRequestTask });

            WfTicket? implementationTicket = (WfTicket?)GetPrivateMethod("GetCallingTicket").Invoke(handler, [new WfImplTask { Id = 1 }, WfObjectScopes.ImplementationTask]);
            WfTicket? approvalTicket = (WfTicket?)GetPrivateMethod("GetCallingTicket").Invoke(handler, [new WfApproval { Id = 1 }, WfObjectScopes.Approval]);

            Assert.Multiple(() =>
            {
                Assert.That(implementationTicket?.Tasks.Single(), Is.SameAs(activeRequestTask));
                Assert.That(approvalTicket?.Tasks.Single(), Is.SameAs(activeRequestTask));
            });
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
            WfHandler wfHandler = new((_, _, _, _) => { }, UserConfig.ForTextOnly(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, policyChecker);
            ActionHandler handler = new(apiConn, wfHandler, null, true);
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
            WfHandler wfHandler = new((_, _, _, _) => { }, UserConfig.ForTextOnly(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, policyChecker);
            ActionHandler handler = new(apiConn, wfHandler, null, true);
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
            WfHandler wfHandler = new((_, _, _, _) => { }, UserConfig.ForTextOnly(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, new ActionHandlerTestPolicyChecker() { Result = true });
            ActionHandler handler = new(apiConn, wfHandler, null, true);
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
            WfHandler wfHandler = new((_, _, _, _) => { }, UserConfig.ForTextOnly(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, new ActionHandlerTestPolicyChecker() { Result = true });
            ActionHandler handler = new(apiConn, wfHandler, null, true);
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
            WfHandler wfHandler = new((_, _, _, _) => { }, UserConfig.ForTextOnly(globalConfig, false), new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, policyChecker);
            ActionHandler handler = new(apiConn, wfHandler, null, true);
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
            WfHandler wfHandler = new(UserConfig.ForTextOnly(globalConfig, false), apiConn, WorkflowPhases.request, null, new ActionHandlerTestPolicyChecker() { Result = true });
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
        public async Task PromoteAfterActionResult_PromotesToConfiguredSuccessOrErrorState()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States = [new WfState { Id = 1 }, new WfState { Id = 2 }, new WfState { Id = 3 }]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfTicket successTicket = new() { StateId = 1 };
            WfTicket errorTicket = new() { StateId = 1 };
            string externalParams = JsonSerializer.Serialize(new ActionResultStateParams { SuccessState = 2, ErrorState = 3 });

            await (Task)GetPrivateMethod("PromoteAfterActionResult").Invoke(handler, [externalParams, true, successTicket, WfObjectScopes.Ticket])!;
            await (Task)GetPrivateMethod("PromoteAfterActionResult").Invoke(handler, [externalParams, false, errorTicket, WfObjectScopes.Ticket])!;

            Assert.Multiple(() =>
            {
                Assert.That(successTicket.StateId, Is.EqualTo(2));
                Assert.That(errorTicket.StateId, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task PromoteAfterActionResult_DoesNotPromoteForEmptyMissingOrUnknownTarget()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States = [new WfState { Id = 1 }, new WfState { Id = 2 }]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfTicket emptyParamsTicket = new() { StateId = 1 };
            WfTicket missingTargetTicket = new() { StateId = 1 };
            WfTicket unknownTargetTicket = new() { StateId = 1 };

            await (Task)GetPrivateMethod("PromoteAfterActionResult").Invoke(handler, ["", true, emptyParamsTicket, WfObjectScopes.Ticket])!;
            await (Task)GetPrivateMethod("PromoteAfterActionResult").Invoke(handler, [JsonSerializer.Serialize(new ActionResultStateParams()), true, missingTargetTicket, WfObjectScopes.Ticket])!;
            await (Task)GetPrivateMethod("PromoteAfterActionResult").Invoke(handler, [JsonSerializer.Serialize(new ActionResultStateParams { SuccessState = 99 }), true, unknownTargetTicket, WfObjectScopes.Ticket])!;

            Assert.Multiple(() =>
            {
                Assert.That(emptyParamsTicket.StateId, Is.EqualTo(1));
                Assert.That(missingTargetTicket.StateId, Is.EqualTo(1));
                Assert.That(unknownTargetTicket.StateId, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task PromoteAfterActionResult_SkipsInvalidJson()
        {
            ActionHandlerTestApiConn apiConn = new()
            {
                States = [new WfState { Id = 1 }, new WfState { Id = 2 }]
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            await handler.Init();
            WfTicket ticket = new() { StateId = 1 };

            await (Task)GetPrivateMethod("PromoteAfterActionResult").Invoke(handler, ["{invalid", true, ticket, WfObjectScopes.Ticket])!;

            Assert.That(ticket.StateId, Is.EqualTo(1));
        }

        [Test]
        public async Task CreateFlow_SkipsWhenFlowDbIsDisabledOrScopeIsImplementationTask()
        {
            ActionHandlerTestApiConn disabledApiConn = new();
            ActionHandler disabledHandler = new(disabledApiConn, new WfHandler { userConfig = new SimulatedUserConfig() });
            ActionHandlerTestApiConn implScopeApiConn = new();
            SimulatedUserConfig enabledConfig = new() { ReqUseFlowDb = true };
            ActionHandler implScopeHandler = new(implScopeApiConn, new WfHandler { userConfig = enabledConfig });
            WfStateAction action = new() { Name = "Create flow" };

            await disabledHandler.CreateFlow(action, new WfTicket(), WfObjectScopes.Ticket, null, null);
            await implScopeHandler.CreateFlow(action, new WfImplTask(), WfObjectScopes.ImplementationTask, null, null);

            Assert.Multiple(() =>
            {
                Assert.That(disabledApiConn.Queries, Is.Empty);
                Assert.That(implScopeApiConn.Queries, Is.Empty);
            });
        }

        [Test]
        public async Task CreateFlow_WithConfirmation_DisplaysFailureMessage()
        {
            ActionHandlerTestApiConn apiConn = new();
            List<(string Title, string Message, bool Error)> uiMessages = [];
            WfHandler wfHandler = new((_, title, message, error) => uiMessages.Add((title, message, error)), new SimulatedUserConfig { ReqUseFlowDb = true },
                new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request);
            ActionHandler handler = new(apiConn, wfHandler);
            WfReqTask task = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = 2
            };
            WfStateAction action = new()
            {
                Name = "Create flow",
                ExternalParams = JsonSerializer.Serialize(new ActionResultStateParams { ConfirmUiMessage = true })
            };

            await handler.CreateFlow(action, task, WfObjectScopes.RequestTask, null, task.TicketId);

            Assert.That(uiMessages, Has.Count.EqualTo(1));
            Assert.That(uiMessages[0].Title, Is.EqualTo("Create flow"));
            Assert.That(uiMessages[0].Message, Is.EqualTo("Flow DB entries could not be created. Check the workflow log for unresolved objects or services."));
            Assert.That(uiMessages[0].Error, Is.True);
        }

        [Test]
        public async Task BundleTasks_SkipsWhenNoTicketCanBeResolved()
        {
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());

            await handler.BundleTasks(new WfStateAction(), new WfReqTask(), WfObjectScopes.RequestTask, null, null);

            Assert.That(apiConn.Queries, Is.Empty);
        }

        [Test]
        public async Task BundleTasks_WithCleanZones_LoadsPolicyMatrixZonesAndUpdatesBundleIds()
        {
            WfReqTask first = CreateBundleRequestTask(1, "10.0.0.1", "10.0.1.1");
            WfReqTask second = CreateBundleRequestTask(2, "10.0.0.1", "10.0.1.2");
            WfReqTask differentZone = CreateBundleRequestTask(3, "10.0.0.1", "10.0.2.1");
            differentZone.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "old-bundle");
            WfTicket ticket = CreateTicket(first, second, differentZone);
            ticket.Id = 7;
            ActionHandlerTestApiConn apiConn = new()
            {
                MatrixNetworkZones = CreateBundleNetworkZones()
            };
            ActionHandler handler = new(apiConn, new WfHandler());
            WfStateAction action = new()
            {
                Name = "Bundle",
                ExternalParams = new BundleTasksActionParams
                {
                    BundleType = BundleTaskType.TwoOutOfThree,
                    CleanZones = true,
                    PolicyId = 13
                }.ToExternalParams()
            };

            await handler.BundleTasks(action, ticket, WfObjectScopes.Ticket, null, null);

            Assert.That(apiConn.Queries, Has.Member(ComplianceQueries.getPolicyById));
            Assert.That(apiConn.Queries, Has.Member(ComplianceQueries.getNetworkZonesForMatrix));
            Assert.That(first.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId), Is.EqualTo("bundle-1-2"));
            Assert.That(second.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId), Is.EqualTo("bundle-1-2"));
            Assert.That(differentZone.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId), Is.Empty);
        }

        [Test]
        public async Task BundleTasks_WithCleanZonesAndPolicyWithoutMatrix_RemovesExistingBundleIds()
        {
            WfReqTask first = CreateBundleRequestTask(1, "10.0.0.1", "10.0.1.1");
            WfReqTask second = CreateBundleRequestTask(2, "10.0.0.1", "10.0.1.2");
            first.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "old-bundle");
            second.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "old-bundle");
            WfTicket ticket = CreateTicket(first, second);
            ticket.Id = 7;
            ActionHandlerTestApiConn apiConn = new();
            ActionHandler handler = new(apiConn, new WfHandler());
            WfStateAction action = new()
            {
                Name = "Bundle",
                ExternalParams = new BundleTasksActionParams
                {
                    BundleType = BundleTaskType.TwoOutOfThree,
                    CleanZones = true,
                    PolicyId = 9
                }.ToExternalParams()
            };

            await handler.BundleTasks(action, ticket, WfObjectScopes.Ticket, null, null);

            Assert.That(apiConn.Queries, Has.Member(ComplianceQueries.getPolicyById));
            Assert.That(apiConn.Queries, Has.No.Member(ComplianceQueries.getNetworkZonesForMatrix));
            Assert.That(first.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId), Is.Empty);
            Assert.That(second.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId), Is.Empty);
        }

        [Test]
        public void GetTicketForBundling_ReturnsScopedOrActiveTicket()
        {
            WfTicket scopedTicket = CreateTicket(CreateEligibleRequestTask(21));
            WfTicket activeTicket = CreateTicket(CreateEligibleRequestTask(22));
            ActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { ActTicket = activeTicket });

            WfTicket? ticketScopeResult = (WfTicket?)GetPrivateMethod("GetTicketForBundling").Invoke(handler, [scopedTicket, WfObjectScopes.Ticket]);
            WfTicket? requestTaskScopeResult = (WfTicket?)GetPrivateMethod("GetTicketForBundling").Invoke(handler, [new WfReqTask(), WfObjectScopes.RequestTask]);
            WfTicket? noneScopeResult = (WfTicket?)GetPrivateMethod("GetTicketForBundling").Invoke(handler, [new WfTicket(), WfObjectScopes.None]);

            Assert.Multiple(() =>
            {
                Assert.That(ticketScopeResult, Is.SameAs(scopedTicket));
                Assert.That(requestTaskScopeResult, Is.SameAs(activeTicket));
                Assert.That(noneScopeResult, Is.Null);
            });
        }

        [Test]
        public async Task SetAlert_SwallowsApiFailure()
        {
            ActionHandlerTestApiConn apiConn = new() { ThrowOnAddAlert = true };
            ActionHandler handler = new(apiConn, new WfHandler());

            await handler.SetAlert("alert");

            Assert.That(apiConn.Queries, Has.Member(MonitorQueries.addAlert));
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
                ],
                AppRolesById = new()
                {
                    [501] = new ModellingAppRole { Id = 501, Comment = "Manual app role note\r\nImplementationState: Old | 2024-01-01T00:00:00.0000000Z\r\nKeep app role note" }
                },
                ServiceGroupsById = new()
                {
                    [601] = new ModellingServiceGroup { Id = 601, Comment = "Manual service group note" }
                }
            };
            List<(string Title, string Message, bool Error)> uiMessages = [];
            WfHandler wfHandler = new((_, title, message, error) => uiMessages.Add((title, message, error)), new SimulatedUserConfig(),
                new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request);
            SetMatrix(wfHandler, WfTaskType.access.ToString());
            ActionHandler handler = new(apiConn, wfHandler, null, true);
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
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams { ModellingState = "Implemented", ConfirmUiMessage = true })
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket, ticketId: 77);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getWorkflowConnectionsByTicketId), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateConnectionProperties), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getAppRoleById), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getServiceGroupById), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateNwGroupComment), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.updateServiceGroupComment), Is.EqualTo(1));
            object? firstConnectionVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateConnectionProperties)];
            Assert.That(GetVariable<int>(firstConnectionVars, "id"), Is.EqualTo(41));
            Assert.That(GetVariable<string>(firstConnectionVars, "connProp"), Does.Contain("\"ImplementationState\":\"Implemented | "));
            object? appRoleVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateNwGroupComment)];
            Assert.That(GetVariable<long>(appRoleVars, "id"), Is.EqualTo(501));
            string appRoleComment = GetVariable<string>(appRoleVars, "comment");
            Assert.That(appRoleComment, Does.Contain("Manual app role note"));
            Assert.That(appRoleComment, Does.Contain("Keep app role note"));
            Assert.That(appRoleComment, Does.Contain("ImplementationState: Implemented | "));
            Assert.That(appRoleComment, Does.Not.Contain("ImplementationState: Old | "));
            object? serviceGroupVars = apiConn.Variables[apiConn.Queries.IndexOf(ModellingQueries.updateServiceGroupComment)];
            Assert.That(GetVariable<int>(serviceGroupVars, "id"), Is.EqualTo(601));
            string serviceGroupComment = GetVariable<string>(serviceGroupVars, "comment");
            Assert.That(serviceGroupComment, Does.Contain("Manual service group note"));
            Assert.That(serviceGroupComment, Does.Contain("ImplementationState: Implemented | "));
            Assert.That(uiMessages, Has.Count.EqualTo(1));
            Assert.That(uiMessages[0].Title, Is.EqualTo("Update Modelling"));
            Assert.That(uiMessages[0].Message, Is.EqualTo("3 modelling objects updated"));
            Assert.That(uiMessages[0].Error, Is.False);
        }

        [Test]
        public async Task PerformAction_UpdateModellingWithConfirmation_DisplaysMessageWhenNothingUpdated()
        {
            ActionHandlerTestApiConn apiConn = new();
            List<(string Title, string Message, bool Error)> uiMessages = [];
            WfHandler wfHandler = new((_, title, message, error) => uiMessages.Add((title, message, error)), new SimulatedUserConfig(),
                new System.Security.Claims.ClaimsPrincipal(), apiConn, new MiddlewareClient("http://localhost/"), WorkflowPhases.request);
            ActionHandler handler = new(apiConn, wfHandler, null, true);
            WfTicket ticket = CreateTicket();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.UpdateModelling.ToString(),
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams { ModellingState = "Implemented", ConfirmUiMessage = true })
            };

            await handler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(uiMessages, Has.Count.EqualTo(1));
            Assert.That(uiMessages[0].Title, Is.EqualTo("Update Modelling"));
            Assert.That(uiMessages[0].Message, Is.EqualTo("0 modelling objects updated"));
            Assert.That(uiMessages[0].Error, Is.False);
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
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams { ModellingState = "Implemented" })
            };

            await handler.PerformAction(action, selectedTask, WfObjectScopes.RequestTask);

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getWorkflowConnectionById), Is.EqualTo(1));
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
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams())
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

            Assert.That(apiConn.Queries.Count(q => q == ModellingQueries.getWorkflowConnectionsByTicketId), Is.EqualTo(1));
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

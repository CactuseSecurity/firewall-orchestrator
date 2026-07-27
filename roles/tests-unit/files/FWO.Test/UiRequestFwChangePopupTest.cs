using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using GraphQL;
using GraphQL.Client.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestFwChangePopupTest
    {
        private static IRenderedComponent<RequestFwChangePopup> RenderPopup(
            BunitContext context,
            RequestFwChangePopupTestApiConn apiConn,
            SimulatedUserConfig userConfig,
            FwoOwner selectedApp,
            List<ModellingConnection> connections,
            Action<bool>? displayChanged = null,
            Func<WfTicket, Task>? refreshParent = null)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestFwChangePopupAuthStateProvider(Roles.Modeller));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddScoped<DomEventService>();

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<RequestFwChangePopup>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                    .Add(p => p.SelectedApp, selectedApp)
                    .Add(p => p.Connections, connections)
                    .Add(p => p.RefreshParent, refreshParent ?? (_ => Task.CompletedTask))
                    .Add(p => p.ChangeStatus, "Ready")
                    .Add(p => p.LastRequestDate, "2026-05-07")));

            return wrapper.FindComponent<RequestFwChangePopup>();
        }

        private static TValue GetPrivateField<TValue>(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? (TValue)field.GetValue(instance)! : throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        private static void SetPrivateProperty(object instance, string propertyName, object? value)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, propertyName);
            }
            property.SetValue(instance, value);
        }

        private static void SetPrivateField(object instance, string fieldName, object? value)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(instance.GetType().FullName, fieldName);
            }
            field.SetValue(instance, value);
        }

        private static MethodInfo GetPrivateMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, name);
        }

        [Test]
        public void DisplayedWithWorkflowNotifications_BuildsTasksAndEnablesRequestButtonForOwner()
        {
            RequestFwChangePopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, selectedApp, [CreateConnection(41)]);

            component.WaitForAssertion(() =>
            {
                Assert.That(component.FindAll("button.btn-primary").Any(button => !button.HasAttribute("disabled")), Is.True);
            });
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getHistoryForApp));
            Assert.That(component.Markup, Does.Contain("41"));
        }

        [Test]
        public void ExistingRequestInProgress_UsesTicketTasksAndDisablesRequestButton()
        {
            WfReqTask existingTask = new()
            {
                Id = 901,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = RequestFwChangePopupTestApiConn.kInProgressStateId,
                Title = "Existing request"
            };
            RequestFwChangePopupTestApiConn apiConn = new()
            {
                LatestTicket = new WfTicket
                {
                    Id = 77,
                    StateId = RequestFwChangePopupTestApiConn.kInProgressStateId,
                    CreationDate = new DateTime(2026, 5, 7, 10, 0, 0, DateTimeKind.Utc),
                    Tasks = [existingTask]
                }
            };
            SimulatedUserConfig userConfig = CreateUserConfig();

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, new() { Id = 7, Name = "App" }, [CreateConnection(41)]);

            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Contain("Existing request"));
            });
            Assert.That(apiConn.Queries, Does.Not.Contain(ModellingQueries.getHistoryForApp));
            Assert.That(component.FindAll("button.btn-primary").All(button => button.HasAttribute("disabled")), Is.True);
        }

        [Test]
        public void DisplayedWhenStateLoadingFails_HandlesErrorAndStopsProgress()
        {
            RequestFwChangePopupTestApiConn apiConn = new() { ThrowOnGetStates = true };
            SimulatedUserConfig userConfig = CreateUserConfig();

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, new() { Id = 7, Name = "App" }, [CreateConnection(41)]);

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getStates));
                Assert.That(component.Markup, Does.Contain("Nothing to request!"));
            });
        }

        [Test]
        public void Close_ResetsDisplayAndNotifiesParent()
        {
            RequestFwChangePopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            bool displayChanged = true;

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(
                context,
                apiConn,
                userConfig,
                new() { Id = 7, Name = "App" },
                [CreateConnection(41)],
                value => displayChanged = value);

            component.Instance.GetType().GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task StartRequests_WithWorkflowNotifications_CreatesTicketMarksConnectionsAndLogsHistory()
        {
            RequestFwChangePopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            bool displayChanged = false;
            int refreshCalls = 0;
            List<(string Title, string Message, bool Error)> messages = [];
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            List<ModellingConnection> connections = [CreateConnection(41)];

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(
                context,
                apiConn,
                userConfig,
                selectedApp,
                connections,
                value => displayChanged = value,
                _ =>
                {
                    refreshCalls++;
                    return Task.CompletedTask;
                });

            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-primary").Any(button => !button.HasAttribute("disabled")), Is.True));

            SetPrivateProperty(component.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, message, error) => messages.Add((title, message, error))));
            SetPrivateProperty(component.Instance, "middlewareClient", null);

            component.FindAll("button.btn-primary").Single(button => !button.HasAttribute("disabled")).Click();
            component.WaitForAssertion(() => Assert.That(apiConn.NewTicketCalls, Is.EqualTo(1)));

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.NewApprovalCalls, Is.Zero);
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task StartRequests_WithWorkflowNotifications_MarksOnlyUnrequestedConnections()
        {
            RequestFwChangePopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            bool displayChanged = true;
            int refreshCalls = 0;
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            List<ModellingConnection> connections =
            [
                CreateConnection(41),
                CreateConnection(42)
            ];
            connections[0].RequestedOnFw = true;

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(
                context,
                apiConn,
                userConfig,
                selectedApp,
                connections,
                value => displayChanged = value,
                _ =>
                {
                    refreshCalls++;
                    return Task.CompletedTask;
                });

            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-primary").Any(button => !button.HasAttribute("disabled")), Is.True));
            component.FindAll("button.btn-primary").Single(button => !button.HasAttribute("disabled")).Click();
            component.WaitForAssertion(() => Assert.That(apiConn.NewTicketCalls, Is.EqualTo(1)));

            Assert.Multiple(() =>
            {
                Assert.That(connections[0].RequestedOnFw, Is.True);
                Assert.That(connections[1].RequestedOnFw, Is.False);
                Assert.That(refreshCalls, Is.EqualTo(0));
                Assert.That(displayChanged, Is.True);
            });
        }

        [Test]
        public void DisplayTaskDetails_CoversAccessRuleDeleteAndGroupBranches()
        {
            RequestFwChangePopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();

            using BunitContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, new() { Id = 7, Name = "App" }, [CreateConnection(41)]);

            SetPrivateField(component.Instance, "ipProtos", new List<IpProtocol>
            {
                new() { Id = 6, Name = "tcp" }
            });
            SetPrivateField(component.Instance, "Devices", new List<Device>
            {
                new() { Id = 1, Name = "gw-1" }
            });

            WfReqTask accessTask = new()
            {
                TaskType = WfTaskType.access.ToString(),
                Comments =
                [
                    new WfCommentDataHelper(new WfComment { CommentText = "access-comment" })
                ],
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), GroupName = "src-group", RequestAction = RequestAction.create.ToString() },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), GroupName = "dst-group", RequestAction = RequestAction.create.ToString() },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), GroupName = "svc-group", RequestAction = RequestAction.create.ToString() },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, PortEnd = 443, ProtoId = 6, RequestAction = RequestAction.create.ToString() }
                ]
            };

            WfReqTask ruleDeleteTask = new()
            {
                TaskType = WfTaskType.rule_delete.ToString(),
                Comments =
                [
                    new WfCommentDataHelper(new WfComment { CommentText = "rule-comment" })
                ],
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), DeviceId = 1, RuleUid = "ru-1", RequestAction = RequestAction.delete.ToString() },
                    new WfReqElement { Field = ElemFieldType.source.ToString(), GroupName = "src-2", RequestAction = RequestAction.delete.ToString() },
                    new WfReqElement { Field = ElemFieldType.modelled_source.ToString(), GroupName = "m-src", RequestAction = RequestAction.create.ToString() },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), GroupName = "dst-2", RequestAction = RequestAction.delete.ToString() },
                    new WfReqElement { Field = ElemFieldType.modelled_destination.ToString(), GroupName = "m-dst", RequestAction = RequestAction.create.ToString() },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), GroupName = "svc-2", RequestAction = RequestAction.delete.ToString() }
                ]
            };

            WfReqTask groupModifyTask = new()
            {
                TaskType = WfTaskType.group_modify.ToString(),
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 22, ProtoId = 6, RequestAction = RequestAction.create.ToString() },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 23, ProtoId = 6, RequestAction = RequestAction.addAfterCreation.ToString() },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 24, ProtoId = 6, RequestAction = RequestAction.delete.ToString() }
                ]
            };

            string accessDetails = (string)GetPrivateMethod(typeof(RequestFwChangePopup), "DisplayTaskDetails").Invoke(component.Instance, [accessTask])!;
            string ruleDetails = (string)GetPrivateMethod(typeof(RequestFwChangePopup), "DisplayTaskDetails").Invoke(component.Instance, [ruleDeleteTask])!;
            string groupDetails = (string)GetPrivateMethod(typeof(RequestFwChangePopup), "DisplayTaskDetails").Invoke(component.Instance, [groupModifyTask])!;
            string emptyDetails = (string)GetPrivateMethod(typeof(RequestFwChangePopup), "DisplayTaskDetails").Invoke(component.Instance, [new WfReqTask { TaskType = "unsupported" }])!;

            Assert.Multiple(() =>
            {
                Assert.That(accessDetails, Does.Contain("src-group"));
                Assert.That(accessDetails, Does.Contain("dst-group"));
                Assert.That(accessDetails, Does.Contain("svc-group"));
                Assert.That(accessDetails, Does.Contain("443"));
                Assert.That(accessDetails, Does.Contain("access-comment"));
                Assert.That(ruleDetails, Does.Contain("gw-1"));
                Assert.That(ruleDetails, Does.Contain("ru-1"));
                Assert.That(ruleDetails, Does.Contain("m-src"));
                Assert.That(ruleDetails, Does.Contain("modelled_destination: m-dst"));
                Assert.That(ruleDetails, Does.Contain("rule-comment"));
                Assert.That(groupDetails, Does.Contain("text-success"));
                Assert.That(groupDetails, Does.Contain("text-info"));
                Assert.That(groupDetails, Does.Contain("text-danger"));
                Assert.That(groupDetails, Does.Contain("22/tcp"));
                Assert.That(groupDetails, Does.Contain("23/tcp"));
                Assert.That(groupDetails, Does.Contain("24/tcp"));
                Assert.That(emptyDetails, Is.Empty);
            });
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new()
            {
                ModIntegrationMode = ModIntegrationMode.WorkflowNotifications,
                ReqPriorities = "[]",
                User = { Ownerships = [7], Roles = [Roles.Modeller] }
            };
        }

        private static ModellingConnection CreateConnection(int id)
        {
            return new()
            {
                Id = id,
                Name = $"Conn{id}",
                SourceAppRoles =
                [
                    new() { Content = new() { Id = 101, IdString = "AR1", Name = "AR1" } }
                ],
                DestinationAppServers =
                [
                    new() { Content = new() { Id = 201, Name = "Server1", Ip = "10.0.1.1/32" } }
                ],
                Services =
                [
                    new() { Content = new() { Id = 301, Name = "HTTPS", ProtoId = 6, Port = 443 } }
                ]
            };
        }

        private sealed class RequestFwChangePopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public RequestFwChangePopupAuthStateProvider(params string[] roles)
            {
                List<Claim> claims = [.. roles.Select(role => new Claim(ClaimTypes.Role, role))];
                principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }

    internal sealed class RequestFwChangePopupTestApiConn : SimulatedApiConnection
    {
        public const int kInitializedStateId = 23;
        public const int kInProgressStateId = 24;
        public WfTicket? LatestTicket { get; set; }
        public WfTicket? CreatedTicket { get; private set; }
        public bool ThrowOnGetStates { get; set; }
        public List<string> Queries { get; } = [];
        public List<object?> Variables { get; } = [];
        public int NewTicketCalls { get; private set; }
        public int NewApprovalCalls { get; private set; }
        public int AddTicketIdCalls { get; private set; }
        public int UpdateConnectionFwRequestedCalls { get; private set; }
        public int AddHistoryEntryCalls { get; private set; }
        private readonly string stateMatrix = JsonSerializer.Serialize(new GlobalStateMatrix
        {
            GlobalMatrix =
            {
                [WorkflowPhases.request] = CreateMatrix(0, 1, 49, true),
                [WorkflowPhases.approval] = CreateMatrix(49, 60, 99, false),
                [WorkflowPhases.planning] = CreateMatrix(99, 110, 149, false),
                [WorkflowPhases.verification] = CreateMatrix(149, 160, 199, false),
                [WorkflowPhases.implementation] = CreateMatrix(49, 210, 249, false),
                [WorkflowPhases.review] = CreateMatrix(249, 260, 299, false),
                [WorkflowPhases.recertification] = CreateMatrix(299, 310, 349, false)
            }
        });

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            Variables.Add(variables);
            if (query == StmQueries.getIpProtocols)
            {
                return Task.FromResult((QueryResponseType)(object)new List<IpProtocol> { new() { Id = 6, Name = "tcp" } });
            }
            if (query == DeviceQueries.getDeviceDetails)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Device>());
            }
            if (query == OwnerQueries.getOwners)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
            }
            if (query == RequestQueries.getStates)
            {
                if (ThrowOnGetStates)
                {
                    throw new HttpRequestException("state loading failed");
                }
                return Task.FromResult((QueryResponseType)(object)new List<WfState>
                {
                    new() { Id = kInitializedStateId, Name = "Initialized" },
                    new() { Id = kInProgressStateId, Name = "In progress" },
                    new() { Id = 90, Name = "Done" },
                    new() { Id = 91, Name = "Rejected" }
                });
            }
            if (query == RequestQueries.getExtStates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<WfExtState>
                {
                    new() { Name = ExtStates.ExtReqInitialized.ToString(), StateId = kInitializedStateId },
                    new() { Name = ExtStates.ExtReqInProgress.ToString(), StateId = kInProgressStateId },
                    new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                    new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                });
            }
            if (query == RequestQueries.getActiveStateMatrixConfiguration)
            {
                return Task.FromResult((QueryResponseType)(object)StateMatrixConfigurationTestHelper.FromLegacyJson(stateMatrix));
            }
            if (query == RequestQueries.newTicket)
            {
                NewTicketCalls++;
                WfTicketWriter requestTasks = GetVariable<WfTicketWriter>(variables, "requestTasks");
                CreatedTicket = BuildCreatedTicket(variables, requestTasks, 100 + NewTicketCalls);
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = CreatedTicket.Id }] });
            }
            if (query == RequestQueries.newApproval)
            {
                NewApprovalCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 900 + NewApprovalCalls }] });
            }
            if (query == ExtRequestQueries.getLatestTicketId)
            {
                List<TicketId> ticketIds = LatestTicket != null ? [new() { Id = LatestTicket.Id }] : [];
                return Task.FromResult((QueryResponseType)(object)ticketIds);
            }
            if (query == RequestQueries.getTicketById)
            {
                if (CreatedTicket != null)
                {
                    return Task.FromResult((QueryResponseType)(object)CreatedTicket);
                }
                if (LatestTicket != null)
                {
                    return Task.FromResult((QueryResponseType)(object)LatestTicket);
                }
                return Task.FromResult((QueryResponseType)(object)new WfTicket());
            }
            if (query == ExtRequestQueries.addTicketId)
            {
                AddTicketIdCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = GetVariable<long>(variables, "ticketId") }] });
            }
            if (query == ModellingQueries.updateConnectionFwRequested)
            {
                UpdateConnectionFwRequestedCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }
            if (query == ModellingQueries.addHistoryEntry)
            {
                AddHistoryEntryCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 1 }] });
            }
            if (query == ModellingQueries.getHistoryForApp)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ModellingHistoryEntry>());
            }
            throw new AssertionException($"Unexpected query: {query}");
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
            Action<Exception> exceptionHandler,
            GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
            string subscription,
            object? variables = null,
            string? operationName = null)
        {
            GraphQLHttpClient graphQlClient = new(
                new GraphQLHttpClientOptions(),
                new GraphQL.Client.Serializer.SystemTextJson.SystemTextJsonSerializer(),
                new HttpClient());
            GraphQLRequest request = new(subscription)
            {
                OperationName = operationName
            };
            return new SimulatedApiSubscription<SubscriptionResponseType>(this, graphQlClient, request, exceptionHandler, subscriptionUpdateHandler);
        }

        private static WfTicket BuildCreatedTicket(object? variables, WfTicketWriter writer, long ticketId)
        {
            WfTicket ticket = new()
            {
                Id = ticketId,
                Title = GetVariable<string>(variables, "title"),
                StateId = GetVariable<int>(variables, "state"),
                Reason = GetVariable<string>(variables, "reason") ?? "",
                Priority = GetVariable<int>(variables, "priority"),
                Locked = GetVariable<bool>(variables, "locked"),
                Requester = new UiUser { DbId = GetVariable<int>(variables, "requesterId"), Name = "Requester" }
            };

            long taskId = ticketId * 10;
            foreach (WfReqTaskWriter taskWriter in writer.Tasks)
            {
                WfReqTask task = new()
                {
                    Id = ++taskId,
                    TicketId = ticketId,
                    Title = taskWriter.Title,
                    TaskNumber = taskWriter.TaskNumber,
                    StateId = taskWriter.StateId,
                    TaskType = taskWriter.TaskType,
                    RequestAction = taskWriter.RequestAction,
                    Reason = taskWriter.Reason,
                    AdditionalInfo = taskWriter.AdditionalInfo,
                    ManagementId = taskWriter.ManagementId,
                    Locked = taskWriter.Locked
                };
                task.Elements.AddRange(taskWriter.Elements.WfElementList.Select(element => new WfReqElement
                {
                    Field = element.Field,
                    RequestAction = element.RequestAction,
                    DeviceId = element.DeviceId,
                    RuleUid = element.RuleUid
                }));
                ticket.Tasks.Add(task);
            }
            return ticket;
        }

        private static StateMatrix CreateMatrix(int input, int started, int end, bool active)
        {
            return new StateMatrix
            {
                Matrix =
                {
                    [0] = [0, 1, 7, 49],
                    [1] = [1, 7, 49],
                    [7] = [7, 49],
                    [49] = [49],
                    [249] = [249]
                },
                DerivedStates =
                {
                    [0] = 0,
                    [1] = 1,
                    [7] = 7,
                    [49] = 49,
                    [249] = 249
                },
                LowestInputState = input,
                LowestStartedState = started,
                LowestEndState = end,
                Active = active
            };
        }

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            object? value = variables?.GetType().GetProperties().First(p => p.Name == propertyName).GetValue(variables, null);
            return value is TValue typedValue ? typedValue : throw new AssertionException($"Variable {propertyName} missing");
        }

    }
}

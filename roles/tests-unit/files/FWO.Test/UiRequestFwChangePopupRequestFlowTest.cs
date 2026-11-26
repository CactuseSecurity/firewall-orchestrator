using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Text;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the popup requesting the firewall changes of an application: the shown tasks, the running request and
    /// the creation of a new request.
    /// </summary>
    [TestFixture]
    internal class UiRequestFwChangePopupRequestFlowTest
    {
        private const int kAppId = 17;
        private const int kInitializedStateId = 1;
        private const int kDoneStateId = 49;
        private const int kRejectedStateId = 249;
        private const int kFirewallDeviceId = 5;

        private sealed record UiMessage(string Title, string Text, bool Error);

        /// <summary>
        /// Answers the queries of the popup and, through its base, the queries of the ticket creation.
        /// </summary>
        private sealed class RequestFwChangeApiConn : TicketCreatorTest.TicketCreatorTestApiConn
        {
            private bool ticketCreated;

            public List<string> PopupQueries { get; } = [];
            public List<TicketId> LatestTicketIds { get; set; } = [];
            public WfTicket? LatestTicket { get; set; }
            public HashSet<string> FailingQueries { get; } = [];
            public bool FailDisposingSubscriptions { get; set; }
            public List<int> FwRequestedConnectionIds { get; } = [];
            public List<long> SavedTicketIds { get; } = [];
            public List<(int AppServerId, long AppZoneId)> AppZoneLinks { get; } = [];
            public List<(Action<Exception> OnError, GraphQlApiSubscription<List<WfReqTask>>.SubscriptionUpdate OnUpdate)> TaskSubscriptions { get; } = [];

            public RequestFwChangeApiConn()
            {
                ExtStates =
                [
                    new WfExtState { Name = FWO.Data.ExtStates.ExtReqInitialized.ToString(), StateId = kInitializedStateId },
                    new WfExtState { Name = FWO.Data.ExtStates.ExtReqDone.ToString(), StateId = kDoneStateId },
                    new WfExtState { Name = FWO.Data.ExtStates.ExtReqRejected.ToString(), StateId = kRejectedStateId }
                ];
            }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                PopupQueries.Add(query);
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException($"query failed: {query}");
                }
                if (query == StmQueries.getIpProtocols)
                {
                    return Answer<T>(new List<IpProtocol> { new() { Id = 1, Name = "icmp" }, new() { Id = 6, Name = "tcp" }, new() { Id = 17, Name = "udp" } });
                }
                if (query == ExtRequestQueries.getLatestTicketId)
                {
                    return Answer<T>(LatestTicketIds);
                }
                if (query == RequestQueries.getTicketById && !ticketCreated && LatestTicket != null)
                {
                    return Answer<T>(LatestTicket);
                }
                if (query == ModellingQueries.getHistoryForApp)
                {
                    return Answer<T>(new List<ModellingHistoryEntry>());
                }
                if (query == ModellingQueries.updateConnectionFwRequested)
                {
                    FwRequestedConnectionIds.Add(GetIntVariable(variables, "id"));
                    return Answer<T>(new ReturnIdWrapper());
                }
                if (query == ExtRequestQueries.addTicketId)
                {
                    SavedTicketIds.Add(GetLongVariable(variables, "ticketId"));
                    return Answer<T>(new ReturnIdWrapper());
                }
                if (query == ModellingQueries.addNwObjectToNwGroup)
                {
                    AppZoneLinks.Add(((int)GetLongVariable(variables, "nwObjectId"), GetLongVariable(variables, "nwGroupId")));
                    return Answer<T>(new ReturnId { AffectedRows = 1 });
                }
                if (query == RequestQueries.newTicket)
                {
                    ticketCreated = true;
                }
                return base.SendQueryAsync<T>(query, variables, operationName, chunkingOptions);
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null,
                string? operationName = null)
            {
                if (subscriptionUpdateHandler is GraphQlApiSubscription<List<WfReqTask>>.SubscriptionUpdate taskUpdateHandler)
                {
                    TaskSubscriptions.Add((exceptionHandler, taskUpdateHandler));
                }
                return new SimulatedApiSubscription<SubscriptionResponseType>(this,
                    new GraphQLHttpClient(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new HttpClient()),
                    new GraphQLRequest(subscription, variables, operationName), exceptionHandler, subscriptionUpdateHandler);
            }

            public override void DisposeSubscriptions<T>()
            {
                if (FailDisposingSubscriptions)
                {
                    throw new InvalidOperationException("subscriptions could not be disposed");
                }
            }

            private static Task<T> Answer<T>(object answer)
            {
                return Task.FromResult((T)answer);
            }

            private static int GetIntVariable(object? variables, string name)
            {
                return variables?.GetType().GetProperty(name)?.GetValue(variables) is int value ? value : 0;
            }

            private static long GetLongVariable(object? variables, string name)
            {
                return variables?.GetType().GetProperty(name)?.GetValue(variables) is long value ? value : 0;
            }
        }

        /// <summary>
        /// Answers the request to create the external requests of a ticket with a configurable outcome.
        /// </summary>
        private sealed class ExternalRequestHandler : HttpMessageHandler
        {
            public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
            public string Body { get; set; } = "true";
            public bool Fail { get; set; }
            public List<string> ExternalRequestBodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Fail)
                {
                    throw new HttpRequestException("middleware not reachable");
                }
                if (request.RequestUri?.AbsolutePath.EndsWith("/ExternalRequest", StringComparison.Ordinal) ?? false)
                {
                    ExternalRequestBodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
                }
                return new HttpResponseMessage(StatusCode) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class PopupTestContext(BunitContext context, RequestFwChangeApiConn apiConn, SimulatedUserConfig userConfig,
            ExternalRequestHandler middleware) : IAsyncDisposable
        {
            public BunitContext Context { get; } = context;
            public RequestFwChangeApiConn ApiConn { get; } = apiConn;
            public SimulatedUserConfig UserConfig { get; } = userConfig;
            public ExternalRequestHandler Middleware { get; } = middleware;
            public List<UiMessage> Messages { get; } = [];
            public List<WfTicket> RefreshedTickets { get; } = [];
            public List<bool> DisplayChanges { get; } = [];

            public ValueTask DisposeAsync()
            {
                return Context.DisposeAsync();
            }
        }

        private static PopupTestContext CreatePopupContext(ModIntegrationMode integrationMode = ModIntegrationMode.WorkflowNotifications, bool modeller = true)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Modeller));
            RequestFwChangeApiConn apiConn = new();
            SimulatedUserConfig userConfig = new()
            {
                ModIconify = false,
                ModIntegrationMode = integrationMode,
                ModModelledMarker = "FWO:",
                ModNamingConvention = "{}",
                ModExtraConfigs = "[]",
                ReqPriorities = "[]"
            };
            userConfig.User.DbId = 3;
            userConfig.User.Name = "requester";
            userConfig.User.Roles = modeller ? [Roles.Modeller] : [Roles.Auditor];
            userConfig.User.Ownerships = [kAppId];
            ExternalRequestHandler middleware = new();
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(middleware);
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
            context.Services.AddSingleton<DomEventService>();
            return new PopupTestContext(context, apiConn, userConfig, middleware);
        }

        private static IRenderedComponent<RequestFwChangePopup> RenderPopup(PopupTestContext test, List<ModellingConnection>? connections = null)
        {
            Action<Exception?, string, string, bool> displayMessage = (_, title, text, error) => test.Messages.Add(new(title, text, error));
            return test.Context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(cascade => cascade
                    .Add(value => value.Value, displayMessage)
                    .AddChildContent<RequestFwChangePopup>(popup => popup
                        .Add(p => p.Display, true)
                        .Add(p => p.DisplayChanged, value => test.DisplayChanges.Add(value))
                        .Add(p => p.SelectedApp, new FwoOwner { Id = kAppId, Name = "App17" })
                        .Add(p => p.Connections, connections ?? [])
                        .Add(p => p.ChangeStatus, "In process")
                        .Add(p => p.LastRequestDate, "01.07.2026")
                        .Add(p => p.RefreshParent, ticket =>
                        {
                            test.RefreshedTickets.Add(ticket);
                            return Task.CompletedTask;
                        }))))
                .FindComponent<RequestFwChangePopup>();
        }

        private static ModellingConnection CreateRequestableConnection(int id)
        {
            return new ModellingConnection
            {
                Id = id,
                AppId = kAppId,
                Name = $"conn{id}",
                Reason = $"reason{id}",
                SourceAppServers = [new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = id * 10, Name = $"src{id}", Ip = "10.0.0.1/32", IpEnd = "10.0.0.1/32" } }],
                DestinationAppServers = [new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = id * 10 + 1, Name = $"dst{id}", Ip = "10.0.1.1/32", IpEnd = "10.0.1.1/32" } }],
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = id * 10 + 2, Name = "https", ProtoId = 6, Port = 443 } }]
            };
        }

        private static IElement RequestButton(IRenderedComponent<RequestFwChangePopup> popup, SimulatedUserConfig userConfig)
        {
            return popup.FindAll("button").Single(button => button.TextContent.Trim() == userConfig.GetText("request_fw_change"));
        }

        private static WfReqElement NetworkElement(string field, string ip, string? name = null, string? groupName = null, string? requestAction = null)
        {
            return new WfReqElement { Field = field, IpString = ip, Name = name, GroupName = groupName, RequestAction = requestAction ?? RequestAction.create.ToString() };
        }

        private static WfReqElement ServiceElement(int? protoId, int? port, int? portEnd = null, string? groupName = null, string? requestAction = null)
        {
            return new WfReqElement
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = protoId,
                Port = port,
                PortEnd = portEnd,
                GroupName = groupName,
                RequestAction = requestAction ?? RequestAction.create.ToString()
            };
        }

        private static WfTicket CreateRunningTicket()
        {
            WfReqTask accessTask = new()
            {
                Id = 1001,
                TaskNumber = 1,
                Title = "access <b>task</b>",
                TaskType = WfTaskType.access.ToString(),
                StateId = kInitializedStateId,
                Elements =
                [
                    NetworkElement(ElemFieldType.source.ToString(), "10.1.1.1/32", "srv-a"),
                    NetworkElement(ElemFieldType.source.ToString(), "10.1.1.0/24", groupName: "AR-source"),
                    NetworkElement(ElemFieldType.destination.ToString(), "10.2.2.2/32", "srv-b"),
                    ServiceElement(6, 443),
                    ServiceElement(99, 8080, 8090),
                    ServiceElement(1, null),
                    ServiceElement(6, 22, groupName: "SG-admin")
                ],
                Comments = [new WfCommentDataHelper { Comment = new WfComment { CommentText = "please hurry" } }]
            };
            accessTask.SetAddInfo(AdditionalInfoKeys.ExtIcketId, "EXT-4711");
            WfReqTask modifyTask = new()
            {
                Id = 1002,
                TaskNumber = 2,
                Title = "modify rule",
                TaskType = WfTaskType.rule_modify.ToString(),
                StateId = kInitializedStateId,
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = "uid-1", DeviceId = kFirewallDeviceId },
                    NetworkElement(ElemFieldType.source.ToString(), "10.1.1.1/32", "srv-a", requestAction: RequestAction.delete.ToString())
                ]
            };
            modifyTask.SetDeviceList([kFirewallDeviceId, 999]);
            WfReqTask deleteTask = new()
            {
                Id = 1003,
                TaskNumber = 3,
                Title = "delete rule",
                TaskType = WfTaskType.rule_delete.ToString(),
                StateId = kDoneStateId,
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = "uid-2" },
                    NetworkElement(ElemFieldType.modelled_source.ToString(), "10.3.3.3/32", "modelled-src"),
                    NetworkElement(ElemFieldType.modelled_destination.ToString(), "10.4.4.4/32", "modelled-dst")
                ]
            };
            deleteTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            WfReqTask groupTask = new()
            {
                Id = 1004,
                TaskNumber = 4,
                Title = "group members",
                TaskType = WfTaskType.group_modify.ToString(),
                StateId = kInitializedStateId,
                Elements =
                [
                    NetworkElement(ElemFieldType.source.ToString(), "10.5.5.5/32", "current-member", requestAction: RequestAction.unchanged.ToString()),
                    NetworkElement(ElemFieldType.source.ToString(), "10.6.6.6/32", "new-member"),
                    NetworkElement(ElemFieldType.source.ToString(), "10.6.6.7/32", "late-member", requestAction: RequestAction.addAfterCreation.ToString()),
                    NetworkElement(ElemFieldType.source.ToString(), "10.7.7.7/32", "old-member", requestAction: RequestAction.delete.ToString()),
                    ServiceElement(17, 53)
                ]
            };
            WfReqTask otherTask = new()
            {
                Id = 1005,
                TaskNumber = 5,
                Title = "interface",
                TaskType = WfTaskType.new_interface.ToString(),
                StateId = kInitializedStateId
            };
            return new WfTicket
            {
                Id = 42,
                StateId = kInitializedStateId,
                CreationDate = new DateTime(2026, 7, 1, 10, 0, 0),
                Tasks = [accessTask, modifyTask, deleteTask, groupTask, otherTask]
            };
        }

        [Test]
        public async Task RunningRequestShowsItsTasksWithTheirDetails()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.LatestTicketIds = [new TicketId { Id = 42 }];
            test.ApiConn.LatestTicket = CreateRunningTicket();

            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);
            popup.WaitForAssertion(() => Assert.That(popup.Markup, Does.Contain("EXT-4711")));
            int taskCount = popup.FindAll("tbody tr a[aria-expanded]").Count;
            for (int opened = 0; opened < taskCount; opened++)
            {
                await popup.InvokeAsync(() => popup.FindAll("tbody tr a[aria-expanded='false']")[0].Click());
            }

            Assert.Multiple(() =>
            {
                Assert.That(popup.Markup, Does.Contain("access &lt;b&gt;task&lt;/b&gt;").Or.Contain("access task"));
                Assert.That(popup.Markup, Does.Contain("<span class=\"text-danger\">modify rule</span>"));
                Assert.That(popup.Markup, Does.Contain("<span class=\"text-danger\">delete rule</span>"));
                Assert.That(taskCount, Is.EqualTo(5));
                Assert.That(popup.Markup, Does.Contain("please hurry").And.Contain("uid-1").And.Contain("old-member"), "the opened details show the task contents");
                Assert.That(test.ApiConn.TaskSubscriptions, Has.Count.EqualTo(5));
                Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.True);
            });
        }

        [Test]
        public async Task TaskDetailsDescribeEveryKindOfTask()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.LatestTicketIds = [new TicketId { Id = 42 }];
            test.ApiConn.LatestTicket = CreateRunningTicket();
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);
            popup.WaitForAssertion(() => Assert.That(test.ApiConn.TaskSubscriptions, Has.Count.EqualTo(5)));
            List<WfReqTask> tasks = GetTaskList(popup);

            string accessDetails = DisplayTaskDetails(popup, tasks[0]);
            string modifyDetails = DisplayTaskDetails(popup, tasks[1]);
            string deleteDetails = DisplayTaskDetails(popup, tasks[2]);
            string groupDetails = DisplayTaskDetails(popup, tasks[3]);
            string otherDetails = DisplayTaskDetails(popup, tasks[4]);

            Assert.Multiple(() =>
            {
                Assert.That(accessDetails, Does.Contain("srv-a").And.Contain("AR-source").And.Contain("srv-b"));
                Assert.That(accessDetails, Does.Contain("443/tcp").And.Contain("8080-8090/99").And.Contain("icmp").And.Contain("SG-admin"));
                Assert.That(accessDetails, Does.Contain("please hurry"));
                Assert.That(modifyDetails, Does.Contain("FW1").And.Contain("uid-1"));
                Assert.That(deleteDetails, Does.Contain(test.UserConfig.GetText("all")).And.Contain("uid-2"));
                Assert.That(deleteDetails, Does.Contain("modelled-src").And.Contain("modelled-dst"));
                Assert.That(groupDetails, Does.Contain("current-member"));
                Assert.That(groupDetails, Does.Contain("<span class=\"text-success\" >").And.Contain("new-member"));
                Assert.That(groupDetails, Does.Contain("<span class=\"text-info\" >").And.Contain("late-member"));
                Assert.That(groupDetails, Does.Contain("<span class=\"text-danger\" >").And.Contain("old-member"));
                Assert.That(groupDetails, Does.Contain("53/udp"));
                Assert.That(otherDetails, Is.Empty);
            });
        }

        private static List<WfReqTask> GetTaskList(IRenderedComponent<RequestFwChangePopup> popup)
        {
            return (List<WfReqTask>)typeof(RequestFwChangePopup).GetField("TaskList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(popup.Instance)!;
        }

        private static string DisplayTaskDetails(IRenderedComponent<RequestFwChangePopup> popup, WfReqTask task)
        {
            return (string)typeof(RequestFwChangePopup).GetMethod("DisplayTaskDetails", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(popup.Instance, [task])!;
        }

        [Test]
        public async Task TaskUpdatesReplaceTheShownTaskAndErrorsAreReported()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.LatestTicketIds = [new TicketId { Id = 42 }];
            test.ApiConn.LatestTicket = CreateRunningTicket();
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);
            popup.WaitForAssertion(() => Assert.That(test.ApiConn.TaskSubscriptions, Has.Count.EqualTo(5)));
            (Action<Exception> onError, GraphQlApiSubscription<List<WfReqTask>>.SubscriptionUpdate onUpdate) = test.ApiConn.TaskSubscriptions[0];

            onUpdate([new WfReqTask { Id = 1001, TaskNumber = 1, Title = "updated access", TaskType = WfTaskType.access.ToString(), StateId = kDoneStateId }]);
            onUpdate([new WfReqTask { Id = 9999, Title = "unknown task" }]);
            onUpdate([]);
            onError(new InvalidOperationException("connection lost"));

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain("updated access"));
                Assert.That(popup.Markup, Does.Not.Contain("unknown task"));
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("request_fw_change") && message.Error));
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("task"), test.UserConfig.GetText("update_subscription_error"), true)));
            });
        }

        [Test]
        public async Task TaskUpdatesBeforeTheSubscriptionsAreSetUpAreIgnored()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.LatestTicketIds = [new TicketId { Id = 42 }];
            test.ApiConn.LatestTicket = CreateRunningTicket();
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);
            popup.WaitForAssertion(() => Assert.That(test.ApiConn.TaskSubscriptions, Has.Count.EqualTo(5)));
            typeof(RequestFwChangePopup).GetField("SubscriptionsSetupComplete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(popup.Instance, false);

            test.ApiConn.TaskSubscriptions[0].OnUpdate([new WfReqTask { Id = 1001, Title = "too early" }]);

            Assert.That(GetTaskList(popup)[0].Title, Is.EqualTo("access <b>task</b>"));
        }

        [Test]
        public async Task FinishedRequestOffersTheNewChanges()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.LatestTicketIds = [new TicketId { Id = 42 }];
            WfTicket finishedTicket = CreateRunningTicket();
            finishedTicket.StateId = kDoneStateId;
            test.ApiConn.LatestTicket = finishedTicket;

            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain("conn61"));
                Assert.That(test.ApiConn.TaskSubscriptions, Is.Empty);
                Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task StartRequestsCreatesTheTicketAndMarksTheRequestedConnections()
        {
            await using PopupTestContext test = CreatePopupContext();
            ModellingConnection alreadyRequested = CreateRequestableConnection(62);
            alreadyRequested.RequestedOnFw = true;
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61), alreadyRequested]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.RefreshedTickets, Has.Count.EqualTo(1));
                Assert.That(test.ApiConn.FwRequestedConnectionIds, Is.EqualTo(new List<int> { 61 }));
                Assert.That(test.ApiConn.SavedTicketIds, Is.EqualTo(new List<long> { test.RefreshedTickets[0].Id }));
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("U9020"), test.UserConfig.GetText("U0016"), false)));
                Assert.That(test.Middleware.ExternalRequestBodies, Is.Empty, "notification requests need no external requests");
                Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.True);
            });
        }

        [TestCase(HttpStatusCode.OK, "true", false)]
        [TestCase(HttpStatusCode.OK, "false", true)]
        [TestCase(HttpStatusCode.InternalServerError, "true", true)]
        public async Task StartRequestsCreatesTheExternalRequests(HttpStatusCode statusCode, string body, bool expectError)
        {
            await using PopupTestContext test = CreatePopupContext();
            test.Middleware.StatusCode = statusCode;
            test.Middleware.Body = body;
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));
            // the tasks are built in workflow notification mode; the request is then sent the integrated way
            test.UserConfig.ModIntegrationMode = ModIntegrationMode.FullyIntegrated;

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Middleware.ExternalRequestBodies, Has.Count.EqualTo(1));
                Assert.That(test.ApiConn.FwRequestedConnectionIds, Is.EqualTo(new List<int> { 61 }));
                Assert.That(test.Messages.Contains(new UiMessage(test.UserConfig.GetText("request_fw_change"), test.UserConfig.GetText("E9101"), true)), Is.EqualTo(expectError));
                Assert.That(test.RefreshedTickets, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task StartRequestsUpdatesThePlannedAppZoneFirst()
        {
            await using PopupTestContext test = CreatePopupContext();
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));
            // set by the variance analysis of the integrated mode, which plans the app zone of the application
            ModellingAppZone plannedAppZone = new() { Id = 77, AlreadyExistsInDb = true };
            plannedAppZone.AppServersNew.Add(new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = 9, Name = "new-server" } });
            typeof(RequestFwChangePopup).GetField("PlannedAppZoneDbUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(popup.Instance, plannedAppZone);

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.ApiConn.AppZoneLinks, Is.EqualTo(new List<(int, long)> { (9, 77) }));
                Assert.That(test.RefreshedTickets, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task StartRequestsReportsAMissingMiddlewareClient()
        {
            await using PopupTestContext test = CreatePopupContext();
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));
            test.UserConfig.ModIntegrationMode = ModIntegrationMode.FullyIntegrated;
            // the ticket is created without the middleware, only the external requests need it
            typeof(RequestFwChangePopup).GetProperty("middlewareClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(popup.Instance, null);

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("request_fw_change") && message.Error));
                Assert.That(test.RefreshedTickets, Has.Count.EqualTo(1), "the ticket exists even without external requests");
            });
        }

        [Test]
        public async Task StartRequestsReportsAnUnreachableMiddleware()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.Middleware.Fail = true;
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));
            test.UserConfig.ModIntegrationMode = ModIntegrationMode.FullyIntegrated;

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("request_fw_change"), test.UserConfig.GetText("E9101"), true)));
                Assert.That(test.RefreshedTickets, Has.Count.EqualTo(1), "the ticket exists even without external requests");
            });
        }

        [Test]
        public async Task StartRequestsReportsAFailedTicketCreation()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.FailingQueries.Add(RequestQueries.newTicket);
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.RefreshedTickets, Is.Empty);
                Assert.That(test.ApiConn.FwRequestedConnectionIds, Is.Empty);
                Assert.That(test.ApiConn.SavedTicketIds, Is.Empty);
                Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False, "the request can be tried again");
            });
        }

        [Test]
        public async Task StartRequestsReportsAFailureWhileMarkingTheConnections()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.FailingQueries.Add(ModellingQueries.updateConnectionFwRequested);
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("request_fw_change") && message.Error));
                Assert.That(test.RefreshedTickets, Is.Empty);
                Assert.That(popup.FindComponent<FWO.Ui.Shared.InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task StartRequestsReportsThatTheTicketIdCouldNotBeSaved()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.FailingQueries.Add(ExtRequestQueries.addTicketId);
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test, [CreateRequestableConnection(61)]);
            popup.WaitForAssertion(() => Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False));

            await popup.InvokeAsync(() => RequestButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("request_fw_change") && message.Error));
                Assert.That(test.RefreshedTickets, Has.Count.EqualTo(1), "the request goes on without the saved ticket id");
            });
        }

        [Test]
        public async Task ReportsAFailureWhileLoadingTheBaseData()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.FailingQueries.Add(StmQueries.getIpProtocols);

            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);

            popup.WaitForAssertion(() =>
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("fetch_data") && message.Error)));
        }

        [Test]
        public async Task ReportsAFailureWhileLoadingTheTasks()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.FailingQueries.Add(ExtRequestQueries.getLatestTicketId);

            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("request_fw_change") && message.Error));
                Assert.That(popup.FindComponent<FWO.Ui.Shared.InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task IntegratedModeAnalysesTheConnectionsAgainAfterReopening()
        {
            await using PopupTestContext test = CreatePopupContext(ModIntegrationMode.FullyIntegrated);
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);
            popup.WaitForAssertion(() => Assert.That(popup.FindComponent<FWO.Ui.Shared.InProgress>().Instance.Display, Is.False));
            int latestTicketQueries = test.ApiConn.PopupQueries.Count(query => query == ExtRequestQueries.getLatestTicketId);

            await popup.InvokeAsync(() => popup.FindAll("button").Single(button => button.TextContent.Trim() == test.UserConfig.GetText("close")).Click());
            popup.Render(parameters => parameters.Add(p => p.Display, true));

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
                Assert.That(test.ApiConn.PopupQueries.Count(query => query == ExtRequestQueries.getLatestTicketId), Is.EqualTo(latestTicketQueries + 1));
            });
        }

        [Test]
        public async Task ClosingIgnoresFailuresWhileDisposingTheSubscriptions()
        {
            await using PopupTestContext test = CreatePopupContext();
            test.ApiConn.LatestTicketIds = [new TicketId { Id = 42 }];
            test.ApiConn.LatestTicket = CreateRunningTicket();
            IRenderedComponent<RequestFwChangePopup> popup = RenderPopup(test);
            popup.WaitForAssertion(() => Assert.That(test.ApiConn.TaskSubscriptions, Has.Count.EqualTo(5)));
            test.ApiConn.FailDisposingSubscriptions = true;

            await popup.InvokeAsync(() => popup.FindAll("button").Single(button => button.TextContent.Trim() == test.UserConfig.GetText("close")).Click());

            Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
        }
    }
}

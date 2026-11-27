using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.RuleTreeBuilder;
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
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal partial class UiNetworkModellingPageTest
    {
        private static readonly List<string> kAuditorRoles = [Roles.Auditor];

        [Test]
        public async Task Render_SelectsRouteAppAndLoadsConnections()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-B");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Beta App"));
                Assert.That(apiConn.ConnectionQueryAppIds, Does.Contain(20));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_ModellerForSelectedOwnerCanUseModellingActions()
        {
            await using BunitContext context = CreateContext([Roles.Modeller], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Ownerships = [10];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement addConnectionButton = FindButton(page, "add_connection");
                IElement requestButton = FindButton(page, "Request firewall changes");
                Assert.That(addConnectionButton.HasAttribute("disabled"), Is.False);
                Assert.That(requestButton.HasAttribute("disabled"), Is.False);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_MixedUserUsesModellerRoleForWorkflowLookup()
        {
            await using BunitContext context = CreateContext([Roles.Auditor, Roles.Modeller], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Ownerships = [10];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                Assert.That(apiConn.ExtRequestRoles, Does.Contain(Roles.Modeller));
                Assert.That(apiConn.ExtRequestRoles, Does.Not.Contain(""));
            });
        }

        [Test]
        public async Task Render_AuditorCanModelButCannotSaveOrRequestChanges()
        {
            await using BunitContext context = CreateContext([Roles.Auditor], out NetworkModellingPageTestApiConn apiConn, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement addConnectionButton = FindButton(page, "add_connection");
                IElement editButton = FindButton(page, "edit");
                IElement requestButton = FindButton(page, "Request firewall changes");
                Assert.That(addConnectionButton.HasAttribute("disabled"), Is.False);
                Assert.That(editButton.HasAttribute("disabled"), Is.False);
                Assert.That(requestButton.HasAttribute("disabled"), Is.False);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });

            FindButton(page, "add_connection").Click();

            page.WaitForAssertion(() =>
            {
                IElement saveButton = FindButton(page, "save");
                Assert.That(saveButton.HasAttribute("disabled"), Is.True);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_AdminExecutionModeCanOpenRecertPopupButCannotSubmit()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Recertifier], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.RecertOwnerships = [10];
            userConfig.SetExecutionMode(Roles.Admin);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement recertButton = FindButton(page, "recertify");
                Assert.That(recertButton.HasAttribute("disabled"), Is.False);
                Assert.That(page.FindComponent<RequestRecertPopup>().Instance.CanRecertify, Is.False);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_AuditorCanOpenRequestFwChangePopupButCannotSubmit()
        {
            await using BunitContext context = CreateContext(kAuditorRoles, out _, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));
            FindButton(page, "Request firewall changes").Click();

            page.WaitForAssertion(() =>
            {
                IRenderedComponent<RequestFwChangePopup> popup = page.FindComponent<RequestFwChangePopup>();
                Assert.That(popup.Instance.Display, Is.True);
                List<IElement> submitButtons = [.. popup.FindAll("button")
                    .Where(button => button.TextContent.Trim() == "Request firewall changes")];
                Assert.That(submitButtons, Is.Not.Empty);
                Assert.That(submitButtons.All(button => button.HasAttribute("disabled")), Is.True);
                Assert.That(page.Markup, Does.Not.Contain("E9104"));
            });
        }

        [Test]
        public async Task Render_AuditorCanOpenRecertPopupButCannotSubmit()
        {
            await using BunitContext context = CreateContext(kAuditorRoles, out NetworkModellingPageTestApiConn apiConn, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement recertButton = FindButton(page, "recertify");
                Assert.That(recertButton.HasAttribute("disabled"), Is.False);
                Assert.That(page.FindComponent<RequestRecertPopup>().Instance.CanRecertify, Is.False);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_RecertifierAssignedToSelectedOwnerCanRecertify()
        {
            await using BunitContext context = CreateContext([Roles.Modeller, Roles.Recertifier], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Ownerships = [10];
            userConfig.User.RecertOwnerships = [10];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement recertButton = FindButton(page, "recertify");
                Assert.That(recertButton.HasAttribute("disabled"), Is.False);
                Assert.That(page.FindComponent<RequestRecertPopup>().Instance.CanRecertify, Is.True);
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_ModellerWithoutRecertifierRoleGetsRecertRoleTooltip()
        {
            await using BunitContext context = CreateContext([Roles.Modeller], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Ownerships = [10];
            userConfig.User.RecertOwnerships = [10];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement recertButton = FindButton(page, "recertify");
                Assert.That(recertButton.HasAttribute("disabled"), Is.True);
                Assert.That(recertButton.ParentElement?.GetAttribute("title"), Is.EqualTo("You need the recertifier role to recertify this owner."));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Render_RecertifierWithoutOwnerAssignmentGetsOwnerTooltip()
        {
            await using BunitContext context = CreateContext([Roles.Modeller, Roles.Recertifier], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Ownerships = [10];
            userConfig.User.RecertOwnerships = [20];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement recertButton = FindButton(page, "recertify");
                Assert.That(recertButton.HasAttribute("disabled"), Is.True);
                Assert.That(recertButton.ParentElement?.GetAttribute("title"), Is.EqualTo("You are not assigned to this owner as a recertifiable responsible person."));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task ReportButtonNavigatesToReportGenerationForSelectedApp()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-B");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Beta App"));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
            FindButton(page, userConfig.GetText("generate_report")).Click();

            NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
            Assert.That(navigation.Uri, Does.EndWith("/report/generation/20"));
        }

        [Test]
        public async Task Render_ReactsToModIconifyChange_ForGenerateReportButton()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out SimulatedUserConfig userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<NetworkModelling>(child =>
                {
                    child.Add(page => page.AppId, "APP-A");
                }));
            IRenderedComponent<NetworkModelling> page = wrapper.FindComponent<NetworkModelling>();

            page.WaitForAssertion(() =>
            {
                IReadOnlyList<IElement> buttons = page.FindAll(".btn-group button");
                Assert.That(buttons[0].TextContent.Trim(), Is.EqualTo(userConfig.GetText("generate_report")));
            });

            List<ConfigItem> changedItems =
            [
                new ConfigItem { Key = "modIconify", Value = "true", User = userConfig.User.DbId }
            ];
            await page.InvokeAsync(() => userConfig.SubscriptionUpdateHandler(changedItems.ToArray()));

            page.WaitForAssertion(() =>
            {
                IReadOnlyList<IElement> buttons = page.FindAll(".btn-group button");
                Assert.That(buttons[0].InnerHtml, Does.Contain(Icons.GenerateReport));
            });

            wrapper.Dispose();
        }

        private static BunitContext CreateContext(
            IEnumerable<string> roles,
            out NetworkModellingPageTestApiConn apiConn,
            out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();

            apiConn = new NetworkModellingPageTestApiConn();
            userConfig = CreateUserConfig(roles);
            apiConn.SetAmbientRole(CreatePrincipal(roles), [Roles.Modeller, Roles.Admin]);

            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<AuthenticationStateProvider>(new NetworkModellingAuthStateProvider(roles));
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<IRuleTreeBuilder, RuleTreeBuilder>();
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new EventMediator());

            return context;
        }

        private static IRenderedComponent<NetworkModelling> RenderPage(BunitContext context, string? appId = null, string? connId = null)
        {
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<NetworkModelling>(child =>
                {
                    if (appId != null)
                    {
                        child.Add(page => page.AppId, appId);
                    }
                    if (connId != null)
                    {
                        child.Add(page => page.ConnId, connId);
                    }
                }));
            return wrapper.FindComponent<NetworkModelling>();
        }

        private static SimulatedUserConfig CreateUserConfig(IEnumerable<string> roles)
        {
            SimulatedUserConfig userConfig = new()
            {
                ModIconify = false,
                ModRolloutActive = true,
                ModRecertActive = true,
                ModIntegrationMode = ModIntegrationMode.FullyIntegrated,
                ModNamingConvention = "{}",
                ModExtraConfigs = "[]",
                ModModelledMarker = "FWO:",
                VarianceAnalysisSync = false,
                VarianceAnalysisRefresh = false,
                AllowServiceInConn = true,
                User =
                {
                    Roles = [.. roles],
                    Ownerships = [],
                    RecertOwnerships = []
                }
            };
            foreach ((string key, string text) in TestTranslations())
            {
                userConfig.Translate[key] = text;
            }
            return userConfig;
        }

        private static ClaimsPrincipal CreatePrincipal(IEnumerable<string> roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
            [
                .. roles.Select(role => new Claim(ClaimTypes.Role, role)),
                new Claim(ClaimTypes.Name, "test.user")
            ], "Test", ClaimTypes.Name, ClaimTypes.Role));
        }

        private static Dictionary<string, string> TestTranslations()
        {
            return new()
            {
                ["network_modelling"] = "Network modelling",
                ["application"] = "Application",
                ["common_service"] = "Common service",
                ["connections"] = "Connections",
                ["provided_interfaces"] = "Provided interfaces",
                ["common_services"] = "Common services",
                ["generate_report"] = "Generate report",
                ["show_history"] = "Show history",
                ["request_fw_change"] = "Request firewall changes",
                ["recertify"] = "Recertify",
                ["add_connection"] = "Add connection",
                ["add_interface"] = "Add interface",
                ["add_common_service"] = "Add common service",
                ["edit"] = "Edit",
                ["save"] = "Save",
                ["comm_profile"] = "Communication profile",
                ["share_link"] = "Share link",
                ["edit_app_server"] = "Edit app server",
                ["delete_connection"] = "Delete connection",
                ["delete_interface"] = "Delete interface",
                ["fetch_data"] = "Fetch data",
                ["never_requested"] = "Never requested",
                ["last_recertified"] = "Last recertified",
                ["last_recertifier"] = "Last recertifier",
                ["next_recertification"] = "Next recertification",
                ["C9031"] = "You need the recertifier role to recertify this owner.",
                ["C9032"] = "You are not assigned to this owner as a recertifiable responsible person."
            };
        }

        private static IElement FindButton(IRenderedComponent<NetworkModelling> page, string text)
        {
            List<IElement> matches = [.. page.FindAll("button")
                .Where(button => string.Equals(button.TextContent.Trim(), text, StringComparison.OrdinalIgnoreCase))];
            if (matches.Count == 1)
            {
                return matches[0];
            }

            string visibleButtons = string.Join(", ", page.FindAll("button").Select(button => $"'{button.TextContent.Trim()}'"));
            Assert.Fail($"Expected exactly one button containing '{text}', found {matches.Count}. Buttons: {visibleButtons}");
            throw new InvalidOperationException("Unreachable after Assert.Fail.");
        }

        private sealed class NetworkModellingAuthStateProvider(IEnumerable<string> roles) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)).Append(new Claim(ClaimTypes.Name, "test.user")),
                "Test",
                ClaimTypes.Name,
                ClaimTypes.Role));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class NetworkModellingPageTestApiConn : SimulatedApiConnection
        {
            public List<int> ConnectionQueryAppIds { get; } = [];
            public List<string> UnexpectedQueries { get; } = [];
            public List<string> ExtRequestRoles { get; } = [];

            /// <summary>
            /// Ticket ids answered for the latest firewall change request; empty if the app was never requested.
            /// </summary>
            public List<TicketId> LatestTicketIds { get; set; } = [new() { Id = 42 }];

            /// <summary>
            /// Workflow state of the answered ticket: 10 is in progress, 90 done, 91 rejected.
            /// </summary>
            public int TicketStateId { get; set; } = 10;

            /// <summary>
            /// Answer for the last external request of a ticket; null answers the default request.
            /// </summary>
            public List<ExternalRequest>? LastRequests { get; set; }

            /// <summary>
            /// Connections answered for every app; null answers the default connection per app.
            /// </summary>
            public List<ModellingConnection>? Connections { get; set; }

            /// <summary>
            /// Queries answered by an exception, to reach the error handling of the page.
            /// </summary>
            public HashSet<string> FailingQueries { get; } = [];

            /// <summary>
            /// Holds the answer to the state query back until completed, so the page can be seen while loading.
            /// </summary>
            public TaskCompletionSource? StatesGate { get; set; }

            /// <summary>
            /// Update handler of the last subscription to ticket state changes, to simulate a ticket update.
            /// </summary>
            public GraphQlApiSubscription<List<WfTicket>>.SubscriptionUpdate? TicketUpdateHandler { get; private set; }

            /// <summary>
            /// Error handler of the last subscription to ticket state changes, to simulate a failing subscription.
            /// </summary>
            public Action<Exception>? TicketSubscriptionErrorHandler { get; private set; }

            private readonly Stack<string> previousRoles = new();
            public string ActiveRole { get; private set; } = "";

            public List<FwoOwner> Owners { get; set; } =
            [
                CreateOwner(10, "Alpha App", "APP-A"),
                CreateOwner(20, "Beta App", "APP-B")
            ];

            // one answer per query, so a query added to the page needs an entry here instead of
            // another branch in the dispatch below
            private readonly Dictionary<string, Func<object?, object>> queryAnswers;

            public NetworkModellingPageTestApiConn()
            {
                queryAnswers = CreateQueryAnswers();
            }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException($"query failed: {query}");
                }
                if (query == RequestQueries.getStates && StatesGate != null)
                {
                    await StatesGate.Task;
                }
                if (queryAnswers.TryGetValue(query, out Func<object?, object>? answer))
                {
                    return (QueryResponseType)answer(variables);
                }

                UnexpectedQueries.Add(query);
                return default!;
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null,
                string? operationName = null)
            {
                if (subscriptionUpdateHandler is GraphQlApiSubscription<List<WfTicket>>.SubscriptionUpdate ticketUpdateHandler)
                {
                    TicketUpdateHandler = ticketUpdateHandler;
                    TicketSubscriptionErrorHandler = exceptionHandler;
                }
                return new SimulatedApiSubscription<SubscriptionResponseType>(this,
                    new GraphQLHttpClient(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new HttpClient()),
                    new GraphQLRequest(subscription, variables, operationName), exceptionHandler, subscriptionUpdateHandler);
            }

            /// <summary>
            /// The answer this stub gives to every query the modelling page sends, by query text.
            /// </summary>
            private Dictionary<string, Func<object?, object>> CreateQueryAnswers()
            {
                return new()
                {
                    [RequestQueries.getStates] = _ => new List<WfState>
                    {
                        new() { Id = 10, Name = "In progress" },
                        new() { Id = 90, Name = "Done" },
                        new() { Id = 91, Name = "Rejected" }
                    },
                    [RequestQueries.getExtStates] = _ => new List<WfExtState>
                    {
                        new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                        new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                    },
                    [StmQueries.getIpProtocols] = _ => new List<IpProtocol>
                    {
                        new() { Id = 6, Name = "tcp" },
                        new() { Id = 17, Name = "udp" }
                    },
                    [StmQueries.getRuleActions] = _ => new List<RuleAction>(),
                    [StmQueries.getTracking] = _ => new List<Tracking>(),
                    [DeviceQueries.getDeviceDetails] = _ => new List<Device>(),
                    [OwnerQueries.getOwnersWithConn] = _ => Owners.Select(owner => new FwoOwner(owner)).ToList(),
                    [OwnerQueries.getEditableOwnersWithConn] = variables => AnswerEditableOwners(variables),
                    [ModellingQueries.getConnectionsResolved] = variables => AnswerConnections(variables),
                    [ModellingQueries.getDummyAppRole] = _ => new List<ModellingAppRole> { new() { Id = 999, IdString = "DUMMY", Name = "Dummy" } },
                    [ModellingQueries.getSelectedConnections] = _ => new List<ModellingConnectionWrapper>(),
                    [ModellingQueries.getAppServersForOwner] = _ => new List<ModellingAppServer>(),
                    [ModellingQueries.getAppRoles] = _ => new List<ModellingAppRole>(),
                    [ModellingQueries.getNwGroupObjects] = _ => new List<ModellingNetworkArea>(),
                    [ModellingQueries.getAreas] = _ => new List<ModellingNetworkArea>(),
                    [ModellingQueries.getSelectedNwGroupObjects] = _ => new List<ModellingNwGroupWrapper>(),
                    [ModellingQueries.getGlobalServiceGroups] = _ => new List<ModellingServiceGroup>(),
                    [ModellingQueries.getServiceGroupsForApp] = _ => new List<ModellingServiceGroup>(),
                    [ModellingQueries.getGlobalServices] = _ => new List<ModellingService>(),
                    [ModellingQueries.getServicesForApp] = _ => new List<ModellingService>(),
                    [ExtRequestQueries.getLatestTicketId] = _ => AnswerLatestTicketId(),
                    [RequestQueries.getTicketById] = variables => AnswerTicket(variables),
                    [ExtRequestQueries.getLastRequest] = variables => AnswerLastRequest(variables),
                    [ModellingQueries.deleteConnection] = variables => new ReturnId { DeletedId = GetIntVariable(variables, "id") },
                    [ModellingQueries.addHistoryEntry] = _ => new ReturnIdWrapper()
                };
            }

            private List<FwoOwner> AnswerEditableOwners(object? variables)
            {
                int[] appIds = GetIntArrayVariable(variables, "appIds");
                return Owners.Where(owner => appIds.Contains(owner.Id)).Select(owner => new FwoOwner(owner)).ToList();
            }

            private List<ModellingConnection> AnswerConnections(object? variables)
            {
                int appId = GetIntVariable(variables, "appId");
                ConnectionQueryAppIds.Add(appId);
                RecordQueryRole();
                return Connections?.Select(connection => new ModellingConnection(connection)).ToList() ?? CreateConnections(appId);
            }

            private List<TicketId> AnswerLatestTicketId()
            {
                RecordQueryRole();
                return [.. LatestTicketIds];
            }

            private WfTicket AnswerTicket(object? variables)
            {
                return new()
                {
                    Id = GetLongVariable(variables, "Id"),
                    StateId = TicketStateId,
                    CreationDate = new DateTime(2026, 7, 1, 10, 0, 0),
                    Requester = new UiUser { Name = "requester" }
                };
            }

            private List<ExternalRequest> AnswerLastRequest(object? variables)
            {
                RecordQueryRole();
                if (LastRequests != null)
                {
                    return LastRequests;
                }
                return
                [
                    new()
                    {
                        TicketId = GetLongVariable(variables, "ticketId"),
                        LastCreationResponse = "workflow lookup ok"
                    }
                ];
            }

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
            {
                previousRoles.Push(ActiveRole);
                ActiveRole = targetRoleList.FirstOrDefault(role => user.Claims.Any(claim => claim.Value.Equals(role, StringComparison.OrdinalIgnoreCase))) ?? "";
            }

            public override void SetAmbientRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                previousRoles.Push(ActiveRole);
                ActiveRole = targetRoleList.FirstOrDefault(role => user.Claims.Any(claim => claim.Value.Equals(role, StringComparison.OrdinalIgnoreCase))) ?? "";
            }

            public override void SwitchBack()
            {
                ActiveRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }

            private static int GetIntVariable(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value is int intValue ? intValue : 0;
            }

            private static long GetLongVariable(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value is long longValue ? longValue : 0;
            }

            private static int[] GetIntArrayVariable(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value as int[] ?? [];
            }

            private void RecordQueryRole()
            {
                ExtRequestRoles.Add(ActiveRole);
            }

            private static FwoOwner CreateOwner(int id, string name, string extAppId)
            {
                return new()
                {
                    Id = id,
                    Name = name,
                    ExtAppId = extAppId,
                    Active = true,
                    RecertActive = true,
                    CommSvcPossible = true,
                    LastRecertified = new DateTime(2026, 1, 10),
                    LastRecertifierDn = "cn=recert.user,ou=users,dc=test,dc=local",
                    NextRecertDate = new DateTime(2026, 7, 10),
                    ConnectionCount = new() { Aggregate = new() { Count = 1 } }
                };
            }

            private static List<ModellingConnection> CreateConnections(int appId)
            {
                return
                [
                    new()
                    {
                        Id = appId + 1,
                        AppId = appId,
                        Name = $"Connection {appId}",
                        IsInterface = false,
                        IsCommonService = false,
                        SourceAppServers = [new() { Content = new() { Id = appId + 100, Name = "Source", Ip = "10.0.0.1/32" } }],
                        DestinationAppServers = [new() { Content = new() { Id = appId + 200, Name = "Destination", Ip = "10.0.1.1/32" } }],
                        Services = [new() { Content = new() { Id = appId + 300, Name = "HTTPS", ProtoId = 6, Port = 443 } }]
                    }
                ];
            }
        }
    }
}

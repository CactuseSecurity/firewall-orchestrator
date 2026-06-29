using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Services;
using FWO.Ui.Pages.NetworkModelling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiNetworkModellingPageTest
    {
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
        public async Task Render_AuditorSeesReadOnlyModellingActions()
        {
            await using BunitContext context = CreateContext([Roles.Auditor], out NetworkModellingPageTestApiConn apiConn, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                IElement addConnectionButton = FindButton(page, "add_connection");
                IElement requestButton = FindButton(page, "Request firewall changes");
                Assert.That(addConnectionButton.HasAttribute("disabled"), Is.True);
                Assert.That(requestButton.HasAttribute("disabled"), Is.True);
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
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, appId: "APP-B");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Beta App"));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
            FindButton(page, "generate_report").Click();

            NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
            Assert.That(navigation.Uri, Does.EndWith("/report/generation/20"));
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
                .Where(button => button.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase))];
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

            private readonly List<FwoOwner> owners =
            [
                CreateOwner(10, "Alpha App", "APP-A"),
                CreateOwner(20, "Beta App", "APP-B")
            ];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getStates)
                {
                    return Result<QueryResponseType>(new List<WfState>
                    {
                        new() { Id = 10, Name = "In progress" },
                        new() { Id = 90, Name = "Done" }
                    });
                }
                if (query == RequestQueries.getExtStates)
                {
                    return Result<QueryResponseType>(new List<WfExtState>
                    {
                        new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                        new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                    });
                }
                if (query == StmQueries.getIpProtocols)
                {
                    return Result<QueryResponseType>(new List<IpProtocol>
                    {
                        new() { Id = 6, Name = "tcp" },
                        new() { Id = 17, Name = "udp" }
                    });
                }
                if (query == DeviceQueries.getDeviceDetails)
                {
                    return Result<QueryResponseType>(new List<Device>());
                }
                if (query == OwnerQueries.getOwnersWithConn)
                {
                    return Result<QueryResponseType>(owners.Select(owner => new FwoOwner(owner)).ToList());
                }
                if (query == OwnerQueries.getEditableOwnersWithConn)
                {
                    int[] appIds = GetIntArrayVariable(variables, "appIds");
                    return Result<QueryResponseType>(owners.Where(owner => appIds.Contains(owner.Id)).Select(owner => new FwoOwner(owner)).ToList());
                }
                if (query == ModellingQueries.getConnectionsResolved)
                {
                    int appId = GetIntVariable(variables, "appId");
                    ConnectionQueryAppIds.Add(appId);
                    return Result<QueryResponseType>(CreateConnections(appId));
                }
                if (query == ModellingQueries.getDummyAppRole)
                {
                    return Result<QueryResponseType>(new List<ModellingAppRole> { new() { Id = 999, IdString = "DUMMY", Name = "Dummy" } });
                }
                if (query == ModellingQueries.getSelectedConnections)
                {
                    return Result<QueryResponseType>(new List<ModellingConnectionWrapper>());
                }
                if (query == ModellingQueries.getAppServersForOwner)
                {
                    return Result<QueryResponseType>(new List<ModellingAppServer>());
                }
                if (query == ModellingQueries.getAppRoles)
                {
                    return Result<QueryResponseType>(new List<ModellingAppRole>());
                }
                if (query == ModellingQueries.getNwGroupObjects)
                {
                    return Result<QueryResponseType>(new List<ModellingNetworkArea>());
                }
                if (query == ModellingQueries.getAreas)
                {
                    return Result<QueryResponseType>(new List<ModellingNetworkArea>());
                }
                if (query == ModellingQueries.getSelectedNwGroupObjects)
                {
                    return Result<QueryResponseType>(new List<ModellingNwGroupWrapper>());
                }
                if (query == ModellingQueries.getGlobalServiceGroups || query == ModellingQueries.getServiceGroupsForApp)
                {
                    return Result<QueryResponseType>(new List<ModellingServiceGroup>());
                }
                if (query == ModellingQueries.getGlobalServices || query == ModellingQueries.getServicesForApp)
                {
                    return Result<QueryResponseType>(new List<ModellingService>());
                }
                if (query == ExtRequestQueries.getLatestTicketId)
                {
                    return Result<QueryResponseType>(new List<TicketId>());
                }

                UnexpectedQueries.Add(query);
                return Task.FromResult(default(QueryResponseType)!);
            }

            private static Task<T> Result<T>(object value)
            {
                return Task.FromResult((T)value);
            }

            private static int GetIntVariable(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value is int intValue ? intValue : 0;
            }

            private static int[] GetIntArrayVariable(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value as int[] ?? [];
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

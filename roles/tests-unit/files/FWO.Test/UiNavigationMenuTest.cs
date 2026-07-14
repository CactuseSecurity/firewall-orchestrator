using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiNavigationMenuTest
    {
        [Test]
        public void NavigationMenu_IgnoresDisposedServicesDuringInitialization()
        {
            using BunitContext context = new();
            UserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Requester];
            userConfig.Dispose();

            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(new DisposedNavigationApiConnection());
            context.Services.AddSingleton<AuthenticationStateProvider>(new NavigationMenuAuthStateProvider([Roles.Requester]));

            Assert.DoesNotThrow(() => RenderMenu(context));
        }

        [Test]
        public async Task NavigationMenu_UsesApproverRoleForWorkflowInitializationAndShowsRelevantLinks()
        {
            await using BunitContext context = CreateContext([Roles.Modeller, Roles.Approver, Roles.Planner], approvalActive: true, planningActive: true,
                out NavigationMenuTestApiConnection apiConnection, out SimulatedUserConfig userConfig);

            IRenderedComponent<NavigationMenu> menu = RenderMenu(context);

            menu.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.LastTargetRoles, Is.EqualTo(new[]
                {
                    Roles.Requester, Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer,
                    Roles.Admin, Roles.FwAdmin, Roles.Auditor
                }));
                Assert.That(apiConnection.QueryRoles, Is.EqualTo(new[] { Roles.Approver, Roles.Approver }));
                Assert.That(menu.Markup, Does.Contain("networkmodelling"));
                Assert.That(menu.Markup, Does.Contain("/request/approvals"));
                Assert.That(menu.Markup, Does.Contain("/monitoring"));
                Assert.That(userConfig.User.Name.ToUpperInvariant(), Is.EqualTo("APPROVERPLANNER"));
            });
        }

        [Test]
        public async Task NavigationMenu_UsesPlannerRoleWhenApprovalPhaseIsInactive()
        {
            await using BunitContext context = CreateContext([Roles.Modeller, Roles.Approver, Roles.Planner], approvalActive: false, planningActive: true,
                out NavigationMenuTestApiConnection apiConnection, out _);

            IRenderedComponent<NavigationMenu> menu = RenderMenu(context);

            menu.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.QueryRoles, Is.EqualTo(new[] { Roles.Approver, Roles.Approver }));
                Assert.That(menu.Markup, Does.Contain("/request/plannings"));
                Assert.That(menu.Markup, Does.Contain("networkmodelling"));
            });
        }

        private static BunitContext CreateContext(IEnumerable<string> roles, bool approvalActive, bool planningActive,
            out NavigationMenuTestApiConnection apiConnection, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());

            apiConnection = new NavigationMenuTestApiConnection(approvalActive, planningActive);
            userConfig = new SimulatedUserConfig
            {
                User =
                {
                    Name = "approverplanner",
                    Roles = roles.ToList()
                }
            };

            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<AuthenticationStateProvider>(new NavigationMenuAuthStateProvider(roles));
            return context;
        }

        private static IRenderedComponent<NavigationMenu> RenderMenu(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                    .AddChildContent<NavigationMenu>())
                .FindComponent<NavigationMenu>();
        }

        private static string CreateWorkflowConfigJson(bool approvalActive, bool planningActive)
        {
            Dictionary<string, object?> CreatePhase(bool active) => new()
            {
                ["matrix"] = new Dictionary<string, object>(),
                ["derived_states"] = new Dictionary<string, object>(),
                ["lowest_input_state"] = 1,
                ["lowest_start_state"] = 2,
                ["lowest_end_state"] = 3,
                ["active"] = active
            };

            object config = new
            {
                config_value = new Dictionary<string, object?>
                {
                    ["request"] = CreatePhase(true),
                    ["approval"] = CreatePhase(approvalActive),
                    ["planning"] = CreatePhase(planningActive),
                    ["verification"] = CreatePhase(false),
                    ["implementation"] = CreatePhase(false),
                    ["review"] = CreatePhase(false),
                    ["recertification"] = CreatePhase(false)
                }
            };

            return JsonSerializer.Serialize(config);
        }

        private sealed class NavigationMenuAuthStateProvider(IEnumerable<string> roles) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class DisposedNavigationApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new ObjectDisposedException(nameof(DisposedNavigationApiConnection));
            }
        }

        private sealed class NavigationMenuTestApiConnection : SimulatedApiConnection
        {
            private readonly Stack<string> previousRoles = new();
            private readonly bool approvalActive;
            private readonly bool planningActive;

            public string ActiveRole { get; private set; } = "";
            public List<string> LastTargetRoles { get; private set; } = [];
            public List<string> QueryRoles { get; } = [];

            public NavigationMenuTestApiConnection(bool approvalActive, bool planningActive)
            {
                this.approvalActive = approvalActive;
                this.planningActive = planningActive;
            }

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                LastTargetRoles = [.. targetRoleList];
                string selectedRole = targetRoleList.First(role => user.IsInRole(role));
                SetRole(selectedRole);
            }

            public override void SetRole(string role)
            {
                previousRoles.Push(ActiveRole);
                ActiveRole = role;
            }

            public override void SwitchBack()
            {
                ActiveRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ConfigQueries.getConfigItemByKey && typeof(QueryResponseType) == typeof(List<ConfigItem>))
                {
                    QueryRoles.Add(ActiveRole);
                    return Task.FromResult((QueryResponseType)(object)new List<ConfigItem>
                    {
                        new() { Value = CreateWorkflowConfigJson(approvalActive, planningActive) }
                    });
                }

                if (query == RequestQueries.getActiveStateMatrixConfiguration && typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>))
                {
                    QueryRoles.Add(ActiveRole);
                    return Task.FromResult((QueryResponseType)(object)StateMatrixConfigurationTestHelper.FromLegacyJson(CreateWorkflowConfigJson(approvalActive, planningActive)));
                }

                if (query == RequestQueries.getStates && typeof(QueryResponseType) == typeof(List<WfState>))
                {
                    QueryRoles.Add(ActiveRole);
                    return Task.FromResult((QueryResponseType)(object)new List<WfState>());
                }

                throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
            }
        }
    }
}

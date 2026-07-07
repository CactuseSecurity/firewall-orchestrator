using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiRequestLayoutTest
    {
        [Test]
        public async Task RequestLayout_UsesApproverRoleForWorkflowInitializationAndShowsApprovalTab()
        {
            await using BunitContext context = CreateContext([Roles.Modeller, Roles.Approver, Roles.Planner], approvalActive: true, planningActive: true,
                out RequestLayoutTestApiConnection apiConnection);

            IRenderedComponent<RequestLayout> layout = RenderLayout(context);

            layout.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.LastTargetRoles, Is.EqualTo(new[]
                {
                    Roles.Requester, Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer,
                    Roles.Admin, Roles.FwAdmin, Roles.Auditor
                }));
                Assert.That(apiConnection.QueryRoles, Is.EqualTo(new[] { Roles.Approver, Roles.Approver }));
                StateMatrix stateMatrix = GetPrivateField<StateMatrix>(layout.Instance, "stateMatrix");
                Assert.That(stateMatrix.PhaseActive[WorkflowPhases.request], Is.True);
                Assert.That(stateMatrix.PhaseActive[WorkflowPhases.approval], Is.True);
                Assert.That(stateMatrix.PhaseActive[WorkflowPhases.planning], Is.True);
            });
        }

        [Test]
        public async Task RequestLayout_NullAuthenticationStateTaskFallsBackToDirectInitialization()
        {
            await using BunitContext context = CreateContext([Roles.Requester], approvalActive: true, planningActive: false,
                out RequestLayoutTestApiConnection apiConnection, useAuthStateProvider: false);

            IRenderedComponent<RequestLayout> layout = RenderLayoutWithoutCascadingAuth(context);

            layout.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.LastTargetRoles, Is.Empty);
                Assert.That(apiConnection.QueryRoles, Has.Count.EqualTo(2));
                Assert.That(apiConnection.QueryRoles, Is.All.EqualTo(string.Empty));
                StateMatrix stateMatrix = GetPrivateField<StateMatrix>(layout.Instance, "stateMatrix");
                Assert.That(stateMatrix.PhaseActive[WorkflowPhases.request], Is.True);
            });
        }

        [Test]
        public async Task RequestLayout_InitializationFailureReportsInitEnvironmentError()
        {
            await using BunitContext context = CreateContext([Roles.Approver], approvalActive: true, planningActive: true,
                out RequestLayoutFailingApiConnection apiConnection);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            IRenderedComponent<RequestLayout> layout = RenderLayout(context, messages);

            layout.WaitForAssertion(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Init Environment"));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(apiConnection.SendQueryCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task RequestLayout_UserConfigChangeAndDispose_DoNotThrow()
        {
            await using BunitContext context = CreateContextWithUserConfig([Roles.Requester], approvalActive: true, planningActive: false,
                out RequestLayoutTestApiConnection apiConnection, out SimulatedUserConfig userConfig);

            IRenderedComponent<RequestLayout> layout = RenderLayout(context);

            layout.WaitForAssertion(() => Assert.That(apiConnection.QueryRoles, Has.Count.EqualTo(2)));

            Assert.DoesNotThrow(() => userConfig.SetExecutionMode(Roles.Admin));
            layout.WaitForAssertion(() => Assert.That(layout.Markup, Is.Not.Null.And.Not.Empty));

            layout.Instance.Dispose();

            Assert.DoesNotThrow(() => userConfig.SetExecutionMode(Roles.Auditor));
        }

        private static T GetPrivateField<T>(RequestLayout component, string fieldName)
        {
            FieldInfo? field = typeof(RequestLayout).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(RequestLayout).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static BunitContext CreateContext(IEnumerable<string> roles, bool approvalActive, bool planningActive,
            out RequestLayoutTestApiConnection apiConnection, bool useAuthStateProvider = true)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User =
                {
                    Name = "requestuser",
                    Roles = roles.ToList()
                }
            });

            apiConnection = new RequestLayoutTestApiConnection(approvalActive, planningActive);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            if (useAuthStateProvider)
            {
                context.Services.AddSingleton<AuthenticationStateProvider>(new RequestLayoutAuthStateProvider(roles));
            }

            return context;
        }

        private static BunitContext CreateContextWithUserConfig(IEnumerable<string> roles, bool approvalActive, bool planningActive,
            out RequestLayoutTestApiConnection apiConnection, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<DomEventService>();
            userConfig = new SimulatedUserConfig
            {
                User =
                {
                    Name = "requestuser",
                    Roles = roles.ToList()
                }
            };
            context.Services.AddSingleton<UserConfig>(userConfig);

            apiConnection = new RequestLayoutTestApiConnection(approvalActive, planningActive);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestLayoutAuthStateProvider(roles));

            return context;
        }

        private static BunitContext CreateContext(IEnumerable<string> roles, bool approvalActive, bool planningActive,
            out RequestLayoutFailingApiConnection apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User =
                {
                    Name = "requestuser",
                    Roles = roles.ToList()
                }
            });

            apiConnection = new RequestLayoutFailingApiConnection(approvalActive, planningActive);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestLayoutAuthStateProvider(roles));

            return context;
        }

        private static IRenderedComponent<RequestLayout> RenderLayout(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<RequestLayout>())
                .FindComponent<RequestLayout>();
        }

        private static IRenderedComponent<RequestLayout> RenderLayout(BunitContext context, List<(Exception? Exception, string Title, string Message, bool IsError)> messages)
        {
            Action<Exception?, string, string, bool> displayMessage = (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            };

            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, displayMessage)
                    .AddChildContent<RequestLayout>()))
                .FindComponent<RequestLayout>();
        }

        private static IRenderedComponent<RequestLayout> RenderLayoutWithoutCascadingAuth(BunitContext context)
        {
            return context.Render<RequestLayout>();
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

        private sealed class RequestLayoutAuthStateProvider(IEnumerable<string> roles) : AuthenticationStateProvider
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

        private class RequestLayoutTestApiConnection : SimulatedApiConnection
        {
            private readonly Stack<string> previousRoles = new();
            private readonly bool approvalActive;
            private readonly bool planningActive;

            public string ActiveRole { get; private set; } = "";
            public List<string> LastTargetRoles { get; private set; } = [];
            public List<string> QueryRoles { get; } = [];

            public RequestLayoutTestApiConnection(bool approvalActive, bool planningActive)
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
                if (query == ConfigQueries.getConfigItemByKey && typeof(QueryResponseType) == typeof(List<GlobalStateMatrixHelper>))
                {
                    QueryRoles.Add(ActiveRole);
                    return Task.FromResult((QueryResponseType)(object)new List<GlobalStateMatrixHelper>
                    {
                        new() { ConfData = CreateWorkflowConfigJson(approvalActive, planningActive) }
                    });
                }

                if (query == RequestQueries.getStates && typeof(QueryResponseType) == typeof(List<WfState>))
                {
                    QueryRoles.Add(ActiveRole);
                    return Task.FromResult((QueryResponseType)(object)new List<WfState>());
                }

                throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
            }
        }

        private sealed class RequestLayoutFailingApiConnection : RequestLayoutTestApiConnection
        {
            public int SendQueryCallCount { get; private set; }

            public RequestLayoutFailingApiConnection(bool approvalActive, bool planningActive)
                : base(approvalActive, planningActive)
            {
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                SendQueryCallCount++;
                throw new InvalidOperationException("boom");
            }
        }
    }
}

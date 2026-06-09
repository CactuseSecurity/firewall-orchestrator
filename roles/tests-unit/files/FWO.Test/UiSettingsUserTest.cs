using System.Reflection;
using System.Security.Claims;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsUserTest
    {
        [Test]
        public async Task SettingsUser_HidesExecutionModeDropdownForSingleAdminRole()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out _);

            IRenderedComponent<SettingsUser> component = RenderSettingsUser(context);

            component.WaitForAssertion(() =>
                Assert.That(component.FindAll("#dropdown-input-execution_mode_select"), Is.Empty));
        }

        [Test]
        public async Task SettingsUser_ShowsExecutionModeDropdownForAdminWithUserRole()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Modeller], out _, out _);

            IRenderedComponent<SettingsUser> component = RenderSettingsUser(context);

            component.WaitForAssertion(() =>
            {
                IRenderedComponent<Dropdown<string>> dropdown = component.FindComponent<Dropdown<string>>();
                Assert.That(dropdown.Instance.SelectedElement, Is.EqualTo(GlobalConst.kUserRolesSelection));
                Assert.That(dropdown.Instance.Elements, Is.EqualTo(new[] { GlobalConst.kUserRolesSelection, Roles.Admin }));
            });
        }

        [Test]
        public async Task SettingsUser_ShowsOnlyElevatedModesForAdminAndAuditor()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Auditor], out _, out _);

            IRenderedComponent<SettingsUser> component = RenderSettingsUser(context);

            component.WaitForAssertion(() =>
            {
                IRenderedComponent<Dropdown<string>> dropdown = component.FindComponent<Dropdown<string>>();
                Assert.That(dropdown.Instance.SelectedElement, Is.EqualTo(Roles.Admin));
                Assert.That(dropdown.Instance.Elements, Is.EqualTo(new[] { Roles.Admin, Roles.Auditor }));
            });
        }

        [Test]
        public async Task SettingsUser_SelectingAdminUpdatesApiConnectionAndUserConfig()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Modeller], out SettingsUserTestApiConnection apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<SettingsUser> component = RenderSettingsUser(context);

            IRenderedComponent<Dropdown<string>> dropdown = component.FindComponent<Dropdown<string>>();
            await SelectDropdownElement(dropdown, Roles.Admin);

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.SelectedExecutionMode, Is.EqualTo(Roles.Admin));
                Assert.That(userConfig.ExecutionMode, Is.EqualTo(Roles.Admin));
                Assert.That(apiConnection.SelectionUser.IsInRole(Roles.Admin), Is.True);
            });
        }

        private static BunitContext CreateContext(List<string> roles, out SettingsUserTestApiConnection apiConnection, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddScoped<DomEventService>();

            apiConnection = new SettingsUserTestApiConnection();
            userConfig = new SimulatedUserConfig
            {
                User = { Name = "testuser", Roles = roles }
            };

            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ExecutionModeStorage>(new ExecutionModeStorage(new MockProtectedSessionStorage()));
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsUserAuthStateProvider(roles));
            return context;
        }

        private static IRenderedComponent<SettingsUser> RenderSettingsUser(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsUser>())
                .FindComponent<SettingsUser>();
        }

        private static async Task SelectDropdownElement(IRenderedComponent<Dropdown<string>> dropdown, string role)
        {
            MethodInfo method = typeof(Dropdown<string>).GetMethod("SelectElement", BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null)
                ?? throw new MissingMethodException(typeof(Dropdown<string>).FullName, "SelectElement");
            await dropdown.InvokeAsync(async () => await (Task)method.Invoke(dropdown.Instance, [role])!);
        }

        private sealed class SettingsUserAuthStateProvider(IEnumerable<string> roles) : AuthenticationStateProvider
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

        private sealed class SettingsUserTestApiConnection : SimulatedApiConnection
        {
            public string SelectedExecutionMode { get; private set; } = "";
            public ClaimsPrincipal SelectionUser { get; private set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getUserByDbId)
                {
                    object users = new List<UiUser>
                    {
                        new()
                        {
                            Name = "testuser",
                            Email = "test@example.invalid",
                            LastLogin = new DateTime(2026, 5, 29, 8, 0, 0, DateTimeKind.Utc),
                            LastPasswordChange = new DateTime(2026, 5, 28, 8, 0, 0, DateTimeKind.Utc)
                        }
                    };
                    return Task.FromResult((QueryResponseType)users);
                }
                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            public override void SetExecutionMode(ClaimsPrincipal user, string role)
            {
                SelectionUser = user;
                SelectedExecutionMode = role;
            }

            public override string GetExecutionMode()
            {
                return SelectedExecutionMode;
            }
        }
    }
}

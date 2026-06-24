using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMainLayoutTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(MainLayout).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MainLayout).FullName, name);
        }

        private static FieldInfo GetPrivateField(string name)
        {
            return typeof(MainLayout).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(MainLayout).FullName, name);
        }

        private static void InvokeDisplayMessage(MainLayout layout, Exception? exception, string title, string message, bool errorFlag)
        {
            GetPrivateMethod("DisplayMessageInUi").Invoke(layout, [exception, title, message, errorFlag]);
        }

        private static T GetPrivateFieldValue<T>(MainLayout layout, string name)
        {
            return (T)GetPrivateField(name).GetValue(layout)!;
        }

        private static void SetPrivateFieldValue<T>(MainLayout layout, string name, T value)
        {
            GetPrivateField(name).SetValue(layout, value);
        }

        [Test]
        public async Task DisplayMessageInUi_Success_ShowsSuccessMessageAndWritesUiLog()
        {
            await using MainLayoutFixture fixture = new();
            MainLayout layout = fixture.Layout.Instance;

            InvokeDisplayMessage(layout, null, "Save", "Done", false);

            fixture.Layout.WaitForAssertion(() =>
            {
                Assert.That(fixture.Layout.Markup, Does.Contain("Save - Done"));
                Assert.That(fixture.Layout.Markup, Does.Contain("alert-success"));
            });
            fixture.ApiConnection.WaitForLogCount(1);
            Assert.That(fixture.ApiConnection.UiLogs[0], Is.EqualTo(new UiLogEntry(0, "Save", "Done")));
        }

        [Test]
        public async Task DisplayMessageInUi_UserWarning_ShowsWarningMessageAndWritesWarningLog()
        {
            await using MainLayoutFixture fixture = new();
            MainLayout layout = fixture.Layout.Instance;

            InvokeDisplayMessage(layout, null, "Careful", "Check this", true);

            fixture.Layout.WaitForAssertion(() =>
            {
                Assert.That(fixture.Layout.Markup, Does.Contain("Careful - Check this"));
                Assert.That(fixture.Layout.Markup, Does.Contain("alert-warning-override"));
            });
            fixture.ApiConnection.WaitForLogCount(1);
            Assert.That(fixture.ApiConnection.UiLogs[0], Is.EqualTo(new UiLogEntry(1, "Careful", "Check this")));
        }

        [Test]
        public async Task DisplayMessageInUi_ApiSchemaAccessError_UsesTranslatedApiAccessText()
        {
            await using MainLayoutFixture fixture = new();
            MainLayout layout = fixture.Layout.Instance;

            InvokeDisplayMessage(layout, new Exception("no such type exists in the schema: 'cidr'"), "", "", false);

            fixture.Layout.WaitForAssertion(() => Assert.That(fixture.Layout.Markup, Does.Contain("api_access - E0004")));
            fixture.ApiConnection.WaitForLogCount(1);
            Assert.That(fixture.ApiConnection.UiLogs[0], Is.EqualTo(new UiLogEntry(2, "api_access", "E0004")));
        }

        [Test]
        public async Task DisplayMessageInUi_GenericException_IncludesExceptionTextOnlyForErrorFlag()
        {
            await using MainLayoutFixture fixture = new();
            MainLayout layout = fixture.Layout.Instance;

            InvokeDisplayMessage(layout, new InvalidOperationException("exploded"), "Load", "Could not load", true);

            fixture.Layout.WaitForAssertion(() => Assert.That(fixture.Layout.Markup, Does.Contain("Load - Could not load: exploded . E0002")));
            fixture.ApiConnection.WaitForLogCount(1);
            UiLogEntry log = fixture.ApiConnection.UiLogs[0];
            Assert.That(log.Severity, Is.EqualTo(2));
            Assert.That(log.Cause, Is.EqualTo("Load"));
            Assert.That(log.Description, Is.EqualTo("Could not load: exploded . E0002"));
        }

        [Test]
        public async Task DisplayMessageInUi_JwtExpired_ShowsReloginMessageWithoutUiLog()
        {
            await using MainLayoutFixture fixture = new();
            MainLayout layout = fixture.Layout.Instance;

            InvokeDisplayMessage(layout, new Exception("JWTExpired"), "Token", "", true);

            fixture.Layout.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateFieldValue<bool>(layout, "showReloginDialog"), Is.True);
                Assert.That(fixture.Layout.Markup, Does.Contain("jwt_expired_title"));
                Assert.That(fixture.Layout.Markup, Does.Contain("jwt_expired_text"));
            });
            Assert.That(fixture.ApiConnection.UiLogs, Is.Empty);
        }

        [Test]
        public async Task EventMediator_PermissionChangedForCurrentUser_ShowsReloginDialog()
        {
            await using MainLayoutFixture fixture = new();

            fixture.EventMediator.Publish(nameof(PermissionChangedEvent), new PermissionChangedEvent(new(fixture.UserConfig.User.Dn)));

            fixture.Layout.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateFieldValue<bool>(fixture.Layout.Instance, "showReloginDialog"), Is.True);
                Assert.That(fixture.Layout.Markup, Does.Contain("permissions_text"));
            });
        }

        [Test]
        public async Task EventMediator_ReloginRequiredForCurrentUser_ShowsReloginDialog()
        {
            await using MainLayoutFixture fixture = new();

            fixture.EventMediator.Publish(nameof(ReloginRequiredEvent), new ReloginRequiredEvent(new(fixture.UserConfig.User.Dn)));

            fixture.Layout.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateFieldValue<bool>(fixture.Layout.Instance, "showReloginDialog"), Is.True);
                Assert.That(fixture.Layout.Markup, Does.Contain("jwt_expired_text"));
            });
        }

        [Test]
        public async Task OnAlertUpdate_WhenAdminHasOpenAlerts_ShowsNavigationAlertMarker()
        {
            await using MainLayoutFixture fixture = new(roles: [Roles.Admin]);

            GetPrivateMethod("OnAlertUpdate").Invoke(fixture.Layout.Instance, [new List<Alert> { new() { Id = 42 } }]);

            fixture.Layout.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateFieldValue<bool>(fixture.Layout.Instance, "showAlert"), Is.True);
                Assert.That(fixture.Layout.Markup, Does.Contain(Icons.Alarm));
            });
        }

        [Test]
        public async Task SetAlert_WritesUiAlert()
        {
            await using MainLayoutFixture fixture = new();

            await fixture.Layout.Instance.setAlert("Failure", "Description");

            Assert.That(fixture.ApiConnection.Alerts, Is.EqualTo(new[] { ("Failure", "Description") }));
        }

        [Test]
        public async Task OnInitializedAsync_StartsTokenRefreshCoordinator_AndDisposeStopsIt()
        {
            MainLayoutFixture fixture = new();
            try
            {
                Assert.That(fixture.TokenRefreshCoordinator.StartCallCount, Is.EqualTo(1));
                Assert.That(fixture.TokenRefreshCoordinator.StopCallCount, Is.EqualTo(0));
            }
            finally
            {
                await fixture.DisposeAsync();
            }

            Assert.That(fixture.TokenRefreshCoordinator.StopCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddUiLogEntry_WhenApiThrows_DoesNotPropagateException()
        {
            await using MainLayoutFixture fixture = new();
            fixture.ApiConnection.ThrowOnUiLog = true;

            Assert.DoesNotThrowAsync(async () => await fixture.Layout.Instance.AddUiLogEntry(2, "Cause", "Description"));
        }

        [Test]
        public async Task DisplayMessageInUi_WhenDisplayPathThrows_DoesNotPropagateException()
        {
            await using MainLayoutFixture fixture = new();
            MainLayout layout = fixture.Layout.Instance;
            SetPrivateFieldValue<List<FWO.Ui.Data.UIMessage>?>(layout, "UIMessages", null);

            Assert.DoesNotThrow(() => InvokeDisplayMessage(layout, null, "Save", "Done", false));
            Assert.That(fixture.ApiConnection.UiLogs, Is.Empty);
        }

        private sealed class MainLayoutFixture : IAsyncDisposable
        {
            private readonly BunitContext context = new();
            private readonly MockProtectedSessionStorage sessionStorage = new();

            public MainLayoutTestApiConnection ApiConnection { get; } = new();
            public SimulatedUserConfig UserConfig { get; }
            public EventMediator EventMediator { get; } = new();
            public MainLayoutTokenRefreshCoordinatorStub TokenRefreshCoordinator { get; } = new();
            public IRenderedComponent<MainLayout> Layout { get; }

            public MainLayoutFixture(int maxMessages = 3, IEnumerable<string>? roles = null)
            {
                List<string> userRoles = roles?.ToList() ?? [Roles.Reporter];
                context.JSInterop.Mode = JSRuntimeMode.Loose;
                context.Services.AddAuthorizationCore();
                context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
                context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
                UserConfig = new SimulatedUserConfig
                {
                    MessageViewTime = 30,
                    MaxMessages = maxMessages,
                    AvailableModules = "[]"
                };
                UserConfig.User.DbId = 7;
                UserConfig.User.Name = "tester";
                UserConfig.User.Dn = "uid=tester,ou=people,dc=example,dc=com";
                UserConfig.User.Roles = userRoles;
                context.Services.AddSingleton<UserConfig>(UserConfig);
                context.Services.AddSingleton<ApiConnection>(ApiConnection);
                MockMiddlewareClient middlewareClient = new();
                context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
                context.Services.AddSingleton<ISessionStorage>(sessionStorage);
                context.Services.AddSingleton(new TokenService(middlewareClient, sessionStorage));
                context.Services.AddSingleton<IEventMediator>(EventMediator);
                context.Services.AddSingleton<ITokenRefreshCoordinator>(TokenRefreshCoordinator);
                context.Services.AddSingleton(new KeyboardInputService());
                context.Services.AddSingleton((ProtectedSessionStorage)RuntimeHelpers.GetUninitializedObject(typeof(ProtectedSessionStorage)));
                context.Services.AddSingleton<AuthenticationStateProvider>(new MainLayoutAuthStateProvider(UserConfig.User.Name, UserConfig.User.Dn, userRoles));

                Layout = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<MainLayout>())
                    .FindComponent<MainLayout>();
            }

            public async ValueTask DisposeAsync()
            {
                await context.DisposeAsync();
            }
        }

        private sealed class MainLayoutAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public MainLayoutAuthStateProvider(string username, string userDn, IEnumerable<string> roles)
            {
                List<Claim> claims =
                [
                    new(ClaimTypes.Name, username),
                    new("x-hasura-uuid", userDn)
                ];
                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
                principal = new(new ClaimsIdentity(claims, "Test"));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class MainLayoutTokenRefreshCoordinatorStub : ITokenRefreshCoordinator
        {
            public int StartCallCount { get; private set; }
            public int StopCallCount { get; private set; }

            public Task StartAsync()
            {
                StartCallCount++;
                return Task.CompletedTask;
            }

            public void Stop()
            {
                StopCallCount++;
            }

            public void Dispose()
            {
            }
        }
    }

    internal sealed record UiLogEntry(int Severity, string Cause, string Description);

    internal sealed class MainLayoutTestApiConnection : SimulatedApiConnection
    {
        public List<UiLogEntry> UiLogs { get; } = [];
        public List<(string Title, string Message)> Alerts { get; } = [];
        public bool ThrowOnUiLog { get; set; }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            return null!;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == MonitorQueries.addUiLogEntry)
            {
                if (ThrowOnUiLog)
                {
                    throw new InvalidOperationException("UI log failed");
                }

                UiLogs.Add(new UiLogEntry(
                    Convert.ToInt32(GetVariableValue(variables, "severity")),
                    Convert.ToString(GetVariableValue(variables, "suspectedCause")) ?? "",
                    Convert.ToString(GetVariableValue(variables, "description")) ?? ""));
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 1 }] });
            }

            if (typeof(QueryResponseType) == typeof(List<Alert>) && query == MonitorQueries.getOpenAlerts)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Alert>());
            }

            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == MonitorQueries.addAlert)
            {
                object? title = GetVariableValue(variables, "title");
                object? message = GetVariableValue(variables, "description");
                Alerts.Add((Convert.ToString(title) ?? "", Convert.ToString(message) ?? ""));
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 99 }] });
            }

            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                object? title = GetVariableValue(variables, "title");
                object? message = GetVariableValue(variables, "description");
                Alerts.Add((Convert.ToString(title) ?? "", Convert.ToString(message) ?? ""));
                return Task.FromResult((QueryResponseType)(object)new ReturnId { NewIdLong = 1 });
            }

            throw new NotImplementedException($"Unhandled query response type {typeof(QueryResponseType).Name}");
        }

        public void WaitForLogCount(int expectedCount)
        {
            Assert.That(() => UiLogs.Count, Is.EqualTo(expectedCount).After(2000, 25));
        }

        private static object? GetVariableValue(object? variables, string propertyName)
        {
            return variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
        }
    }
}

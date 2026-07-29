using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitoringMainTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(MonitoringMain).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MonitoringMain).FullName, name);
        }

        private static T GetPrivateField<T>(MonitoringMain component, string fieldName)
        {
            FieldInfo? field = typeof(MonitoringMain).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(component)!;
            }

            PropertyInfo? property = typeof(MonitoringMain).GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingFieldException(typeof(MonitoringMain).FullName, fieldName);
            }
            return (T)property.GetValue(component)!;
        }

        private static void SetPrivateField<T>(MonitoringMain component, string fieldName, T value)
        {
            FieldInfo? field = typeof(MonitoringMain).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitoringMain).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static void SetPrivateProperty<T>(MonitoringMain component, string propertyName, T value)
        {
            PropertyInfo? property = typeof(MonitoringMain).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(MonitoringMain).FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static object? InvokePrivateMethod(MonitoringMain component, string name, params object[] args)
        {
            return GetPrivateMethod(name).Invoke(component, args);
        }

        private static Task InvokePrivateTask(MonitoringMain component, string name, params object[] args)
        {
            return (Task)InvokePrivateMethod(component, name, args)!;
        }

        private static void InvokePrivateVoid(MonitoringMain component, string name, params object[] args)
        {
            InvokePrivateMethod(component, name, args);
        }

        private static MonitoringMainTestSetup RenderComponent(params string[] roles)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(roles));
            MonitoringMainTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            SimulatedUserConfig userConfig = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitoringMain>());

            return new MonitoringMainTestSetup(context, component.FindComponent<MonitoringMain>().Instance, apiConn, userConfig);
        }

        [Test]
        public async Task Acknowledge_RemovesAlertAndCallsApi()
        {
            await using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;

            Alert alert = new() { Id = 123, Source = "test" };
            List<Alert> alerts = new() { alert };
            SetPrivateField(monitoring, "alertEntrys", alerts);

            await InvokePrivateTask(monitoring, "Acknowledge", alert);

            Assert.That(alerts, Is.Empty);
            Assert.That(setup.ApiConn.AcknowledgedAlertIds, Is.EqualTo(new List<long> { 123L }));
        }

        [Test]
        public async Task AcknowledgeAllOpen_ClearsAlertsWhenSuccessful()
        {
            await using MonitoringMainTestSetup setup = RenderComponent();
            setup.ApiConn.AckAllAffectedRows = 2;
            MonitoringMain monitoring = setup.Component;

            List<Alert> alerts = new()
            {
                new Alert { Id = 1, Source = "test" },
                new Alert { Id = 2, Source = "test" }
            };
            SetPrivateField(monitoring, "alertEntrys", alerts);

            await InvokePrivateTask(monitoring, "AcknowledgeAllOpen");

            Assert.That(alerts, Is.Empty);
            Assert.That(setup.ApiConn.AcknowledgeAllCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task AcknowledgeAllOpen_NegativeResultRefreshesAlerts()
        {
            await using MonitoringMainTestSetup setup = RenderComponent();
            setup.ApiConn.AckAllAffectedRows = -1;
            MonitoringMain monitoring = setup.Component;

            List<Alert> alerts = new() { new Alert { Id = 1, Source = "test" } };
            SetPrivateField(monitoring, "alertEntrys", alerts);

            await InvokePrivateTask(monitoring, "AcknowledgeAllOpen");

            Assert.That(setup.ApiConn.AcknowledgeAllCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.OpenAlertQueryCalls, Is.EqualTo(1));
            Assert.That(GetPrivateField<List<Alert>>(monitoring, "alertEntrys"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<List<Alert>>(monitoring, "alertEntrys")[0].Id, Is.EqualTo(1));
        }

        [Test]
        public async Task OnInitializedAsync_PrivilegedUser_LoadsManagementsAndAlerts()
        {
            await using MonitoringMainTestSetup setup = RenderComponent(Roles.Admin);
            setup.UserConfig.User.Roles.Add(Roles.Admin);

            await InvokePrivateTask(setup.Component, "OnInitializedAsync");

            Assert.That(setup.ApiConn.ManagementQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.OpenAlertQueryCalls, Is.EqualTo(1));
            Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            Assert.That(GetPrivateField<List<Management>>(setup.Component, "managements"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<List<Alert>>(setup.Component, "alertEntrys"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task OnInitializedAsync_NonPrivilegedUser_SkipsLoading()
        {
            await using MonitoringMainTestSetup setup = RenderComponent();

            await InvokePrivateTask(setup.Component, "OnInitializedAsync");

            Assert.That(setup.ApiConn.ManagementQueryCalls, Is.EqualTo(0));
            Assert.That(setup.ApiConn.OpenAlertQueryCalls, Is.EqualTo(0));
            Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            Assert.That(GetPrivateField<List<Management>>(setup.Component, "managements"), Is.Empty);
            Assert.That(GetPrivateField<List<Alert>>(setup.Component, "alertEntrys"), Is.Empty);
        }

        [Test]
        public void ShowAutodiscDetails_StoresSingleActionAndOpensDialog()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            Alert alert = new() { Id = 7, Title = "title", Description = "desc", JsonData = "payload" };

            InvokePrivateVoid(monitoring, "ShowAutodiscDetails", alert);

            List<ActionItem> actions = GetPrivateField<List<ActionItem>>(monitoring, "actActions");
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].AlertId, Is.EqualTo(alert.Id));
            Assert.That(actions[0].Supermanager, Is.EqualTo(alert.Title));
            Assert.That(GetPrivateField<bool>(monitoring, "AutoDiscoverMode"), Is.True);
        }

        [Test]
        public void ShowImportDetails_ValidJson_EnablesDetailsMode()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            ImportStatus importStatus = new()
            {
                MgmId = 44,
                MgmName = "mgm",
                LastImport = new List<ImportControl>
                {
                    new ImportControl { ControlId = 11, SuccessfulImport = true }
                }.ToArray()
            };
            Alert alert = new() { JsonData = JsonSerializer.Serialize(importStatus) };

            InvokePrivateVoid(monitoring, "ShowImportDetails", alert);

            ImportStatus storedStatus = GetPrivateField<ImportStatus>(monitoring, "actStatus");
            Assert.That(storedStatus.MgmId, Is.EqualTo(44));
            Assert.That(storedStatus.LastImport, Is.Not.Null);
            Assert.That(storedStatus.LastImport![0].SuccessfulImport, Is.True);
            Assert.That(GetPrivateField<bool>(monitoring, "DetailsMode"), Is.True);
        }

        [Test]
        public void ShowImportDetails_InvalidJson_ReportsError()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            List<(Exception? Exception, string Title, string Message, bool ErrorFlag)> messages = new();
            SetPrivateProperty(monitoring, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, title, message, errorFlag) =>
                messages.Add((exception, title, message, errorFlag))));

            Alert alert = new() { JsonData = "not-json" };

            InvokePrivateVoid(monitoring, "ShowImportDetails", alert);

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("handle_alert"));
            Assert.That(messages[0].ErrorFlag, Is.True);
        }

        [Test]
        public void ShowImportRollback_ValidJson_EnablesRollbackMode()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            ImportControl[] controls =
            {
                new ImportControl
                {
                    ControlId = 9,
                    StartTime = DateTime.UtcNow
                }
            };
            Alert alert = new()
            {
                ManagementId = 55,
                JsonData = JsonSerializer.Serialize(controls)
            };

            InvokePrivateVoid(monitoring, "ShowImportRollback", alert);

            ImportControl[]? storedControls = GetPrivateField<ImportControl[]?>(monitoring, "LastIncompleteImport");
            Assert.That(GetPrivateField<int>(monitoring, "actMgmtId"), Is.EqualTo(55));
            Assert.That(storedControls, Is.Not.Null);
            Assert.That(storedControls![0].ControlId, Is.EqualTo(9));
            Assert.That(GetPrivateField<bool>(monitoring, "RollbackMode"), Is.True);
        }

        [Test]
        public void ShowImportRollback_MissingManagementId_ReportsError()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            List<(Exception? Exception, string Title, string Message, bool ErrorFlag)> messages = new();
            SetPrivateProperty(monitoring, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, title, message, errorFlag) =>
                messages.Add((exception, title, message, errorFlag))));

            Alert alert = new() { JsonData = JsonSerializer.Serialize(new ImportControl[0]) };

            InvokePrivateVoid(monitoring, "ShowImportRollback", alert);

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("handle_alert"));
            Assert.That(messages[0].ErrorFlag, Is.True);
        }

        [Test]
        public void ShowRemoveSampleData_StoresAlertAndOpensConfirmation()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            Alert alert = new() { Id = 88 };

            InvokePrivateVoid(monitoring, "ShowRemoveSampleData", alert);

            Assert.That(GetPrivateField<bool>(monitoring, "RemoveSampleDataMode"), Is.True);
            Assert.That(GetPrivateField<Alert>(monitoring, "actAlert").Id, Is.EqualTo(88));
        }

        [Test]
        public void ConstructMessage_CombinesNonZeroCounters()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            SimulatedUserConfig userConfig = setup.UserConfig;

            SetPrivateField(monitoring, "deletedSampleManagements", 1);
            SetPrivateField(monitoring, "deletedSampleUsers", 2);
            SetPrivateField(monitoring, "deletedSampleOwners", 3);

            string message = (string)InvokePrivateMethod(monitoring, "ConstructMessage")!;

            Assert.That(message, Is.EqualTo($"1 {userConfig.GetText("managements")} 2 {userConfig.GetText("users")} 3 {userConfig.GetText("owners")} {userConfig.GetText("deleted")}"));
        }

        [Test]
        public void Cancel_ClosesAllModals()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            SetPrivateField(monitoring, "RemoveSampleDataMode", true);
            SetPrivateField(monitoring, "AckAllMode", true);
            SetPrivateField(monitoring, "DetailsMode", true);
            SetPrivateField(monitoring, "RollbackMode", true);

            InvokePrivateVoid(monitoring, "Cancel");

            Assert.That(GetPrivateField<bool>(monitoring, "RemoveSampleDataMode"), Is.False);
            Assert.That(GetPrivateField<bool>(monitoring, "AckAllMode"), Is.False);
            Assert.That(GetPrivateField<bool>(monitoring, "DetailsMode"), Is.False);
            Assert.That(GetPrivateField<bool>(monitoring, "RollbackMode"), Is.False);
        }

    }

    internal sealed class MonitoringMainTestSetup : IDisposable, IAsyncDisposable
    {
        public MonitoringMainTestSetup(BunitContext context, MonitoringMain component, MonitoringMainTestApiConn apiConn, SimulatedUserConfig userConfig)
        {
            Context = context;
            Component = component;
            ApiConn = apiConn;
            UserConfig = userConfig;
        }

        public BunitContext Context { get; }
        public MonitoringMain Component { get; }
        public MonitoringMainTestApiConn ApiConn { get; }
        public SimulatedUserConfig UserConfig { get; }

        public void Dispose()
        {
            Context.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
        }
    }

    internal sealed class MonitoringMainTestApiConn : SimulatedApiConnection
    {
        public List<long> AcknowledgedAlertIds { get; } = new();
        public int AcknowledgeAllCalls { get; private set; }
        public int AckAllAffectedRows { get; set; } = 1;
        public int ManagementQueryCalls { get; private set; }
        public int OpenAlertQueryCalls { get; private set; }
        private static readonly List<Management> kManagements = new() { new Management { Id = 1, Name = "mgm-demo" } };
        private static readonly List<Alert> kAlerts = new() { new Alert { Id = 1, Source = "monitoring", Title = "alert" } };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
            {
                ManagementQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<Management>(kManagements));
            }

            if (typeof(QueryResponseType) == typeof(List<Alert>) && query == MonitorQueries.getOpenAlerts)
            {
                OpenAlertQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<Alert>(kAlerts));
            }

            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                if (query == MonitorQueries.acknowledgeAlert)
                {
                    if (variables != null)
                    {
                        object? idValue = variables.GetType().GetProperty("id")?.GetValue(variables);
                        if (idValue != null)
                        {
                            AcknowledgedAlertIds.Add(Convert.ToInt64(idValue));
                        }
                    }
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (query == MonitorQueries.acknowledgeAllOpenAlerts)
                {
                    AcknowledgeAllCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = AckAllAffectedRows });
                }
            }

            throw new NotImplementedException();
        }
    }

    internal sealed class MonitoringTestAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal principal;

        public MonitoringTestAuthStateProvider(params string[] roles)
        {
            List<Claim> claims = new();
            foreach (string role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            ClaimsIdentity identity = new(claims, "Test");
            principal = new ClaimsPrincipal(identity);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(principal));
        }
    }
}

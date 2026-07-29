using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;

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
            return RenderComponent(null, roles);
        }

        private static MonitoringMainTestSetup RenderComponent(MiddlewareClient? middlewareClient, params string[] roles)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(roles));
            MonitoringMainTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(middlewareClient ?? new MiddlewareClient("http://localhost/"));
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
        public void ShowImportRollback_InvalidJson_ReportsError()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            List<(Exception? Exception, string Title, string Message, bool ErrorFlag)> messages = new();
            SetPrivateProperty(monitoring, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, title, message, errorFlag) =>
                messages.Add((exception, title, message, errorFlag))));

            Alert alert = new() { JsonData = "not-json", ManagementId = 12 };

            InvokePrivateVoid(monitoring, "ShowImportRollback", alert);

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("handle_alert"));
            Assert.That(messages[0].ErrorFlag, Is.True);
            Assert.That(GetPrivateField<bool>(monitoring, "RollbackMode"), Is.False);
        }

        [Test]
        public void ShowImportRollback_ValidJson_EnablesRollbackMode()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            List<ImportControl> controls = new List<ImportControl>
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
                JsonData = JsonSerializer.Serialize(controls.ToArray())
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
        public void RequestAcknowledgeAllOpen_SetsMode()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;

            InvokePrivateVoid(monitoring, "RequestAcknowledgeAllOpen");

            Assert.That(GetPrivateField<bool>(monitoring, "AckAllMode"), Is.True);
        }

        [Test]
        public void UpdatePageSize_StoresValue()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;

            InvokePrivateVoid(monitoring, "UpdatePageSize", 37);

            Assert.That(GetPrivateField<int>(monitoring, "PageSize"), Is.EqualTo(37));
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
        public void ConstructMessage_WithNoCounters_ReturnsNothingDeleted()
        {
            using MonitoringMainTestSetup setup = RenderComponent();
            MonitoringMain monitoring = setup.Component;
            SimulatedUserConfig userConfig = setup.UserConfig;

            string message = (string)InvokePrivateMethod(monitoring, "ConstructMessage")!;

            Assert.That(message, Is.EqualTo($"{userConfig.GetText("nothing")} {userConfig.GetText("deleted")}"));
        }

        [Test]
        public async Task RemoveSampleData_DeletesDemoEntriesAndBuildsSummary()
        {
            TestMiddlewareClient middlewareClient = new("http://localhost/");
            MonitoringMainCleanupHandler handler = new()
            {
                InternalGroups = new List<GroupGetReturnParameters>
                {
                    new GroupGetReturnParameters { GroupDn = "cn=group_demo,ou=groups,dc=example,dc=com" },
                    new GroupGetReturnParameters { GroupDn = "cn=group_regular,ou=groups,dc=example,dc=com" }
                }
            };
            middlewareClient.UseHandler(handler);

            await using MonitoringMainTestSetup setup = RenderComponent(middlewareClient);
            setup.UserConfig.User.Roles.Add(Roles.Admin);
            MonitoringMain monitoring = setup.Component;
            List<(Exception? Exception, string Title, string Message, bool ErrorFlag)> messages = new();
            SetPrivateProperty(monitoring, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, title, message, errorFlag) =>
                messages.Add((exception, title, message, errorFlag))));

            Alert alert = new() { Id = 99 };
            SetPrivateField(monitoring, "actAlert", alert);
            SetPrivateField(monitoring, "alertEntrys", new List<Alert> { alert });
            setup.ApiConn.Managements.Clear();
            setup.ApiConn.Managements.Add(new Management { Id = 1, Name = "mgm_demo" });
            setup.ApiConn.Managements.Add(new Management { Id = 2, Name = "mgm_regular" });
            setup.ApiConn.Credentials.Clear();
            setup.ApiConn.Credentials.Add(new ImportCredential { Id = 3, Name = "cred_demo" });
            setup.ApiConn.Credentials.Add(new ImportCredential { Id = 4, Name = "cred_regular" });
            setup.ApiConn.Users.Clear();
            setup.ApiConn.Users.Add(new UiUser { DbId = 5, Name = "user_demo", LdapConnection = new UiLdapConnection { Id = 15 } });
            setup.ApiConn.Users.Add(new UiUser { DbId = 6, Name = "user_regular", LdapConnection = new UiLdapConnection { Id = 16 } });
            setup.ApiConn.Tenants.Clear();
            setup.ApiConn.Tenants.Add(new Tenant { Id = 7, Name = "tenant_demo" });
            setup.ApiConn.Tenants.Add(new Tenant { Id = 8, Name = "tenant_regular" });
            setup.ApiConn.Owners.Clear();
            setup.ApiConn.Owners.Add(new FwoOwner { Id = 9, Name = "owner_demo" });
            setup.ApiConn.Owners.Add(new FwoOwner { Id = 10, Name = "owner_regular" });

            await InvokePrivateTask(monitoring, "RemoveSampleData");

            Assert.That(setup.ApiConn.ManagementQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.CredentialQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.UserQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.TenantQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.OwnerQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.ManagementDeleteQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.CredentialDeleteQueryCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.OwnerDeleteQueryCalls, Is.EqualTo(1));
            Assert.That(handler.DeleteUserCalls, Is.EqualTo(1));
            Assert.That(handler.DeleteTenantCalls, Is.EqualTo(1));
            Assert.That(handler.DeleteGroupCalls, Is.EqualTo(1));
            Assert.That(setup.ApiConn.AcknowledgedAlertIds, Is.EqualTo(new List<long> { 99L }));
            Assert.That(GetPrivateField<List<Alert>>(monitoring, "alertEntrys"), Is.Empty);
            Assert.That(GetPrivateField<bool>(monitoring, "RemoveSampleDataMode"), Is.False);
            Assert.That(GetPrivateField<bool>(monitoring, "workInProgress"), Is.False);
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo(setup.UserConfig.GetText("remove_sample_data")));
            Assert.That(messages[0].ErrorFlag, Is.False);
            Assert.That(messages[0].Message, Does.Contain(setup.UserConfig.GetText("deleted")));
            Assert.That(messages[0].Message, Does.Contain(setup.UserConfig.GetText("managements")));
            Assert.That(messages[0].Message, Does.Contain(setup.UserConfig.GetText("users")));
            Assert.That(messages[0].Message, Does.Contain(setup.UserConfig.GetText("tenants")));
            Assert.That(messages[0].Message, Does.Contain(setup.UserConfig.GetText("groups")));
            Assert.That(messages[0].Message, Does.Contain(setup.UserConfig.GetText("owners")));
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
        public int CredentialQueryCalls { get; private set; }
        public int UserQueryCalls { get; private set; }
        public int TenantQueryCalls { get; private set; }
        public int OwnerQueryCalls { get; private set; }
        public int ManagementDeleteQueryCalls { get; private set; }
        public int CredentialDeleteQueryCalls { get; private set; }
        public int UserDeleteQueryCalls { get; private set; }
        public int TenantDeleteQueryCalls { get; private set; }
        public int OwnerDeleteQueryCalls { get; private set; }
        public List<Management> Managements { get; } = new List<Management> { new Management { Id = 1, Name = "mgm_demo" } };
        public List<ImportCredential> Credentials { get; } = new List<ImportCredential> { new ImportCredential { Id = 2, Name = "cred_demo" } };
        public List<UiUser> Users { get; } = new List<UiUser> { new UiUser { DbId = 3, Name = "user_demo", LdapConnection = new UiLdapConnection { Id = 13 } } };
        public List<Tenant> Tenants { get; } = new List<Tenant> { new Tenant { Id = 4, Name = "tenant_demo" } };
        public List<FwoOwner> Owners { get; } = new List<FwoOwner> { new FwoOwner { Id = 5, Name = "owner_demo" } };
        public List<Alert> OpenAlerts { get; } = new List<Alert> { new Alert { Id = 1, Source = "monitoring", Title = "alert" } };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
            {
                ManagementQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<Management>(Managements));
            }

            if (typeof(QueryResponseType) == typeof(List<Alert>) && query == MonitorQueries.getOpenAlerts)
            {
                OpenAlertQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<Alert>(OpenAlerts));
            }

            if (typeof(QueryResponseType) == typeof(List<ImportCredential>) && query == DeviceQueries.getCredentials)
            {
                CredentialQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<ImportCredential>(Credentials));
            }

            if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUsers)
            {
                UserQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<UiUser>(Users));
            }

            if (typeof(QueryResponseType) == typeof(List<Tenant>) && query == AuthQueries.getTenants)
            {
                TenantQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<Tenant>(Tenants));
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                OwnerQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>(Owners));
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

                if (query == DeviceQueries.deleteManagement)
                {
                    ManagementDeleteQueryCalls++;
                    int id = Convert.ToInt32(variables?.GetType().GetProperty("id")?.GetValue(variables) ?? 0);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { DeletedId = id });
                }

                if (query == DeviceQueries.deleteCredential)
                {
                    CredentialDeleteQueryCalls++;
                    int id = Convert.ToInt32(variables?.GetType().GetProperty("id")?.GetValue(variables) ?? 0);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { DeletedId = id });
                }

                if (query == OwnerQueries.deleteOwner)
                {
                    OwnerDeleteQueryCalls++;
                    int id = Convert.ToInt32(variables?.GetType().GetProperty("id")?.GetValue(variables) ?? 0);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { DeletedId = id });
                }
            }

            throw new NotImplementedException();
        }
    }

    internal sealed class MonitoringMainCleanupHandler : HttpMessageHandler
    {
        public List<GroupGetReturnParameters> InternalGroups { get; set; } = new List<GroupGetReturnParameters>();
        public int DeleteUserCalls { get; private set; }
        public int DeleteTenantCalls { get; private set; }
        public int DeleteGroupCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != null && request.RequestUri.AbsolutePath.EndsWith("/Group", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Method == HttpMethod.Get)
                {
                    return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(InternalGroups)));
                }

                if (request.Method == HttpMethod.Delete)
                {
                    DeleteGroupCalls++;
                    return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, "true"));
                }
            }

            if (request.RequestUri != null && request.RequestUri.AbsolutePath.EndsWith("/User", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Delete)
            {
                DeleteUserCalls++;
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, "true"));
            }

            if (request.RequestUri != null && request.RequestUri.AbsolutePath.EndsWith("/Tenant", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Delete)
            {
                DeleteTenantCalls++;
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, "true"));
            }

            return Task.FromResult(CreateJsonResponse(HttpStatusCode.NotFound, "{}"));
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                ReasonPhrase = statusCode == HttpStatusCode.OK ? "OK" : "Not Found"
            };
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

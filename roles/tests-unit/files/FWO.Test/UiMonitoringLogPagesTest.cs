using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitoringLogPagesTest
    {
        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static MonitorTestApiConn RenderApiConn()
        {
            return new MonitorTestApiConn();
        }

        private static TestSetup<TComponent> RenderComponent<TComponent>(MonitorTestApiConn apiConn)
            where TComponent : class, IComponent
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<TComponent>());
            return new TestSetup<TComponent>(context, component.FindComponent<TComponent>().Instance);
        }

        [Test]
        public void MonitorAll_LoadsLogEntries()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.LogEntries.Add(CreateLogEntry(2, "monitor"));
            apiConn.LogEntries.Add(CreateLogEntry(1, "monitor"));

            using TestSetup<MonitorAll> setup = RenderComponent<MonitorAll>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.LogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getLogEntrys));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(2));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public void MonitorAlerts_LoadsAlertsUsersAndManagements()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.Alerts.Add(CreateAlert(1));
            apiConn.UiUsers.Add(CreateUiUser(42, "user"));
            apiConn.Managements.Add(CreateManagement(7, "mgm"));

            using TestSetup<MonitorAlerts> setup = RenderComponent<MonitorAlerts>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.AlertQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.UserQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.ManagementQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQueries, Does.Contain(MonitorQueries.getAlerts));
                Assert.That(apiConn.LastQueries, Does.Contain(AuthQueries.getUsers));
                Assert.That(apiConn.LastQueries, Does.Contain(DeviceQueries.getManagementNames));
                Assert.That(GetPrivateField<List<Alert>>(setup.Component, "alertEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<UiUser>>(setup.Component, "uiUsers"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<Management>>(setup.Component, "managements"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public void MonitorImportLog_LoadsImportLogEntries()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.LogEntries.Add(CreateLogEntry(11, "import"));

            using TestSetup<MonitorImportLog> setup = RenderComponent<MonitorImportLog>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.LogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getImportLogEntrys));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public void MonitorAppDataImportLog_UsesAppDataSource()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.LogEntries.Add(CreateLogEntry(21, GlobalConst.kImportAppData));

            using TestSetup<MonitorAppDataImportLog> setup = RenderComponent<MonitorAppDataImportLog>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.LogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getDataImportLogEntrys));
                Assert.That(apiConn.LastSource, Is.EqualTo(GlobalConst.kImportAppData));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public void MonitorAreaIpDataImportLog_UsesAreaSubnetSource()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.LogEntries.Add(CreateLogEntry(31, GlobalConst.kImportAreaSubnetData));

            using TestSetup<MonitorAreaIpDataImportLog> setup = RenderComponent<MonitorAreaIpDataImportLog>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.LogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getDataImportLogEntrys));
                Assert.That(apiConn.LastSource, Is.EqualTo(GlobalConst.kImportAreaSubnetData));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        private static LogEntry CreateLogEntry(long id, string source)
        {
            return new LogEntry
            {
                Id = id,
                Source = source,
                Severity = 1,
                Timestamp = new DateTime(2026, 1, 1, 12, 0, 0),
                Description = "desc"
            };
        }

        private static Alert CreateAlert(long id)
        {
            return new Alert
            {
                Id = id,
                Source = "source",
                Title = "title",
                Description = "description",
                Timestamp = new DateTime(2026, 1, 1, 12, 0, 0)
            };
        }

        private static UiUser CreateUiUser(int id, string name)
        {
            return new UiUser
            {
                DbId = id,
                Name = name
            };
        }

        private static Management CreateManagement(int id, string name)
        {
            return new Management
            {
                Id = id,
                Name = name
            };
        }
    }

    internal sealed class MonitorTestApiConn : SimulatedApiConnection
    {
        public List<LogEntry> LogEntries { get; } = new();
        public List<Alert> Alerts { get; } = new();
        public List<UiUser> UiUsers { get; } = new();
        public List<Management> Managements { get; } = new();
        public List<string> LastQueries { get; } = new();

        public int LogQueryCount { get; private set; }
        public int AlertQueryCount { get; private set; }
        public int UserQueryCount { get; private set; }
        public int ManagementQueryCount { get; private set; }
        public string? LastQuery { get; private set; }
        public string? LastSource { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            LastQueries.Add(query);
            LastQuery = query;

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getLogEntrys)
            {
                LogQueryCount++;
                return Task.FromResult((QueryResponseType)(object)LogEntries);
            }

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getImportLogEntrys)
            {
                LogQueryCount++;
                return Task.FromResult((QueryResponseType)(object)LogEntries);
            }

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getDataImportLogEntrys)
            {
                LogQueryCount++;
                LastSource = GetVariable<string>(variables, "source");
                return Task.FromResult((QueryResponseType)(object)LogEntries);
            }

            if (typeof(QueryResponseType) == typeof(List<Alert>) && query == MonitorQueries.getAlerts)
            {
                AlertQueryCount++;
                return Task.FromResult((QueryResponseType)(object)Alerts);
            }

            if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUsers)
            {
                UserQueryCount++;
                return Task.FromResult((QueryResponseType)(object)UiUsers);
            }

            if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
            {
                ManagementQueryCount++;
                return Task.FromResult((QueryResponseType)(object)Managements);
            }

            throw new NotImplementedException();
        }

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(variables?.GetType().FullName, propertyName);
            }

            return (TValue)property.GetValue(variables)!;
        }
    }

    internal sealed record TestSetup<TComponent>(BunitContext Context, TComponent Component) : IDisposable
        where TComponent : class, IComponent
    {
        public void Dispose()
        {
            Context.Dispose();
        }
    }
}

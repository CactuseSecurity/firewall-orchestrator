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
using System.Linq;
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

        private static void SetPrivateField(object component, string fieldName, object? value)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateProperty<T>(object component, string propertyName)
        {
            PropertyInfo? property = component.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(component.GetType().FullName, propertyName);
            }
            return (T)property.GetValue(component)!;
        }

        private static void SetPrivateProperty(object component, string propertyName, object? value)
        {
            PropertyInfo? property = component.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(component.GetType().FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static async Task InvokePrivateAsync(object component, string methodName, params object?[] parameters)
        {
            MethodInfo? method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }
            await (Task)method.Invoke(component, parameters)!;
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
            IRenderedComponent<TComponent> renderedComponent = component.FindComponent<TComponent>();
            return new TestSetup<TComponent>(context, renderedComponent.Instance, renderedComponent);
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
        public void MonitorEmailLog_LoadsNotificationLogEntries()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.NotificationLogEntries.Add(CreateNotificationLogEntry("InterfaceRequest", "to@example.test"));

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.NotificationLogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getNotificationLogEntrys));
                Assert.That(apiConn.NotificationLogLimit, Is.EqualTo(21));
                Assert.That(apiConn.NotificationLogOffset, Is.Zero);
                Assert.That(GetPrivateField<List<NotificationLogEntry>>(setup.Component, "notificationLogEntries"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
            Assert.That(setup.Rendered.FindAll("#notification-log-type option").Select(option => option.GetAttribute("value")),
                Does.Contain("InterfaceRequest"));
            Assert.That(setup.Rendered.FindAll("#notification-log-type option").Select(option => option.GetAttribute("value")),
                Does.Not.Contain("None"));
        }

        [Test]
        public void MonitorEmailLog_AppliesNotificationLogFilters()
        {
            MonitorTestApiConn apiConn = RenderApiConn();

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);

            SetPrivateField(setup.Component, "notificationIdFilter", 42);
            SetPrivateField(setup.Component, "notificationTypeFilter", "InterfaceRequest");
            SetPrivateField(setup.Component, "statusFilter", "Failed");
            SetPrivateField(setup.Component, "errorFilter", "timeout");
            MethodInfo buildWhereFilter = setup.Component.GetType().GetMethod("BuildWhereFilter", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Dictionary<string, object> where = (Dictionary<string, object>)buildWhereFilter.Invoke(setup.Component, null)!;
            Assert.Multiple(() =>
            {
                Assert.That(GetFilterValue(where, "notification_id"), Is.EqualTo(42));
                Assert.That(GetFilterValue(where, "notification_type"), Is.EqualTo("InterfaceRequest"));
                Assert.That(GetFilterValue(where, "status"), Is.EqualTo("Failed"));
                Assert.That(GetFilterValue(where, "error"), Is.EqualTo("%timeout%"));
            });
        }

        [Test]
        public void MonitorEmailLog_EscapesErrorLikeWildcards()
        {
            MonitorTestApiConn apiConn = RenderApiConn();

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);

            SetPrivateField(setup.Component, "errorFilter", "folder_100%\\done");
            MethodInfo buildWhereFilter = setup.Component.GetType().GetMethod("BuildWhereFilter", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Dictionary<string, object> where = (Dictionary<string, object>)buildWhereFilter.Invoke(setup.Component, null)!;

            Assert.That(GetFilterValue(where, "error"), Is.EqualTo("%folder\\_100\\%\\\\done%"));
        }

        [Test]
        public async Task MonitorEmailLog_PaginatesAndUpdatesPageSize()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            AddNotificationLogEntries(apiConn, 21);

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);

            await InvokePrivateAsync(setup.Component, "NextPage");
            Assert.That(GetPrivateProperty<int>(setup.Component, "CurrentPage"), Is.EqualTo(1));
            Assert.That(apiConn.NotificationLogOffset, Is.EqualTo(20));

            await InvokePrivateAsync(setup.Component, "PreviousPage");
            Assert.That(GetPrivateProperty<int>(setup.Component, "CurrentPage"), Is.Zero);
            Assert.That(apiConn.NotificationLogOffset, Is.Zero);

            await InvokePrivateAsync(setup.Component, "UpdatePageSize", 10);
            Assert.That(GetPrivateProperty<int>(setup.Component, "PageSize"), Is.EqualTo(10));
            Assert.That(GetPrivateField<List<NotificationLogEntry>>(setup.Component, "notificationLogEntries"), Has.Count.EqualTo(10));
            int queryCountAfterPageSizeUpdate = apiConn.NotificationLogQueryCount;

            await InvokePrivateAsync(setup.Component, "UpdatePageSize", 10);
            Assert.That(apiConn.NotificationLogQueryCount, Is.EqualTo(queryCountAfterPageSizeUpdate));

            await InvokePrivateAsync(setup.Component, "UpdatePageSize", 0);
            Assert.That(GetPrivateProperty<int>(setup.Component, "PageSize"), Is.EqualTo(20));
        }

        [Test]
        public async Task MonitorEmailLog_ApplyAndClearFiltersResetPage()
        {
            MonitorTestApiConn apiConn = RenderApiConn();

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);

            SetPrivateField(setup.Component, "notificationIdFilter", 42);
            SetPrivateField(setup.Component, "notificationTypeFilter", "InterfaceRequest");
            SetPrivateField(setup.Component, "statusFilter", "Failed");
            SetPrivateField(setup.Component, "errorFilter", "timeout");
            SetPrivateProperty(setup.Component, "CurrentPage", 2);

            await InvokePrivateAsync(setup.Component, "ApplyFilters");
            Assert.That(GetPrivateProperty<int>(setup.Component, "CurrentPage"), Is.Zero);
            Assert.That(apiConn.NotificationLogQueryCount, Is.EqualTo(2));

            await InvokePrivateAsync(setup.Component, "ClearFilters");
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<int?>(setup.Component, "notificationIdFilter"), Is.Null);
                Assert.That(GetPrivateField<string>(setup.Component, "notificationTypeFilter"), Is.Empty);
                Assert.That(GetPrivateField<string>(setup.Component, "statusFilter"), Is.Empty);
                Assert.That(GetPrivateField<string>(setup.Component, "errorFilter"), Is.Empty);
                Assert.That(GetPrivateProperty<int>(setup.Component, "CurrentPage"), Is.Zero);
                Assert.That(apiConn.NotificationLogQueryCount, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task MonitorEmailLog_DoesNothingAtPaginationBoundaries()
        {
            MonitorTestApiConn apiConn = RenderApiConn();

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);
            int initialQueryCount = apiConn.NotificationLogQueryCount;

            await InvokePrivateAsync(setup.Component, "PreviousPage");
            await InvokePrivateAsync(setup.Component, "NextPage");

            Assert.That(apiConn.NotificationLogQueryCount, Is.EqualTo(initialQueryCount));
        }

        [Test]
        public void MonitorEmailLog_HandlesNotificationLogQueryFailure()
        {
            MonitorTestApiConn apiConn = RenderApiConn();
            apiConn.ThrowNotificationLogQuery = true;

            using TestSetup<MonitorEmailLog> setup = RenderComponent<MonitorEmailLog>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.False);
                Assert.That(apiConn.NotificationLogQueryCount, Is.EqualTo(1));
            });
        }

        private static object GetFilterValue(Dictionary<string, object> where, string fieldName)
        {
            Dictionary<string, object> filter = (Dictionary<string, object>)where[fieldName];
            return filter.Values.Single();
        }

        private static void AddNotificationLogEntries(MonitorTestApiConn apiConn, int count)
        {
            for (int index = 0; index < count; index++)
            {
                apiConn.NotificationLogEntries.Add(CreateNotificationLogEntry("InterfaceRequest", $"to{index}@example.test"));
            }
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

        private static NotificationLogEntry CreateNotificationLogEntry(string notificationType, string to)
        {
            return new NotificationLogEntry
            {
                Timestamp = new DateTimeOffset(new DateTime(2026, 1, 1, 12, 0, 0), TimeSpan.Zero),
                NotificationId = 1,
                NotificationType = notificationType,
                To = to,
                Cc = "",
                Bcc = "",
                Subject = "subject"
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
        public List<NotificationLogEntry> NotificationLogEntries { get; } = new();
        public List<Alert> Alerts { get; } = new();
        public List<UiUser> UiUsers { get; } = new();
        public List<Management> Managements { get; } = new();
        public List<string> LastQueries { get; } = new();

        public int LogQueryCount { get; private set; }
        public int NotificationLogQueryCount { get; private set; }
        public int NotificationLogLimit { get; private set; }
        public int NotificationLogOffset { get; private set; }
        public int AlertQueryCount { get; private set; }
        public int UserQueryCount { get; private set; }
        public int ManagementQueryCount { get; private set; }
        public string? LastQuery { get; private set; }
        public string? LastSource { get; private set; }
        public bool ThrowNotificationLogQuery { get; set; }

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

            if (typeof(QueryResponseType) == typeof(List<NotificationLogEntry>) && query == MonitorQueries.getNotificationLogEntrys)
            {
                NotificationLogQueryCount++;
                if (ThrowNotificationLogQuery)
                {
                    throw new InvalidOperationException("notification log query failed");
                }
                NotificationLogLimit = GetVariable<int>(variables, "limit");
                NotificationLogOffset = GetVariable<int>(variables, "offset");
                return Task.FromResult((QueryResponseType)(object)NotificationLogEntries);
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

    internal sealed record TestSetup<TComponent>(BunitContext Context, TComponent Component, IRenderedComponent<TComponent> Rendered) : IDisposable
        where TComponent : class, IComponent
    {
        public void Dispose()
        {
            Context.Dispose();
        }
    }
}

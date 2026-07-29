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
    public class UiMonitoringMiscPagesTest
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

        private static MethodInfo GetPrivateMethod(object component, string name, params Type[] parameterTypes)
        {
            return component.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)
                ?? throw new MissingMethodException(component.GetType().FullName, name);
        }

        private static Task InvokePrivateTask(object component, string name, Type[] parameterTypes, params object[] args)
        {
            return (Task)GetPrivateMethod(component, name, parameterTypes).Invoke(component, args)!;
        }

        private static void InvokePrivateVoid(object component, string name, Type[] parameterTypes, params object[] args)
        {
            GetPrivateMethod(component, name, parameterTypes).Invoke(component, args);
        }

        private static MiscTestSetup<TComponent> RenderComponent<TComponent>(MonitorPagesTestApiConn apiConn, Action<SimulatedUserConfig>? configureUserConfig = null)
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

            SimulatedUserConfig userConfig = new();
            configureUserConfig?.Invoke(userConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<TComponent>());
            return new MiscTestSetup<TComponent>(context, component.FindComponent<TComponent>().Instance, apiConn, userConfig);
        }

        [Test]
        public void MonitorDailyChecks_LoadsEntries()
        {
            MonitorPagesTestApiConn apiConn = new();
            apiConn.DailyCheckLogs.Add(CreateLogEntry(1, 10, "daily"));
            apiConn.DailyCheckLogs.Add(CreateLogEntry(2, 11, "daily"));

            using MiscTestSetup<MonitorDailyChecks> setup = RenderComponent<MonitorDailyChecks>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.DailyCheckQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getDailyCheckLogEntrys));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(2));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public void MonitorAutodiscoveryLog_LoadsEntriesUsersAndManagements()
        {
            MonitorPagesTestApiConn apiConn = new();
            apiConn.AutodiscoveryLogs.Add(CreateLogEntry(3, 21, "autodiscovery", 31));
            apiConn.Users.Add(CreateUiUser(21, "user-21"));
            apiConn.Managements.Add(CreateManagement(31, "mgm-31"));

            using MiscTestSetup<MonitorAutodiscoveryLog> setup = RenderComponent<MonitorAutodiscoveryLog>(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.AutodiscoveryQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.UserQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.ManagementQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(DeviceQueries.getManagementNames));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<UiUser>>(setup.Component, "uiUsers"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<Management>>(setup.Component, "managements"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public void MonitorImportStatus_LoadsStatusesSortsAndCountsErrors()
        {
            MonitorPagesTestApiConn apiConn = new();
            apiConn.ImportStatuses.Add(CreateIssueStatus(1, "issue"));
            apiConn.ImportStatuses.Add(CreateRunningStatus(2, "running"));
            apiConn.ImportStatuses.Add(CreateOkStatus(3, "ok"));
            apiConn.ImportStatuses.Add(CreateDisabledStatus(4, "disabled"));

            using MiscTestSetup<MonitorImportStatus> setup = RenderComponent<MonitorImportStatus>(apiConn);

            List<ImportStatus> sortedImportStati = GetPrivateField<List<ImportStatus>>(setup.Component, "sortedImportStati");
            Assert.Multiple(() =>
            {
                Type[] importStatusParameterTypes = new Type[1];
                importStatusParameterTypes[0] = typeof(ImportStatus);
                object[] overdueArgs = new object[1];
                overdueArgs[0] = sortedImportStati[0];
                object[] upcomingArgs = new object[1];
                upcomingArgs[0] = sortedImportStati[1];
                object[] disabledArgs = new object[1];
                disabledArgs[0] = sortedImportStati[3];
                Assert.That(apiConn.ImportStatusQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.LastQuery, Is.EqualTo(MonitorQueries.getImportStatus));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
                Assert.That(sortedImportStati, Has.Count.EqualTo(4));
                Assert.That(sortedImportStati[0].MgmId, Is.EqualTo(1));
                Assert.That(sortedImportStati[0].SortPrio, Is.EqualTo(ImportStatusMonitor.kSortPrioIssue));
                Assert.That(sortedImportStati[1].MgmId, Is.EqualTo(2));
                Assert.That(sortedImportStati[1].SortPrio, Is.EqualTo(ImportStatusMonitor.kSortPrioRunning));
                Assert.That(sortedImportStati[2].MgmId, Is.EqualTo(3));
                Assert.That(sortedImportStati[2].SortPrio, Is.EqualTo(ImportStatusMonitor.kSortPrioOk));
                Assert.That(sortedImportStati[2].ErrorCount, Is.EqualTo(2));
                Assert.That(sortedImportStati[3].MgmId, Is.EqualTo(4));
                Assert.That(sortedImportStati[3].SortPrio, Is.EqualTo(ImportStatusMonitor.kSortPrioDisabled));
                Assert.That((string)GetPrivateMethod(setup.Component, "GetTableRowClass", importStatusParameterTypes).Invoke(setup.Component, overdueArgs)!, Is.EqualTo("background-overdue"));
                Assert.That((string)GetPrivateMethod(setup.Component, "GetTableRowClass", importStatusParameterTypes).Invoke(setup.Component, upcomingArgs)!, Is.EqualTo("background-upcoming"));
                Assert.That((string)GetPrivateMethod(setup.Component, "GetTableRowClass", importStatusParameterTypes).Invoke(setup.Component, disabledArgs)!, Is.EqualTo("background-disabled"));
            });
        }

        [Test]
        public async Task MonitorImportStatus_DetailsRequestRollbackAndRefreshWork()
        {
            MonitorPagesTestApiConn apiConn = new();
            apiConn.ImportStatuses.Add(CreateOkStatus(7, "first"));

            using MiscTestSetup<MonitorImportStatus> setup = RenderComponent<MonitorImportStatus>(apiConn);
            ImportStatus status = GetPrivateField<List<ImportStatus>>(setup.Component, "sortedImportStati")[0];

            Type[] importStatusParameterTypes = new Type[1];
            importStatusParameterTypes[0] = typeof(ImportStatus);
            InvokePrivateVoid(setup.Component, "Details", importStatusParameterTypes, status);
            InvokePrivateVoid(setup.Component, "RequestRollback", importStatusParameterTypes, status);
            apiConn.ImportStatuses.Clear();
            apiConn.ImportStatuses.Add(CreateDisabledStatus(8, "refreshed"));
            await InvokePrivateTask(setup.Component, "Refresh", Array.Empty<Type>());

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(setup.Component, "DetailsMode"), Is.True);
                Assert.That(GetPrivateField<bool>(setup.Component, "rollbackMode"), Is.True);
                Assert.That(apiConn.ImportStatusQueryCount, Is.EqualTo(2));
                Assert.That(GetPrivateField<List<ImportStatus>>(setup.Component, "sortedImportStati"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<ImportStatus>>(setup.Component, "sortedImportStati")[0].MgmId, Is.EqualTo(8));
            });
        }

        [Test]
        public void MonitorUiLog_NonPrivilegedUser_LoadsOwnEntries()
        {
            MonitorPagesTestApiConn apiConn = new();
            apiConn.UiLogs.Add(CreateLogEntry(11, 77, "ui", null));

            using MiscTestSetup<MonitorUiLog> setup = RenderComponent<MonitorUiLog>(apiConn, userConfig =>
            {
                userConfig.User.DbId = 77;
                userConfig.User.Name = "user-77";
            });

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.UiLogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.AllUiLogQueryCount, Is.EqualTo(0));
                Assert.That(apiConn.LastUiLogUserId, Is.EqualTo(77));
                Assert.That(GetPrivateField<bool>(setup.Component, "seeAllUsers"), Is.False);
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public async Task MonitorUiLog_PrivilegedUserLoadsAllUsersAndRefreshesOnSelectionChange()
        {
            MonitorPagesTestApiConn apiConn = new();
            apiConn.UiLogs.Add(CreateLogEntry(20, 13, "ui", null));
            apiConn.AllUiLogs.Add(CreateLogEntry(21, 99, "ui", null));
            apiConn.Users.Add(CreateUiUser(13, "bob"));
            apiConn.Users.Add(CreateUiUser(99, "carol"));

            using MiscTestSetup<MonitorUiLog> setup = RenderComponent<MonitorUiLog>(apiConn, userConfig =>
            {
                userConfig.User.DbId = 13;
                userConfig.User.Name = "bob";
                userConfig.User.Roles.Add(Roles.Admin);
            });

            List<UiUser> uiUsers = GetPrivateField<List<UiUser>>(setup.Component, "uiUsers");
            UiUser allUsersEntry = uiUsers[0];
            Type[] selectUserParameterTypes = new Type[1];
            selectUserParameterTypes[0] = typeof(UiUser);
            await InvokePrivateTask(setup.Component, "SelectUser", selectUserParameterTypes, allUsersEntry);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.UserQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.UiLogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.AllUiLogQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.UiLogUserIds, Has.Count.EqualTo(2));
                Assert.That(apiConn.UiLogUserIds[0], Is.EqualTo(13));
                Assert.That(apiConn.UiLogUserIds[1], Is.EqualTo(-1));
                Assert.That(GetPrivateField<bool>(setup.Component, "seeAllUsers"), Is.True);
                Assert.That(GetPrivateField<UiUser>(setup.Component, "selectedUser").DbId, Is.EqualTo(-1));
                Assert.That(uiUsers, Has.Count.EqualTo(3));
                Assert.That(GetPrivateField<List<LogEntry>>(setup.Component, "logEntrys"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(setup.Component, "InitComplete"), Is.True);
            });
        }

        private static LogEntry CreateLogEntry(long id, int userId, string source, int? managementId = null)
        {
            return new LogEntry
            {
                Id = id,
                UserId = userId,
                Source = source,
                ManagementId = managementId,
                Severity = 2,
                Timestamp = new DateTime(2026, 1, 1, 12, 0, 0),
                SuspectedCause = "cause",
                Description = "description"
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

        private static ImportStatus CreateIssueStatus(int mgmId, string name)
        {
            return new ImportStatus
            {
                MgmId = mgmId,
                MgmName = name,
                DeviceType = CreateDeviceType(mgmId, "Type", "1"),
                LastImport = null,
                ErroneousImports = null
            };
        }

        private static ImportStatus CreateRunningStatus(int mgmId, string name)
        {
            ImportControl[] lastIncompleteImport = new ImportControl[1];
            lastIncompleteImport[0] = new ImportControl
            {
                ControlId = 201,
                StartTime = DateTime.Now.AddHours(-1),
                StopTime = null,
                SuccessfulImport = false
            };

            return new ImportStatus
            {
                MgmId = mgmId,
                MgmName = name,
                DeviceType = CreateDeviceType(mgmId, "Type", "1"),
                LastIncompleteImport = lastIncompleteImport
            };
        }

        private static ImportStatus CreateOkStatus(int mgmId, string name)
        {
            ImportControl[] lastSuccessfulImport = new ImportControl[1];
            lastSuccessfulImport[0] = new ImportControl
            {
                ControlId = 10,
                StartTime = new DateTime(2026, 1, 1, 10, 0, 0),
                StopTime = new DateTime(2026, 1, 1, 10, 30, 0),
                SuccessfulImport = true
            };

            ImportControl[] lastImport = new ImportControl[1];
            lastImport[0] = new ImportControl
            {
                ControlId = 12,
                StartTime = new DateTime(2026, 1, 1, 11, 0, 0),
                StopTime = new DateTime(2026, 1, 1, 11, 20, 0),
                SuccessfulImport = true
            };

            ImportControl[] erroneousImports = new ImportControl[3];
            erroneousImports[0] = new ImportControl { ControlId = 9 };
            erroneousImports[1] = new ImportControl { ControlId = 11 };
            erroneousImports[2] = new ImportControl { ControlId = 12 };

            return new ImportStatus
            {
                MgmId = mgmId,
                MgmName = name,
                DeviceType = CreateDeviceType(mgmId, "Type", "1"),
                LastImport = lastImport,
                LastSuccessfulImport = lastSuccessfulImport,
                ErroneousImports = erroneousImports,
                LastImportAttempt = DateTime.Now
            };
        }

        private static ImportStatus CreateDisabledStatus(int mgmId, string name)
        {
            return new ImportStatus
            {
                MgmId = mgmId,
                MgmName = name,
                DeviceType = CreateDeviceType(mgmId, "Type", "1"),
                ImportDisabled = true
            };
        }

        private static DeviceType CreateDeviceType(int id, string name, string version)
        {
            return new DeviceType
            {
                Id = id,
                Name = name,
                Version = version
            };
        }
    }

    internal sealed record MiscTestSetup<TComponent>(BunitContext Context, TComponent Component, MonitorPagesTestApiConn ApiConn, SimulatedUserConfig UserConfig) : IDisposable
        where TComponent : class, IComponent
    {
        public void Dispose()
        {
            Context.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    internal sealed class MonitorPagesTestApiConn : SimulatedApiConnection
    {
        public List<LogEntry> DailyCheckLogs { get; } = new List<LogEntry>();
        public List<LogEntry> AutodiscoveryLogs { get; } = new List<LogEntry>();
        public List<LogEntry> UiLogs { get; } = new List<LogEntry>();
        public List<LogEntry> AllUiLogs { get; } = new List<LogEntry>();
        public List<UiUser> Users { get; } = new List<UiUser>();
        public List<Management> Managements { get; } = new List<Management>();
        public List<ImportStatus> ImportStatuses { get; } = new List<ImportStatus>();

        public int DailyCheckQueryCount { get; private set; }
        public int AutodiscoveryQueryCount { get; private set; }
        public int UiLogQueryCount { get; private set; }
        public int AllUiLogQueryCount { get; private set; }
        public int UserQueryCount { get; private set; }
        public int ManagementQueryCount { get; private set; }
        public int ImportStatusQueryCount { get; private set; }
        public string? LastQuery { get; private set; }
        public int LastUiLogUserId { get; private set; }
        public List<int> UiLogUserIds { get; } = new List<int>();

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            LastQuery = query;

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getDailyCheckLogEntrys)
            {
                DailyCheckQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<LogEntry>(DailyCheckLogs));
            }

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getAutodiscoveryLogEntrys)
            {
                AutodiscoveryQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<LogEntry>(AutodiscoveryLogs));
            }

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getUiLogEntrys)
            {
                UiLogQueryCount++;
                LastUiLogUserId = GetValue<int>(variables, "user");
                UiLogUserIds.Add(LastUiLogUserId);
                return Task.FromResult((QueryResponseType)(object)new List<LogEntry>(UiLogs));
            }

            if (typeof(QueryResponseType) == typeof(List<LogEntry>) && query == MonitorQueries.getAllUiLogEntrys)
            {
                AllUiLogQueryCount++;
                LastUiLogUserId = -1;
                UiLogUserIds.Add(LastUiLogUserId);
                return Task.FromResult((QueryResponseType)(object)new List<LogEntry>(AllUiLogs));
            }

            if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUsers)
            {
                UserQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<UiUser>(Users));
            }

            if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
            {
                ManagementQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<Management>(Managements));
            }

            if (typeof(QueryResponseType) == typeof(List<ImportStatus>) && query == MonitorQueries.getImportStatus)
            {
                ImportStatusQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<ImportStatus>(ImportStatuses));
            }

            throw new NotImplementedException();
        }

        private static TValue GetValue<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            if (property == null)
            {
                return default!;
            }
            return (TValue)property.GetValue(variables)!;
        }
    }
}

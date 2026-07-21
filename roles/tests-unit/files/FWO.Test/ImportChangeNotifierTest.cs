using System.Linq;
using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ImportChangeNotifierTest
    {
        private static readonly int[] kExpectedImportedManagements = [3, 5];
        private static readonly int[] kExpectedExpandedManagementIds = [10, 11, 2];

        [Test]
        public async Task Run_ReturnsWithoutSendingWhenNoNewImportsFound()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = []
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            await notifier.Run();

            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getImportsToNotifyForRuleChanges));
            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task Run_ReturnsWithoutNotificationsConfigured_WhenImportsExist()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(11L, 1, "mgmt-a", new DateTime(2026, 7, 14, 8, 0, 0), 3)],
                Notifications = []
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            await notifier.Run();

            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
            Assert.That(apiConnection.NotificationQueryCalls, Is.EqualTo(1));
            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task Run_ResetsWorkInProgress_OnImportQueryException()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ThrowOnImportQuery = true
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await notifier.Run());

            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
        }

        [Test]
        public async Task NewImportFound_UsesObjectChangeQuery_WhenEnabled()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImpChangeIncludeObjectChanges = true
            };
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify =
                [
                    CreateImport(11L, 3, "mgmt-c", new DateTime(2026, 7, 14, 8, 0, 0), 2),
                    CreateImport(12L, 3, "mgmt-c", new DateTime(2026, 7, 14, 8, 5, 0), 5),
                    CreateImport(13L, 5, "mgmt-e", new DateTime(2026, 7, 14, 8, 10, 0), 1)
                ]
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            bool found = await InvokePrivateAsync<bool>(notifier, "NewImportFound");

            Assert.That(found, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getImportsToNotifyForAnyChanges));
            Assert.That(GetPrivateField<List<int>>(notifier, "importedManagements"), Is.EqualTo(kExpectedImportedManagements));
        }

        [Test]
        public async Task NewImportFound_UsesRuleChangeQuery_WhenDisabled()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImpChangeIncludeObjectChanges = false
            };
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(21L, 7, "mgmt-g", new DateTime(2026, 7, 14, 9, 0, 0), 4)]
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            bool found = await InvokePrivateAsync<bool>(notifier, "NewImportFound");

            Assert.That(found, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getImportsToNotifyForRuleChanges));
        }

        [Test]
        public void CreateBody_SummarizesImportsPerManagement()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new();
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);
            SetPrivateField(notifier, "importsToNotify", CreateImportList(
                CreateImport(31L, 2, "Alpha", new DateTime(2026, 7, 14, 10, 0, 0), 4),
                CreateImport(32L, 2, "Alpha", new DateTime(2026, 7, 14, 10, 5, 0), 1),
                CreateImport(33L, 5, "Beta", new DateTime(2026, 7, 14, 10, 10, 0), 3)));
            SetPrivateField(notifier, "importedManagements", new List<int> { 2, 5 });

            string body = InvokePrivate<string>(notifier, "CreateBody");

            Assert.That(body, Is.EqualTo(
                $"Alpha (id=2): 5 {GlobalConst.kUndefinedText}" + Environment.NewLine + Environment.NewLine +
                $"Beta (id=5): 3 {GlobalConst.kUndefinedText}"));
        }

        [Test]
        public async Task SetFilters_ExpandsSuperManagersAndDeduplicatesManagements()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImpChangeIncludeObjectChanges = true
            };
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                Managements =
                [
                    new ManagementSelect
                    {
                        Id = 1,
                        IsSuperManager = true,
                        subManagers =
                        [
                            new ManagementSelect { Id = 10, Name = "A" },
                            new ManagementSelect { Id = 11, Name = "B" },
                            new ManagementSelect { Id = 10, Name = "A duplicate" }
                        ]
                    },
                    new ManagementSelect
                    {
                        Id = 2,
                        Name = "C",
                        IsSuperManager = false
                    }
                ]
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);
            SetPrivateField(notifier, "importedManagements", new List<int> { 1, 2 });
            SetPrivateField(notifier, "importsToNotify", CreateImportList(
                CreateImport(41L, 1, "Super", new DateTime(2026, 7, 14, 11, 0, 0), 2),
                CreateImport(42L, 2, "Direct", new DateTime(2026, 7, 14, 11, 10, 0), 3)));

            ReportParams reportParams = await InvokePrivateAsync<ReportParams>(notifier, "SetFilters");

            DeviceFilter deviceFilter = GetPrivateField<DeviceFilter>(notifier, "deviceFilter");
            Assert.That(reportParams.IncludeObjects, Is.True);
            Assert.That(reportParams.TimeFilter.TimeRangeType, Is.EqualTo(TimeRangeType.Fixeddates));
            Assert.That(deviceFilter.Managements.Select(m => m.Id), Is.EqualTo(kExpectedExpandedManagementIds));
        }

        [Test]
        public async Task SetImportsNotified_SwallowsErrors()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ThrowOnSetImportsNotified = true
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);
            SetPrivateField(notifier, "importsToNotify", CreateImportList(
                CreateImport(51L, 2, "Alpha", new DateTime(2026, 7, 14, 12, 0, 0), 4)));

            await InvokePrivate(notifier, "SetImportsNotified");

            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(1));
        }

        private static object CreateImport(long controlId, int mgmtId, string mgmtName, DateTime stopTime, int relevantChanges)
        {
            Type importType = typeof(ImportChangeNotifier).GetNestedType("ImportToNotify", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ImportToNotify type not found.");
            Type importManagementType = typeof(ImportChangeNotifier).GetNestedType("ImportManagement", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ImportManagement type not found.");

            object mgmt = Activator.CreateInstance(importManagementType)
                ?? throw new InvalidOperationException("Could not create import management.");
            importManagementType.GetProperty("MgmtName", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(mgmt, mgmtName);

            object import = Activator.CreateInstance(importType)
                ?? throw new InvalidOperationException("Could not create import.");
            importType.GetProperty("ControlId", BindingFlags.Public | BindingFlags.Instance)!.SetValue(import, controlId);
            importType.GetProperty("MgmtId", BindingFlags.Public | BindingFlags.Instance)!.SetValue(import, mgmtId);
            importType.GetProperty("Mgmt", BindingFlags.Public | BindingFlags.Instance)!.SetValue(import, mgmt);
            importType.GetProperty("StopTime", BindingFlags.Public | BindingFlags.Instance)!.SetValue(import, stopTime);
            importType.GetProperty("RelevantChanges", BindingFlags.Public | BindingFlags.Instance)!.SetValue(import, relevantChanges);
            return import;
        }

        private static object CreateImportList(params object[] imports)
        {
            Type importType = typeof(ImportChangeNotifier).GetNestedType("ImportToNotify", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ImportToNotify type not found.");
            Type listType = typeof(List<>).MakeGenericType(importType);
            object list = Activator.CreateInstance(listType)
                ?? throw new InvalidOperationException("Could not create import list.");

            System.Collections.IList typedList = (System.Collections.IList)list;
            foreach (object import in imports)
            {
                typedList.Add(import);
            }
            return list;
        }

        private static T InvokePrivate<T>(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return (T)method.Invoke(instance, parameters)!;
        }

        private static async Task InvokePrivate(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            await (Task)method.Invoke(instance, parameters)!;
        }

        private static async Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return await (Task<T>)method.Invoke(instance, parameters)!;
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{fieldName} not found.");
            return (T)field.GetValue(instance)!;
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{fieldName} not found.");
            field.SetValue(instance, value);
        }

        private sealed class ImportChangeNotifierTestApiConn : SimulatedApiConnection
        {
            public List<object> ImportsToNotify { get; init; } = [];
            public List<FwoNotification> Notifications { get; init; } = [];
            public List<ManagementSelect> Managements { get; init; } = [];
            public bool ThrowOnImportQuery { get; init; }
            public bool ThrowOnSetImportsNotified { get; init; }
            public string? LastQuery { get; private set; }
            public object? LastVariables { get; private set; }
            public int NotificationQueryCalls { get; private set; }
            public int SetImportsNotifiedCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;

                if (query == ReportQueries.getImportsToNotifyForAnyChanges || query == ReportQueries.getImportsToNotifyForRuleChanges)
                {
                    if (ThrowOnImportQuery)
                    {
                        throw new InvalidOperationException("Import query failed.");
                    }

                    return Task.FromResult((QueryResponseType)CreateImportList());
                }

                if (query == NotificationQueries.getNotifications)
                {
                    ++NotificationQueryCalls;
                    return Task.FromResult((QueryResponseType)(object)Notifications.ToList());
                }

                if (query == NotificationQueries.updateNotificationsLastSent)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 0 });
                }

                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (query == DeviceQueries.getDevicesByManagementOrSuperMgm)
                {
                    return Task.FromResult((QueryResponseType)(object)Managements.Select(CloneManagement).ToList());
                }

                if (query == ReportQueries.setImportsNotified)
                {
                    ++SetImportsNotifiedCalls;
                    if (ThrowOnSetImportsNotified)
                    {
                        throw new InvalidOperationException("SetImportsNotified failed.");
                    }

                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = ExtractIds(variables).Count });
                }

                throw new NotImplementedException();
            }

            private object CreateImportList()
            {
                return ImportChangeNotifierTest.CreateImportList(ImportsToNotify.ToArray());
            }

            private static ManagementSelect CloneManagement(ManagementSelect management)
            {
                return new()
                {
                    Id = management.Id,
                    Name = management.Name,
                    Uid = management.Uid,
                    IsSuperManager = management.IsSuperManager,
                    subManagers = management.subManagers.Select(CloneManagement).ToList(),
                    Devices = management.Devices.Select(device => new DeviceSelect(device)).ToList()
                };
            }

            private static List<int> ExtractIds(object? variables)
            {
                if (variables == null)
                {
                    return [];
                }

                PropertyInfo? idsProperty = variables.GetType().GetProperty("ids", BindingFlags.Public | BindingFlags.Instance);
                if (idsProperty?.GetValue(variables) is IEnumerable<long> longIds)
                {
                    return longIds.Select(id => (int)id).ToList();
                }

                if (idsProperty?.GetValue(variables) is IEnumerable<int> intIds)
                {
                    return intIds.ToList();
                }

                return [];
            }
        }
    }
}

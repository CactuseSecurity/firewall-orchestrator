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
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ImportChangeNotifierTest
    {
        private static readonly int[] kExpectedImportedManagements = [3, 5];
        private static readonly int[] kExpectedExpandedManagementIds = [10, 11, 2];
        private static readonly object?[] kMissingReportArgument = [null];
        private static readonly string[] kExpectedNotificationClients = [nameof(NotificationClient.ImportChange), nameof(NotificationClient.ImportNatChange)];

        [Test]
        public async Task Run_ReturnsWithoutSendingWhenNoNewImportsFound()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [],
                Notifications = [new FwoNotification { Id = 1, Layout = NotificationLayout.SimpleText }]
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            await notifier.Run();

            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getImportsToNotifyForRuleChanges));
            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task Run_WithoutNotificationsConfigured_DoesNotQueryImports()
        {
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(11L, 1, "mgmt-a", new DateTime(2026, 7, 14, 8, 0, 0), 3)]
            };
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig());

            await notifier.Run();

            Assert.That(apiConnection.ImportQueryCalls, Is.EqualTo(0));
            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
        }

        /// <summary>
        /// Imports with object changes only are found when NAT rule change notifications are configured. Without object change
        /// notification they must not trigger the rule change notifications, but are marked as notified.
        /// </summary>
        [Test]
        public async Task Run_ObjectChangesOnly_SendsNoRuleChangeNotification()
        {
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(11L, 1, "mgmt-a", new DateTime(2026, 7, 14, 8, 0, 0), 0)],
                Notifications = [new FwoNotification { Id = 1, Layout = NotificationLayout.HtmlInBody }]
            };
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig { ImpChangeIncludeObjectChanges = false });

            await notifier.Run();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.DevicesQueryCalls, Is.EqualTo(0), "no change report is generated");
                Assert.That(apiConnection.UpdateNotificationsLastSentCalls, Is.EqualTo(0), "no notification is sent");
                Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(1));
            });
        }

        /// <summary>
        /// Rule change notifications have to be based on the security-relevant change counter:
        /// policy_changes_found also covers documentation-only rule changes (e.g. comment or name),
        /// which do not show up in the attached change report.
        /// </summary>
        [Test]
        public void RuleChangeNotificationQuery_FiltersOnSecurityRelevantChanges()
        {
            Assert.That(ReportQueries.getImportsToNotifyForRuleChanges, Does.Contain("security_relevant_changes_counter: {_gt: 0}"));
            Assert.That(ReportQueries.getImportsToNotifyForRuleChanges, Does.Not.Contain("policy_changes_found"));
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
            Assert.That(apiConnection.NotificationQueryCalls, Is.EqualTo(2), "rule change and NAT rule change notifications are loaded");
            Assert.That(apiConnection.LdapQueryCalls, Is.EqualTo(0));
            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task Run_ResetsWorkInProgress_OnImportQueryException()
        {
            SimulatedGlobalConfig globalConfig = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ThrowOnImportQuery = true,
                Notifications = [new FwoNotification { Id = 1, Layout = NotificationLayout.SimpleText }]
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await notifier.Run());

            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
        }

        [Test]
        public void Run_CanceledBeforeSending_KeepsImportsUnnotified()
        {
            using CancellationTokenSource cancellationTokenSource = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(11L, 1, "mgmt-a", new DateTime(2026, 7, 14, 8, 0, 0), 3)],
                Notifications = [new FwoNotification { Id = 1, Layout = NotificationLayout.SimpleText }],
                OnNotificationQuery = cancellationTokenSource.Cancel
            };
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig());

            Assert.ThrowsAsync<OperationCanceledException>(async () => await notifier.Run(cancellationTokenSource.Token));

            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
        }

        [Test]
        public void Run_CanceledWhileGeneratingChangeReport_RethrowsAndKeepsImportsUnnotified()
        {
            using CancellationTokenSource cancellationTokenSource = new();
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(11L, 1, "mgmt-a", new DateTime(2026, 7, 14, 8, 0, 0), 3)],
                Notifications = [new FwoNotification { Id = 1, Layout = NotificationLayout.HtmlInBody }],
                OnDevicesQuery = () =>
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }
            };
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig());

            Assert.ThrowsAsync<OperationCanceledException>(async () => await notifier.Run(cancellationTokenSource.Token));

            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.False);
        }

        [Test]
        public async Task Run_WhileWorkInProgress_ReturnsWithoutQuerying()
        {
            ImportChangeNotifierTestApiConn apiConnection = new();
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig());
            SetPrivateField(notifier, "WorkInProgress", true);

            await notifier.Run();

            Assert.That(apiConnection.LastQuery, Is.Not.EqualTo(ReportQueries.getImportsToNotifyForRuleChanges));
            Assert.That(GetPrivateField<bool>(notifier, "WorkInProgress"), Is.True, "the running notification keeps its flag");
        }

        [Test]
        public void Run_PreCanceledToken_StopsBeforeQueryingImports()
        {
            ImportChangeNotifierTestApiConn apiConnection = new();
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig());

            Assert.ThrowsAsync<OperationCanceledException>(async () => await notifier.Run(new CancellationToken(canceled: true)));

            Assert.That(apiConnection.LastQuery, Is.Not.EqualTo(ReportQueries.getImportsToNotifyForRuleChanges));
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

            bool found = await InvokePrivateAsync<bool>(notifier, "NewImportFound", false);

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

            bool found = await InvokePrivateAsync<bool>(notifier, "NewImportFound", false);

            Assert.That(found, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getImportsToNotifyForRuleChanges));
        }

        /// <summary>
        /// Object changes may change NAT rules without a new rule version, so imports with object changes only are needed.
        /// </summary>
        [Test]
        public async Task NewImportFound_UsesObjectChangeQuery_WhenNatChangeNotificationsConfigured()
        {
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(21L, 7, "mgmt-g", new DateTime(2026, 7, 14, 9, 0, 0), 0)]
            };
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig { ImpChangeIncludeObjectChanges = false });

            bool found = await InvokePrivateAsync<bool>(notifier, "NewImportFound", true);

            Assert.That(found, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getImportsToNotifyForAnyChanges));
        }

        [TestCase(false, 0, false)]
        [TestCase(false, 2, true)]
        [TestCase(true, 0, true)]
        public void HasRuleChangesToNotify_DependsOnRuleChangesAndObjectChangeSetting(bool includeObjectChanges, int relevantChanges, bool expected)
        {
            ImportChangeNotifier notifier = new(new ImportChangeNotifierTestApiConn(), new SimulatedGlobalConfig { ImpChangeIncludeObjectChanges = includeObjectChanges });
            SetPrivateField(notifier, "importsToNotify", CreateImportList(
                CreateImport(22L, 7, "mgmt-g", new DateTime(2026, 7, 14, 9, 0, 0), relevantChanges)));

            Assert.That(InvokePrivate<bool>(notifier, "HasRuleChangesToNotify"), Is.EqualTo(expected));
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

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void CreateBody_ListsManagementsWithoutRuleChangesOnlyWithObjectChanges(bool includeObjectChanges, bool expectListed)
        {
            ImportChangeNotifier notifier = new(new ImportChangeNotifierTestApiConn(), new SimulatedGlobalConfig { ImpChangeIncludeObjectChanges = includeObjectChanges });
            SetPrivateField(notifier, "importsToNotify", CreateImportList(
                CreateImport(34L, 2, "Alpha", new DateTime(2026, 7, 14, 10, 0, 0), 4),
                CreateImport(35L, 5, "ObjectsOnly", new DateTime(2026, 7, 14, 10, 5, 0), 0)));
            SetPrivateField(notifier, "importedManagements", new List<int> { 2, 5 });

            string body = InvokePrivate<string>(notifier, "CreateBody");

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.StartWith($"Alpha (id=2): 4 {GlobalConst.kUndefinedText}"));
                Assert.That(body.Contains("ObjectsOnly (id=5): 0"), Is.EqualTo(expectListed));
            });
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

            ReportParams reportParams = await InvokePrivateAsync<ReportParams>(notifier, "SetFilters", ReportType.Changes);

            DeviceFilter deviceFilter = GetPrivateField<DeviceFilter>(notifier, "deviceFilter");
            Assert.That(reportParams.ReportType, Is.EqualTo((int)ReportType.Changes));
            Assert.That(reportParams.IncludeObjects, Is.True);
            Assert.That(reportParams.TimeFilter.TimeRangeType, Is.EqualTo(TimeRangeType.Fixeddates));
            Assert.That(deviceFilter.Managements.Select(m => m.Id), Is.EqualTo(kExpectedExpandedManagementIds));
        }

        [Test]
        public async Task SetFilters_NatChanges_IncludesObjectChangesUsedInNatRules()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImpChangeIncludeObjectChanges = false
            };
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                Managements = [new ManagementSelect { Id = 2, Name = "C" }]
            };
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);
            SetPrivateField(notifier, "importedManagements", new List<int> { 2 });
            SetPrivateField(notifier, "importsToNotify", CreateImportList(
                CreateImport(42L, 2, "Direct", new DateTime(2026, 7, 14, 11, 10, 0), 3)));

            ReportParams reportParams = await InvokePrivateAsync<ReportParams>(notifier, "SetFilters", ReportType.NatChanges);

            Assert.That(reportParams.ReportType, Is.EqualTo((int)ReportType.NatChanges));
            Assert.That(reportParams.IncludeObjects, Is.True, "changes of objects used in NAT rules are always notified");
        }

        [Test]
        public void CreateNatChangesBody_ListsManagementsWithNatRuleChanges()
        {
            ImportChangeNotifier notifier = new(new ImportChangeNotifierTestApiConn(), new SimulatedGlobalConfig());
            ReportChanges natReport = CreateNatChangesReport(
                new ManagementReport { Id = 2, Name = "Alpha", RuleChanges = [new RuleChange(), new RuleChange()] },
                new ManagementReport { Id = 4, Name = "Unchanged", RuleChanges = [] },
                new ManagementReport { Id = 5, Name = "Beta", RuleChanges = [new RuleChange()] });

            string body = InvokePrivate<string>(notifier, "CreateNatChangesBody", natReport);

            Assert.That(body, Is.EqualTo(
                $"Alpha (id=2): 2 {GlobalConst.kUndefinedText}" + Environment.NewLine + Environment.NewLine +
                $"Beta (id=5): 1 {GlobalConst.kUndefinedText}"));
        }

        [Test]
        public void CreateNatChangesBody_ListsChangesOfObjectsUsedInNatRules()
        {
            ImportChangeNotifier notifier = new(new ImportChangeNotifierTestApiConn(), new SimulatedGlobalConfig());
            ReportChanges natReport = CreateNatChangesReport(
                new ManagementReport { Id = 2, Name = "Alpha", RuleChanges = [], ObjectChanges = [new ObjectChange()], ServiceChanges = [new ServiceChange()], UserChanges = [new UserChange()] });

            string body = InvokePrivate<string>(notifier, "CreateNatChangesBody", natReport);

            Assert.That(body, Is.EqualTo($"Alpha (id=2): 0 {GlobalConst.kUndefinedText}, 3 {GlobalConst.kUndefinedText}"));
        }

        [Test]
        public void HasNatChangesToNotify_DependsOnNatChangesInReport()
        {
            ReportChanges reportWithChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [new RuleChange()] });
            ReportChanges reportWithoutChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [] });
            ReportChanges reportWithObjectChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [], ServiceChanges = [new ServiceChange()] });

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateStatic<bool>("HasNatChangesToNotify", reportWithChanges), Is.True);
                Assert.That(InvokePrivateStatic<bool>("HasNatChangesToNotify", reportWithObjectChanges), Is.True, "changes of objects used in NAT rules are notified");
                Assert.That(InvokePrivateStatic<bool>("HasNatChangesToNotify", reportWithoutChanges), Is.False);
                Assert.That(InvokePrivateStatic<bool>("HasNatChangesToNotify", kMissingReportArgument), Is.True, "failed report generation must not suppress the notification");
            });
        }

        [Test]
        public void UseNatChangesReportForRuleChanges_OnlyWhenImportsContainNothingButNatRuleChanges()
        {
            ReportChanges emptyRuleChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [] });
            ReportChanges ruleChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [new RuleChange()] });
            ReportChanges objectChangesOnly = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [], ObjectChanges = [new ObjectChange()] });
            ReportChanges natChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [new RuleChange()] });
            ReportChanges emptyNatChanges = CreateNatChangesReport(new ManagementReport { Id = 1, RuleChanges = [] });
            object?[] noChangeReport = [null, natChanges];
            object?[] noNatReport = [emptyRuleChanges, null];

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateStatic<bool>("UseNatChangesReportForRuleChanges", emptyRuleChanges, natChanges), Is.True);
                Assert.That(InvokePrivateStatic<bool>("UseNatChangesReportForRuleChanges", ruleChanges, natChanges), Is.False);
                Assert.That(InvokePrivateStatic<bool>("UseNatChangesReportForRuleChanges", objectChangesOnly, natChanges), Is.False, "object changes keep the rule changes report");
                Assert.That(InvokePrivateStatic<bool>("UseNatChangesReportForRuleChanges", emptyRuleChanges, emptyNatChanges), Is.False);
                Assert.That(InvokePrivateStatic<bool>("UseNatChangesReportForRuleChanges", noChangeReport), Is.False, "simple text notifications or failed generation");
                Assert.That(InvokePrivateStatic<bool>("UseNatChangesReportForRuleChanges", noNatReport), Is.False);
            });
        }

        [Test]
        public async Task Run_LoadsNatChangeNotificationsSeparately()
        {
            ImportChangeNotifierTestApiConn apiConnection = new()
            {
                ImportsToNotify = [CreateImport(11L, 1, "mgmt-a", new DateTime(2026, 7, 14, 8, 0, 0), 3)]
            };
            ImportChangeNotifier notifier = new(apiConnection, new SimulatedGlobalConfig());

            await notifier.Run();

            Assert.That(apiConnection.RequestedNotificationClients, Is.EqualTo(kExpectedNotificationClients));
            Assert.That(apiConnection.SetImportsNotifiedCalls, Is.EqualTo(0));
        }

        private static ReportChanges CreateNatChangesReport(params ManagementReport[] managements)
        {
            ReportData reportData = new();
            reportData.ManagementData.AddRange(managements);
            return new ReportChanges(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.NatChanges, new TimeFilter(), false)
            {
                ReportData = reportData
            };
        }

        private static T InvokePrivateStatic<T>(string methodName, params object?[] parameters)
        {
            MethodInfo method = typeof(ImportChangeNotifier).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return (T)method.Invoke(null, parameters)!;
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
            public Action? OnNotificationQuery { get; init; }
            public Action? OnDevicesQuery { get; init; }
            public string? LastQuery { get; private set; }
            public object? LastVariables { get; private set; }
            public int LdapQueryCalls { get; private set; }
            public int NotificationQueryCalls { get; private set; }
            public List<string> RequestedNotificationClients { get; } = [];
            public int SetImportsNotifiedCalls { get; private set; }
            public List<FwoNotification> NatNotifications { get; init; } = [];
            public int ImportQueryCalls { get; private set; }
            public int DevicesQueryCalls { get; private set; }
            public int UpdateNotificationsLastSentCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;

                if (query == ReportQueries.getImportsToNotifyForAnyChanges || query == ReportQueries.getImportsToNotifyForRuleChanges)
                {
                    ++ImportQueryCalls;
                    if (ThrowOnImportQuery)
                    {
                        throw new InvalidOperationException("Import query failed.");
                    }

                    return Task.FromResult((QueryResponseType)CreateImportList());
                }

                if (query == NotificationQueries.getNotifications)
                {
                    ++NotificationQueryCalls;
                    string client = variables?.GetType().GetProperty("client")?.GetValue(variables)?.ToString() ?? "";
                    RequestedNotificationClients.Add(client);
                    OnNotificationQuery?.Invoke();
                    List<FwoNotification> notifications = client == nameof(NotificationClient.ImportNatChange) ? NatNotifications : Notifications;
                    return Task.FromResult((QueryResponseType)(object)notifications.ToList());
                }

                if (query == AuthQueries.getLdapConnections)
                {
                    ++LdapQueryCalls;
                    return Task.FromResult((QueryResponseType)(object)new List<Ldap>());
                }

                if (query == NotificationQueries.updateNotificationsLastSent)
                {
                    ++UpdateNotificationsLastSentCalls;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 0 });
                }

                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (query == DeviceQueries.getDevicesByManagementOrSuperMgm)
                {
                    ++DevicesQueryCalls;
                    OnDevicesQuery?.Invoke();
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

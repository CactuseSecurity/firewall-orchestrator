using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Report;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ReportDevicesBaseTest
    {
        private const int kManagementId = 1;
        private const int kSubManagementId = 2;
        private const int kOtherManagementId = 3;
        private const int kGatewayId = 11;
        private const int kRulebaseId = 21;

        private readonly SimulatedUserConfig userConfig = new();

        /// <summary>
        /// Answers the import id queries of the device reports with the given managements.
        /// </summary>
        private sealed class ImportIdApiConnection(List<ManagementReport> managements) : SimulatedApiConnection
        {
            public object? Variables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getRelevantImportIdsInTimeRange || query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    Variables = variables;
                    return Task.FromResult((QueryResponseType)(object)managements);
                }
                throw new NotImplementedException(query);
            }
        }

        private ReportDevicesBase CreateReport(ReportType reportType = ReportType.Rules)
        {
            ReportTemplate template = new("", new ReportParams((int)reportType, new DeviceFilter()));
            return (ReportDevicesBase)ReportBase.ConstructReport(template, userConfig);
        }

        private static ManagementReport ManagementWithImport(int id, long? relevantImportId, params long[] importControlIds)
        {
            ManagementReport management = new()
            {
                Id = id,
                Name = $"management {id}",
                ImportControls = [.. importControlIds.Select(controlId => new ImportControl { ControlId = controlId })]
            };
            management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = relevantImportId;
            return management;
        }

        private static DeviceReport GatewayWithLinks(params RulebaseLink[] links)
        {
            return new DeviceReport { Id = kGatewayId, Name = "gateway", RulebaseLinks = links };
        }

        private static RulebaseLink InitialLink(int rulebaseId)
        {
            return new RulebaseLink { GatewayId = kGatewayId, NextRulebaseId = rulebaseId, IsInitial = true };
        }

        [Test]
        public void TheExtendedReportLoggingIsTakenFromTheGlobalDebugConfig()
        {
            SimulatedGlobalConfig globalConfig = new() { DebugConfig = System.Text.Json.JsonSerializer.Serialize(new DebugConfig { ExtendedLogReportGeneration = true }) };
            typeof(UserConfig).GetField("globalConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(userConfig, globalConfig);

            ReportDevicesBase report = CreateReport();

            DebugConfig debugConfig = (DebugConfig)typeof(ReportDevicesBase).GetField("_debugConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(report)!;
            Assert.That(debugConfig.ExtendedLogReportGeneration, Is.True);
            Assert.That(report.NoRuleFound(), Is.True);
        }

        [Test]
        public void WithoutDebugConfigTheExtendedReportLoggingIsOff()
        {
            ReportDevicesBase report = CreateReport();

            DebugConfig debugConfig = (DebugConfig)typeof(ReportDevicesBase).GetField("_debugConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(report)!;
            Assert.That(debugConfig.ExtendedLogReportGeneration, Is.False);
        }

        [Test]
        public async Task ImportsInTheTimeRangeOfASuperManagerAreHandedToItsSubManagements()
        {
            ManagementReport superManager = ManagementWithImport(kManagementId, 5, 7, 9);
            superManager.SubManagements = [new Management { Id = kSubManagementId }, new Management { Id = 99 }];
            ManagementReport subManagement = ManagementWithImport(kSubManagementId, 4, 8);
            ImportIdApiConnection apiConnection = new([superManager, subManagement]);
            ReportDevicesBase report = CreateReport(ReportType.Changes);

            List<ManagementReport> managements = await report.GetImportIdsInTimeRange(apiConnection, "2026-01-01", "2026-02-01", ruleChangeRequired: true);

            Assert.That(managements.Select(management => management.Id), Is.EqualTo((List<int>)[kSubManagementId]));
            Assert.That(managements[0].ImportControls.Select(import => import.ControlId), Is.EqualTo((List<long>)[7, 8, 9]));
            Assert.That(managements[0].RelevantImportId, Is.EqualTo(4));
        }

        [TestCase(true, false, true)]
        [TestCase(null, false, null)]
        [TestCase(true, true, null)]
        public async Task OnlyRuleChangesAreRequiredWhenObjectChangesAreNotIncluded(bool? ruleChangeRequired, bool includeObjects, bool? expected)
        {
            ImportIdApiConnection apiConnection = new([]);
            ReportDevicesBase report = CreateReport(ReportType.Changes);

            await report.GetImportIdsInTimeRange(apiConnection, "2026-01-01", "2026-02-01", ruleChangeRequired, includeObjects);

            object variables = apiConnection.Variables!;
            Assert.That(variables.GetType().GetProperty("ruleChangesFound")!.GetValue(variables), Is.EqualTo(expected));
            Assert.That(variables.GetType().GetProperty("start_time")!.GetValue(variables), Is.EqualTo("2026-01-01"));
        }

        [Test]
        public async Task ASubManagementUsesTheNewerImportOfItsSuperManagerAndTheSuperManagerIsLeftOut()
        {
            ManagementReport superManager = ManagementWithImport(kManagementId, 9);
            superManager.IsSuperManager = true;
            ManagementReport olderSubManagement = ManagementWithImport(kSubManagementId, 5);
            olderSubManagement.SuperManagerId = kManagementId;
            ManagementReport newerSubManagement = ManagementWithImport(kOtherManagementId, 12);
            newerSubManagement.SuperManagerId = kManagementId;
            ReportDevicesBase report = CreateReport();

            List<ManagementReport> managements = await report.GetRelevantImportIds(
                new ImportIdApiConnection([superManager, olderSubManagement, newerSubManagement]), "2026-01-01");

            Assert.That(managements.Select(management => (management.Id, management.RelevantImportId)),
                Is.EqualTo(new List<(int, long?)> { (kSubManagementId, 9), (kOtherManagementId, 12) }));
            Assert.That(olderSubManagement.Import.ImportAggregate.ImportAggregateMax.RelevantImportId, Is.EqualTo(9));
            // the super manager stays in the import ranges, because the rules of its global rulebases carry its id
            Assert.That(report.GetImportRange(kManagementId), Is.EqualTo(new ImportRange(9, 9)));
        }

        [Test]
        public async Task TheImportRangeOfAReportReachesFromTheStartImportToTheLastImportInThePeriod()
        {
            ReportDevicesBase report = CreateReport(ReportType.Changes);
            await report.GetRelevantImportIds(new ImportIdApiConnection([ManagementWithImport(kManagementId, 5), ManagementWithImport(kOtherManagementId, null)]), "2026-01-01");

            await report.GetImportIdsInTimeRange(new ImportIdApiConnection([ManagementWithImport(kManagementId, 5, 6, 8), ManagementWithImport(42, 3, 4)]), "2026-01-01", "2026-02-01");

            Assert.That(report.GetImportRange(kManagementId), Is.EqualTo(new ImportRange(5, 8)));
            Assert.That(report.GetImportRange(kOtherManagementId), Is.EqualTo(new ImportRange(-1, -1)));
            Assert.That(report.GetImportRange(42), Is.Null);
        }

        [Test]
        public async Task TheUsageCheckWorksOnACopyAndDeselectsManagementsWithoutSelectedGateway()
        {
            // the caller keeps showing its own filter, which a shallow copy used to clear
            DeviceFilter deviceFilter = new(
            [
                new ManagementSelect
                {
                    Id = kManagementId,
                    Selected = true,
                    Devices = [new DeviceSelect { Id = kGatewayId, Name = "selected gateway", Selected = true }, new DeviceSelect { Id = 12, Name = "other gateway" }]
                }
            ]);

            (List<string> unsupportedList, DeviceFilter reducedDeviceFilter) = await ReportDevicesBase.GetUsageDataUnsupportedDevices(new SimulatedApiConnection(), deviceFilter);

            Assert.That(unsupportedList, Does.Not.Contain("other gateway"));
            Assert.That(deviceFilter.Managements[0].Devices[0].Selected, Is.True);
            Assert.That(deviceFilter.Managements[0].Selected, Is.True);
            Assert.That(reducedDeviceFilter.Managements[0].Selected, Is.EqualTo(reducedDeviceFilter.Managements[0].Devices.Any(device => device.Selected)));
        }

        [Test]
        public void AReportWithoutGatewaysHasNoRules()
        {
            ReportDevicesBase report = CreateReport();
            report.ReportData.ManagementData = [new ManagementReport { Id = kManagementId, Devices = [] }];

            Assert.That(report.NoRuleFound(), Is.True);
        }

        [Test]
        public void AGatewayWithoutRulebaseLinksHasNoRules()
        {
            ReportDevicesBase report = CreateReport();
            report.ReportData.ManagementData = [new ManagementReport { Id = kManagementId, Devices = [GatewayWithLinks()] }];

            Assert.That(report.NoRuleFound(), Is.True);
        }

        [Test]
        public void AGatewayWithoutInitialRulebaseHasNoRules()
        {
            ReportDevicesBase report = CreateReport();
            ManagementReport management = new()
            {
                Id = kManagementId,
                Rulebases = [new RulebaseReport { Id = kRulebaseId, Rules = [new Rule { Id = 1 }] }],
                Devices = [GatewayWithLinks(new RulebaseLink { GatewayId = kGatewayId, NextRulebaseId = kRulebaseId, IsInitial = false })]
            };
            report.ReportData.ManagementData = [management];

            Assert.That(report.NoRuleFound(), Is.True);
        }

        [Test]
        public void AGatewayWhoseRulebasesAreEmptyHasNoRules()
        {
            ReportDevicesBase report = CreateReport();
            ManagementReport management = new()
            {
                Id = kManagementId,
                Rulebases = [new RulebaseReport { Id = kRulebaseId, Rules = [] }],
                Devices = [GatewayWithLinks(InitialLink(kRulebaseId), InitialLink(99))]
            };
            report.ReportData.ManagementData = [management];

            Assert.That(report.NoRuleFound(), Is.True);
        }

        [Test]
        public void AGatewayLinkedToARulebaseWithRulesHasRules()
        {
            ReportDevicesBase report = CreateReport();
            ManagementReport management = new()
            {
                Id = kManagementId,
                Rulebases = [new RulebaseReport { Id = kRulebaseId, Name = "rulebase", Rules = [new Rule { Id = 1 }] }],
                Devices = [GatewayWithLinks(InitialLink(kRulebaseId))]
            };
            report.ReportData.ManagementData = [new ManagementReport { Id = kOtherManagementId, Devices = [GatewayWithLinks()] }, management];

            Assert.That(report.NoRuleFound(), Is.False);
        }

        [Test]
        public void ChangesAreFoundWhenAnyManagementHasRuleChanges()
        {
            ReportDevicesBase report = CreateReport(ReportType.Changes);
            report.ReportData.ManagementData =
            [
                new ManagementReport { Id = kManagementId, RuleChanges = null },
                new ManagementReport { Id = kOtherManagementId, RuleChanges = [new RuleChange()] }
            ];

            Assert.That(report.NoChangesFound(), Is.False);
        }

        [Test]
        public void NoChangesAreFoundWhenNoManagementHasRuleChanges()
        {
            ReportDevicesBase report = CreateReport(ReportType.Changes);
            report.ReportData.ManagementData = [new ManagementReport { Id = kManagementId, RuleChanges = [] }];

            Assert.That(report.NoChangesFound(), Is.True);
        }

        [Test]
        public void TheDescriptionCountsTheReportedManagements()
        {
            // the statistics report uses the description of the device reports, rules reports have their own
            ReportDevicesBase report = CreateReport(ReportType.Statistics);
            report.ReportData.ManagementData =
            [
                new ManagementReport { Id = kManagementId },
                new ManagementReport { Id = kOtherManagementId },
                new ManagementReport { Id = 4, Ignore = true }
            ];

            Assert.That(report.SetDescription(), Is.EqualTo($"2 {userConfig.GetText("managements")}"));
        }
    }
}

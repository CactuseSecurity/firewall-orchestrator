using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ReportImportRangeTest
    {
        private const int kManagementId = 1;
        private const int kSubManagementId = 2;
        private const int kSuperManagerId = 3;
        private const int kNeverImportedId = 4;
        private const string kStart = "2026-11-01T00:00:00";
        private const string kEnd = "2026-11-02T00:00:00";

        [Test]
        public void ReportNotGeneratedHasNoImportRange()
        {
            Assert.That(BuildReport().GetImportRange(kManagementId), Is.Null);
            Assert.That(SimulatedReport.DetailedReport().GetImportRange(kManagementId), Is.Null);
        }

        [Test]
        public async Task RangeOfAPointInTimeIsTheRelevantImportIncludingSuperManagers()
        {
            ReportChanges report = BuildReport();

            List<ManagementReport> managements = await report.GetRelevantImportIds(new ImportApiConnection(), kStart);

            Assert.That(managements.Select(management => management.Id), Does.Not.Contain(kSuperManagerId));
            Assert.That(report.GetImportRange(kManagementId), Is.EqualTo(new ImportRange(10, 10)));
            Assert.That(report.GetImportRange(kSubManagementId), Is.EqualTo(new ImportRange(20, 20)), "a newer import of the super manager counts for its sub management");
            Assert.That(report.GetImportRange(kSuperManagerId), Is.EqualTo(new ImportRange(20, 20)));
            Assert.That(report.GetImportRange(kNeverImportedId), Is.EqualTo(new ImportRange(-1, -1)));
        }

        [Test]
        public async Task RangeOfAPeriodEndsWithItsLastImportIncludingSuperManagers()
        {
            ReportChanges report = BuildReport();
            ImportApiConnection api = new();

            await report.GetRelevantImportIds(api, kStart);
            await report.GetImportIdsInTimeRange(api, kStart, kEnd);

            Assert.That(report.GetImportRange(kManagementId), Is.EqualTo(new ImportRange(10, 10)), "no import in the period");
            Assert.That(report.GetImportRange(kSubManagementId), Is.EqualTo(new ImportRange(20, 31)), "imports of the super manager change the sub management too");
            Assert.That(report.GetImportRange(kSuperManagerId), Is.EqualTo(new ImportRange(20, 31)));
            Assert.That(report.GetImportRange(kNeverImportedId), Is.EqualTo(new ImportRange(-1, 33)), "first import in the period");
        }

        [Test]
        public async Task RangeOfAPreviousGenerationIsDiscarded()
        {
            ReportChanges report = BuildReport();
            ImportApiConnection api = new();
            await report.GetRelevantImportIds(api, kStart);
            await report.GetImportIdsInTimeRange(api, kStart, kEnd);

            api.OnlyFirstManagement = true;
            await report.GetRelevantImportIds(api, kStart);

            Assert.That(report.GetImportRange(kManagementId), Is.EqualTo(new ImportRange(10, 10)));
            Assert.That(report.GetImportRange(kSubManagementId), Is.Null);
        }

        private static ReportChanges BuildReport()
        {
            return new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Changes, new TimeFilter(), false);
        }

        private sealed class ImportApiConnection : SimulatedApiConnection
        {
            public bool OnlyFirstManagement { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                List<ManagementReport> managements = query == ReportQueries.getRelevantImportIdsAtTime ? ImportsAtTime() : ImportsInPeriod();
                return Task.FromResult((QueryResponseType)(object)managements);
            }

            private List<ManagementReport> ImportsAtTime()
            {
                List<ManagementReport> managements =
                [
                    BuildManagement(kManagementId, 10),
                    BuildManagement(kSubManagementId, 15),
                    BuildManagement(kSuperManagerId, 20),
                    BuildManagement(kNeverImportedId, null)
                ];
                managements[1].SuperManagerId = kSuperManagerId;
                managements[2].IsSuperManager = true;
                return OnlyFirstManagement ? managements[..1] : managements;
            }

            private static List<ManagementReport> ImportsInPeriod()
            {
                return
                [
                    new() { Id = kManagementId },
                    new() { Id = kSubManagementId, ImportControls = [new ImportControl { ControlId = 25 }] },
                    new() { Id = kSuperManagerId, SubManagements = [new Management { Id = kSubManagementId }], ImportControls = [new ImportControl { ControlId = 24 }, new ImportControl { ControlId = 31 }] },
                    new() { Id = kNeverImportedId, ImportControls = [new ImportControl { ControlId = 33 }] },
                    new() { Id = 99, ImportControls = [new ImportControl { ControlId = 34 }] }
                ];
            }

            private static ManagementReport BuildManagement(int id, long? relevantImportId)
            {
                ManagementReport management = new() { Id = id };
                management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = relevantImportId;
                return management;
            }
        }
    }
}

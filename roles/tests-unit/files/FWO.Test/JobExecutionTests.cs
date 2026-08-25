using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Jobs;
using FWO.Services;
using NUnit.Framework;
using Quartz;

namespace FWO.Test
{
    [TestFixture]
    internal class AutoDiscoverJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoManagementsAreAvailable()
        {
            EmptyListJobApiConnection apiConnection = new();
            AutoDiscoverJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
            Assert.That(apiConnection.Queries[0], Is.EqualTo(DeviceQueries.getManagementsDetails));
        }

        [Test]
        public async Task Execute_LogsErrorWhenManagementQueryFails()
        {
            ThrowingJobApiConnection apiConnection = new();
            AutoDiscoverJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.EqualTo(3));
            Assert.That(apiConnection.Queries[0], Is.EqualTo(DeviceQueries.getManagementsDetails));
            Assert.That(apiConnection.Queries[1], Is.EqualTo(MonitorQueries.addLogEntry));
            Assert.That(apiConnection.Queries[2], Is.EqualTo(MonitorQueries.getOpenAlerts));
        }
    }

    [TestFixture]
    internal class ComplianceJobTest
    {
        [Test]
        public async Task Execute_HandlesFailuresWithoutThrowing()
        {
            ThrowingJobApiConnection apiConnection = new();
            ComplianceJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.EqualTo(3));
            Assert.That(apiConnection.Queries[0], Is.EqualTo(ConfigQueries.getConfigItemsByUser));
            Assert.That(apiConnection.Queries[1], Is.EqualTo(MonitorQueries.addLogEntry));
            Assert.That(apiConnection.Queries[2], Is.EqualTo(MonitorQueries.getOpenAlerts));
        }
    }

    [TestFixture]
    internal class ImportAppDataJobTest
    {
        [Test]
        public async Task Execute_WithEmptyPathInitializesAndReturns()
        {
            AppDataImportNoOpApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportAppDataPath = "[]",
                ModNamingConvention = "{}",
                DnsLookup = false
            };
            ImportAppDataJob job = new(apiConnection, globalConfig);

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.getLdapConnections));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnerResponsibleTypes));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnerLifeCycleStates));
            Assert.That(apiConnection.Queries, Does.Contain(NotificationQueries.getNotifications));
        }

        [Test]
        public async Task Execute_WithMalformedImportPathDoesNotThrow()
        {
            ThrowingJobApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportAppDataPath = "{",
                ModNamingConvention = "{}",
                DnsLookup = false
            };
            ImportAppDataJob job = new(apiConnection, globalConfig);

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(MonitorQueries.addLogEntry));
        }
    }

    [TestFixture]
    internal class ExternalRequestJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoOpenRequestsExist()
        {
            ExternalRequestNoOpApiConnection apiConnection = new();
            ExternalRequestJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.EqualTo(3));
            Assert.That(apiConnection.Queries[0], Is.EqualTo(ConfigQueries.getConfigItemsByUser));
            Assert.That(apiConnection.Queries[1], Is.EqualTo(ConfigQueries.getCustomTextsPerLanguage));
            Assert.That(apiConnection.Queries[2], Is.EqualTo(ExtRequestQueries.getAndLockOpenRequests));
        }

        [Test]
        public void Execute_ThrowsJobExecutionExceptionWhenRequestHandlingFails()
        {
            ThrowingJobApiConnection apiConnection = new();
            ExternalRequestJob job = new(apiConnection, new SimulatedGlobalConfig());

            JobExecutionException? exception = Assert.ThrowsAsync<JobExecutionException>(async () => await job.Execute(null!));
            Assert.That(exception, Is.Not.Null);
            Assert.That(apiConnection.Queries, Has.Count.GreaterThanOrEqualTo(1));
        }
    }

    [TestFixture]
    internal class ImportChangeNotifyJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoImportsNeedNotification()
        {
            EmptyListJobApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ImpChangeIncludeObjectChanges = false
            };
            ImportChangeNotifyJob job = new(apiConnection, globalConfig);

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(apiConnection.Queries, Does.Contain(ConfigQueries.getConfigItemsByUser));
            Assert.That(apiConnection.Queries, Does.Contain(ReportQueries.getImportsToNotifyForRuleChanges));
        }

        [Test]
        public async Task Execute_HandlesFailuresWithoutThrowing()
        {
            ThrowingJobApiConnection apiConnection = new();
            ImportChangeNotifyJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(apiConnection.Queries, Does.Contain(MonitorQueries.addLogEntry));
            Assert.That(apiConnection.Queries, Does.Contain(MonitorQueries.getOpenAlerts));
        }
    }

    [TestFixture]
    internal class ImportIpDataJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoImportPathsAreConfigured()
        {
            EmptyListJobApiConnection apiConnection = new();
            ImportIpDataJob job = new(apiConnection, new SimulatedGlobalConfig
            {
                ImportSubnetDataPath = "[]"
            });

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
            Assert.That(apiConnection.Queries[0], Is.EqualTo(MonitorQueries.addDataImportLogEntry));
        }

        [Test]
        public async Task Execute_WithMalformedImportPathDoesNotThrow()
        {
            ThrowingJobApiConnection apiConnection = new();
            ImportIpDataJob job = new(apiConnection, new SimulatedGlobalConfig
            {
                ImportSubnetDataPath = "{"
            });

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(MonitorQueries.addLogEntry));
            Assert.That(apiConnection.Queries, Does.Contain(MonitorQueries.getOpenAlerts));
        }
    }

    [TestFixture]
    internal class VarianceAnalysisJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoOwnersAreAvailable()
        {
            EmptyListJobApiConnection apiConnection = new();
            VarianceAnalysisJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwners));
        }
    }

    [TestFixture]
    internal class UpdateRuleOwnerMappingJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoOwnerMappingSourceIsConfigured()
        {
            EmptyListJobApiConnection apiConnection = new();
            UpdateRuleOwnerMappingJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [Test]
        public async Task Execute_HandlesFailuresWithoutThrowing()
        {
            ThrowingJobApiConnection apiConnection = new();
            UpdateRuleOwnerMappingJob job = new(apiConnection, new SimulatedGlobalConfig
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.NameField
            });

            await job.Execute(null!);

            Assert.That(apiConnection.Queries, Has.Count.EqualTo(3));
            Assert.That(apiConnection.Queries[0], Is.EqualTo(ImportQueries.getPendingRuleOwnerImports));
            Assert.That(apiConnection.Queries[1], Is.EqualTo(MonitorQueries.addLogEntry));
            Assert.That(apiConnection.Queries[2], Is.EqualTo(MonitorQueries.getOpenAlerts));
        }
    }
}

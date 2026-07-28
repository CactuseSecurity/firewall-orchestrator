using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc;
using NetTools;
using System.Net;
using System.Threading;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceControllerTest
    {
        [Test]
        public async Task StartInitialComplianceCheck_ReturnsAcceptedAndMarksJobSucceeded()
        {
            ComplianceCheckStatusTracker tracker = new();
            ComplianceCheckController controller = new(new DummyApiConnection(), tracker);

            ActionResult<ComplianceCheckStartResult> result = controller.StartInitialComplianceCheck();

            Assert.That(result.Result, Is.InstanceOf<AcceptedResult>());
            ComplianceCheckStartResult startResult = (ComplianceCheckStartResult)((AcceptedResult)result.Result!).Value!;
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(5));
            ComplianceCheckJobStatus finalStatus = await tracker.WaitForTerminalStatusAsync(startResult.JobId, cancellationTokenSource.Token);
            Assert.That(finalStatus.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Succeeded));
            Assert.That(finalStatus.FinishedAt, Is.Not.Null);
        }

        [Test]
        public async Task StartInitialComplianceCheck_WhenBackgroundExecutionFails_MarksJobFailed()
        {
            ComplianceCheckStatusTracker tracker = new();
            ComplianceCheckController controller = new(new DummyApiConnection(throwOnViolationCount: true), tracker);

            ActionResult<ComplianceCheckStartResult> result = controller.StartInitialComplianceCheck();

            Assert.That(result.Result, Is.InstanceOf<AcceptedResult>());
            ComplianceCheckStartResult startResult = (ComplianceCheckStartResult)((AcceptedResult)result.Result!).Value!;
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(5));
            ComplianceCheckJobStatus finalStatus = await tracker.WaitForTerminalStatusAsync(startResult.JobId, cancellationTokenSource.Token);
            Assert.That(finalStatus.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Failed));
            Assert.That(finalStatus.Message, Does.Contain("Violation count query failed."));
            Assert.That(finalStatus.FinishedAt, Is.Not.Null);
        }

        [Test]
        public void GetInitialComplianceCheckStatus_ReturnsNotFoundForUnknownJob()
        {
            DummyApiConnection apiConnection = new();
            ComplianceCheckController controller = new(apiConnection, new ComplianceCheckStatusTracker());

            var result = controller.GetInitialComplianceCheckStatus("missing");

            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        }

        [Test]
        public void WaitForTerminalStatusAsync_ReturnsFaultedTaskForUnknownJob()
        {
            ComplianceCheckStatusTracker tracker = new();

            Task<ComplianceCheckJobStatus> result = tracker.WaitForTerminalStatusAsync("missing");

            Assert.ThrowsAsync<KeyNotFoundException>(async () => await result);
        }

        [Test]
        public async Task WaitForTerminalStatusAsync_RemovesCanceledWaiterAndAllowsLaterWaits()
        {
            ComplianceCheckStatusTracker tracker = new();
            ComplianceCheckJobStatus jobStatus = tracker.CreateQueuedJob();
            using CancellationTokenSource cancellationTokenSource = new();

            Task<ComplianceCheckJobStatus> canceledWait = tracker.WaitForTerminalStatusAsync(jobStatus.JobId, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await canceledWait);

            Task<ComplianceCheckJobStatus> terminalWait = tracker.WaitForTerminalStatusAsync(jobStatus.JobId);
            tracker.SetSucceeded(jobStatus.JobId);

            ComplianceCheckJobStatus terminalStatus = await terminalWait;
            Assert.That(terminalStatus.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Succeeded));
        }

        [Test]
        public void StartInitialComplianceCheck_ReturnsConflictWhenJobAlreadyActive()
        {
            ComplianceCheckStatusTracker tracker = new();
            tracker.CreateQueuedJob();
            DummyApiConnection apiConnection = new();
            ComplianceCheckController controller = new(apiConnection, tracker);

            var result = controller.StartInitialComplianceCheck();

            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());
        }

        [Test]
        public void GetInitialComplianceCheckStatus_ReturnsOkForKnownJob()
        {
            ComplianceCheckStatusTracker tracker = new();
            ComplianceCheckJobStatus jobStatus = tracker.CreateQueuedJob();
            ComplianceCheckController controller = new(new DummyApiConnection(), tracker);

            var result = controller.GetInitialComplianceCheckStatus(jobStatus.JobId);

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            ComplianceCheckJobStatus returnedJobStatus = (ComplianceCheckJobStatus)((OkObjectResult)result.Result!).Value!;
            Assert.That(returnedJobStatus.JobId, Is.EqualTo(jobStatus.JobId));
            Assert.That(returnedJobStatus.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Queued));
        }

        [Test]
        public async Task InitialComplianceCheck_ReturnsTrueWhenExecutionCompletes()
        {
            ComplianceCheckExecutionController controller = new(new DummyApiConnection());

            bool result = await controller.InitialComplianceCheck();

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task InitialComplianceCheck_ReturnsFalseWhenExecutionThrows()
        {
            ComplianceCheckExecutionController controller = new(new DummyApiConnection(throwOnViolationCount: true));

            bool result = await controller.InitialComplianceCheck();

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Post_ReturnsExceptionMessageWhenImportFails()
        {
            ComplianceController controller = new(new DummyApiConnection());

            string result = await controller.Post(new ComplianceImportMatrixParameters
            {
                FileName = "matrix.csv",
                Data = "name;value",
                UserName = "tester",
                UserDn = "cn=tester"
            });

            Assert.That(result, Is.Not.Empty);
        }

        [Test]
        public async Task Get_ReturnsEmptyStringWhenReportGenerationFails()
        {
            ComplianceController controller = new(new DummyApiConnection());

            string result = await controller.Get(new ComplianceReportParameters
            {
                ManagementIds = [1]
            });

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_ReturnsZonesForConfiguredMatrix()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
                [new ComplianceNetworkZone
                {
                    Id = 99,
                    Name = "DMZ",
                    Description = "Demilitarized zone",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255"))]
                }]);
            ComplianceZoneController controller = new(CreateZoneService(apiConnection, 12));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceDesignatedZoneResponse> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceDesignatedZoneResponse> ?? [];
            Assert.That(zones, Has.Count.EqualTo(1));
            Assert.That(zones[0].Id, Is.EqualTo(99));
            Assert.That(zones[0].Name, Is.EqualTo("DMZ"));
            Assert.That(zones[0].Description, Is.EqualTo("Demilitarized zone"));
            Assert.That(zones[0].IpRanges, Has.Count.EqualTo(1));
            Assert.That(zones[0].IpRanges[0].IpStart, Is.EqualTo("10.0.0.0"));
            Assert.That(zones[0].IpRanges[0].IpEnd, Is.EqualTo("10.0.0.255"));
            Assert.That(apiConnection.LastMatrixQuery, Is.EqualTo(ComplianceQueries.getMatrixById));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastMatrixQueryVariables, "criterionId"), Is.EqualTo(12));
            Assert.That(apiConnection.MatrixQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_PassesConfiguredMatrixIdToGraphQl()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
                [new ComplianceNetworkZone { Id = 99, Name = "DMZ" }]);
            ComplianceZoneController controller = new(CreateZoneService(apiConnection, 12));

            _ = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(apiConnection.LastMatrixQuery, Is.EqualTo(ComplianceQueries.getMatrixById));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastMatrixQueryVariables, "criterionId"), Is.EqualTo(12));
            Assert.That(apiConnection.LastNetworkZoneQuery, Is.EqualTo(ComplianceQueries.getNetworkZonesForMatrix));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastNetworkZoneQueryVariables, "criterionId"), Is.EqualTo(12));
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_ReturnsEmptyListWhenConfiguredMatrixWasDeleted()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [],
                [new ComplianceNetworkZone { Id = 99, Name = "DMZ" }]);
            ComplianceZoneController controller = new(CreateZoneService(apiConnection, 12));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceDesignatedZoneResponse> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceDesignatedZoneResponse> ?? [];
            Assert.That(zones, Is.Empty);
            Assert.That(apiConnection.LastMatrixQuery, Is.EqualTo(ComplianceQueries.getMatrixById));
            Assert.That(apiConnection.LastNetworkZoneQuery, Is.Null);
            Assert.That(apiConnection.MatrixQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.NetworkZoneQueryCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_ReturnsEmptyListWhenNoMatrixConfigured()
        {
            DummyApiConnection apiConnection = new();
            ComplianceZoneController controller = new(CreateZoneService(apiConnection));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceDesignatedZoneResponse> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceDesignatedZoneResponse> ?? [];
            Assert.That(zones, Is.Empty);
            Assert.That(apiConnection.LastNetworkZoneQuery, Is.Null);
            Assert.That(apiConnection.NetworkZoneQueryCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_ReturnsServerErrorWhenServiceFails()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
                [new ComplianceNetworkZone { Id = 99, Name = "DMZ" }],
                throwOnNetworkZoneQuery: true);
            ComplianceZoneController controller = new(CreateZoneService(apiConnection, 12));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)result.Result!).StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ResolveZonesForObjects_ReturnsZonesForNestedGroups()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
                [
                    new ComplianceNetworkZone
                    {
                        Id = 20,
                        Name = "Backend",
                        Description = "Backend zone",
                        IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                    },
                    new ComplianceNetworkZone
                    {
                        Id = 10,
                        Name = "DMZ",
                        Description = "Demilitarized zone",
                        IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.1.1"), IPAddress.Parse("10.0.1.1"))]
                    }
                ]);
            ComplianceZoneController controller = new(CreateZoneService(apiConnection, 12));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.ResolveZonesForObjects(new ResolveZonesForObjectsRequest
            {
                Objects =
                [
                    new ResolveZonesForObjectsRequest.GroupObjectRequest
                    {
                        Name = "Root Group",
                        Members =
                        [
                            new ResolveZonesForObjectsRequest.GroupObjectRequest
                            {
                                Name = "Sub Group",
                                Members =
                                [
                                    new ResolveZonesForObjectsRequest.LeafObjectRequest
                                    {
                                        Name = "Backend Host",
                                        Type = "host",
                                        IpStart = "10.0.0.1",
                                        IpEnd = "10.0.0.1"
                                    },
                                    new ResolveZonesForObjectsRequest.LeafObjectRequest
                                    {
                                        Name = "Backend Host Duplicate",
                                        Type = "network",
                                        IpStart = "10.0.0.1",
                                        IpEnd = "10.0.0.1"
                                    },
                                    new ResolveZonesForObjectsRequest.LeafObjectRequest
                                    {
                                        Name = "DMZ Host",
                                        Type = "ip_range",
                                        IpStart = "10.0.1.1",
                                        IpEnd = "10.0.1.1"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceDesignatedZoneResponse> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceDesignatedZoneResponse> ?? [];
            Assert.That(zones, Has.Count.EqualTo(2));
            Assert.That(zones.Select(zone => zone.Name), Is.EqualTo(["Backend", "DMZ"]));
            Assert.That(apiConnection.MatrixQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.NetworkZoneQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ResolveZonesForObjects_ReturnsServerErrorWhenServiceFails()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
                [new ComplianceNetworkZone { Id = 99, Name = "DMZ" }],
                throwOnNetworkZoneQuery: true);
            ComplianceZoneController controller = new(CreateZoneService(apiConnection, 12));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.ResolveZonesForObjects(new ResolveZonesForObjectsRequest
            {
                Objects =
                [
                    new ResolveZonesForObjectsRequest.LeafObjectRequest
                    {
                        Name = "Leaf",
                        Type = "network",
                        IpStart = "10.0.0.1",
                        IpEnd = "10.0.0.1"
                    }
                ]
            });

            Assert.That(result.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)result.Result!).StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ResolveZonesForObjects_ReturnsBadRequestWhenValidationFails()
        {
            ComplianceZoneController controller = new(CreateZoneService(new DummyApiConnection(), 12));

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.ResolveZonesForObjects(new ResolveZonesForObjectsRequest
            {
                Objects = []
            });

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("must contain at least one entry"));
        }

        private sealed class DummyApiConnection : ApiConnection
        {
            private readonly ConfigItem[] configItems;
            private readonly List<ComplianceCriterion> matrices;
            private readonly List<ComplianceNetworkZone> zones;
            private readonly Language[] languages =
            [
                new Language
                {
                    Name = GlobalConst.kEnglish,
                    CultureInfo = "en-US"
                }
            ];
            private readonly List<UiText> uiTexts =
            [
                new UiText
                {
                    Id = "internet_local_zone",
                    Txt = "Internet/Local",
                    Language = GlobalConst.kEnglish
                }
            ];

            public string? LastMatrixQuery { get; private set; }
            public object? LastMatrixQueryVariables { get; private set; }
            public int MatrixQueryCount { get; private set; }
            public string? LastNetworkZoneQuery { get; private set; }
            public object? LastNetworkZoneQueryVariables { get; private set; }
            public int NetworkZoneQueryCount { get; private set; }

            public DummyApiConnection(
                ConfigItem[]? configItems = null,
                List<ComplianceCriterion>? matrices = null,
                List<ComplianceNetworkZone>? zones = null,
                bool throwOnMatrixQuery = false,
                bool throwOnNetworkZoneQuery = false,
                bool throwOnViolationCount = false)
            {
                this.configItems = configItems ?? [];
                this.matrices = matrices ?? [];
                this.zones = zones ?? [];
                this.throwOnMatrixQuery = throwOnMatrixQuery;
                this.throwOnNetworkZoneQuery = throwOnNetworkZoneQuery;
                this.throwOnViolationCount = throwOnViolationCount;
            }

            private readonly bool throwOnMatrixQuery;
            private readonly bool throwOnNetworkZoneQuery;
            private readonly bool throwOnViolationCount;

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(Language[]) && query == ConfigQueries.getLanguages)
                {
                    return Task.FromResult((QueryResponseType)(object)languages);
                }

                if (typeof(QueryResponseType) == typeof(List<UiText>) &&
                    (query == ConfigQueries.getTextsPerLanguage || query == ConfigQueries.getCustomTextsPerLanguage))
                {
                    return Task.FromResult((QueryResponseType)(object)uiTexts);
                }

                if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)configItems);
                }

                if (typeof(QueryResponseType) == typeof(AggregateCount) && query == ComplianceQueries.getViolationCount)
                {
                    if (throwOnViolationCount)
                    {
                        throw new InvalidOperationException("Violation count query failed.");
                    }

                    return Task.FromResult((QueryResponseType)(object)new AggregateCount
                    {
                        Aggregate = new Aggregate { Count = 0 }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<ComplianceCriterion>) && query == ComplianceQueries.getMatrixById)
                {
                    if (throwOnMatrixQuery)
                    {
                        throw new InvalidOperationException("Matrix query failed.");
                    }

                    LastMatrixQuery = query;
                    LastMatrixQueryVariables = variables;
                    MatrixQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)matrices);
                }

                if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>) && query == ComplianceQueries.getNetworkZonesForMatrix)
                {
                    if (throwOnNetworkZoneQuery)
                    {
                        throw new InvalidOperationException("Network zone query failed.");
                    }

                    LastNetworkZoneQuery = query;
                    LastNetworkZoneQueryVariables = variables;
                    NetworkZoneQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)zones);
                }

                throw new NotImplementedException();
            }
            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            protected override void Dispose(bool disposing) { }
            public override void DisposeSubscriptions<T>() { }
            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => throw new NotImplementedException();
        }

        private static T GetAnonymousProperty<T>(object? obj, string propertyName)
        {
            Assert.That(obj, Is.Not.Null);
            object? value = obj!.GetType().GetProperty(propertyName)?.GetValue(obj);
            Assert.That(value, Is.Not.Null, $"Expected property '{propertyName}' to exist and have a value.");
            return (T)value!;
        }

        private static ComplianceZoneService CreateZoneService(ApiConnection apiConnection, int matrixId = 0)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDesignatedZoneMatrixId = matrixId
            };

            return new ComplianceZoneService(apiConnection, globalConfig);
        }

    }
}

using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Compliance.RulePositioning;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class DesignatedZoneMatrixReaderTest
{
    private const int kCriterionId = 12;
    private const string kSentinel = "reader-secret-sentinel";

    [Test]
    public async Task ReadAsync_LoadsMatrixThenZonesWithOneCapturedId()
    {
        SimulatedGlobalConfig config = new() { DesignatedZoneMatrixId = kCriterionId };
        RecordingApiConnection api = new(
            [new ComplianceCriterion { Id = kCriterionId, Name = "Matrix" }],
            [new ComplianceNetworkZone { Id = 21, CriterionId = kCriterionId, Name = "Zone" }]);
        api.AfterMatrixQuery = () => config.DesignatedZoneMatrixId = 99;

        DesignatedZoneMatrixReadSuccess success = Success(await new DesignatedZoneMatrixReader(api, config)
            .ReadAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(success.NetworkZones.Single().Id, Is.EqualTo(21));
            Assert.That(api.Operations, Is.EqualTo(["matrix", "zones"]));
            Assert.That(api.Criteria, Is.EqualTo([kCriterionId, kCriterionId]));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task ReadAsync_RejectsNonPositiveConfigurationWithoutApiCall(int criterionId)
    {
        RecordingApiConnection api = new([], []);

        DesignatedZoneMatrixReadFailure failure = Failure(await new DesignatedZoneMatrixReader(
            api,
            new SimulatedGlobalConfig { DesignatedZoneMatrixId = criterionId }).ReadAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid));
            Assert.That(api.Operations, Is.Empty);
        });
    }

    [Test]
    public async Task ReadAsync_ReturnsUnavailableForNoMatrixWithoutZoneQuery()
    {
        RecordingApiConnection api = new([], []);

        DesignatedZoneMatrixReadFailure failure = Failure(await Reader(api).ReadAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable));
            Assert.That(api.Operations, Is.EqualTo(["matrix"]));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ReadAsync_RejectsMultipleOrMismatchedMatricesWithoutZoneQuery(bool multiple)
    {
        List<ComplianceCriterion> matrices = multiple
            ? [new() { Id = kCriterionId }, new() { Id = kCriterionId }]
            : [new() { Id = kCriterionId + 1 }];
        RecordingApiConnection api = new(matrices, []);

        DesignatedZoneMatrixReadFailure failure = Failure(await Reader(api).ReadAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid));
            Assert.That(api.Operations, Is.EqualTo(["matrix"]));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ReadAsync_TreatsNullOrEmptyZonePayloadAsSuccessfulEmptyRead(bool nullPayload)
    {
        RecordingApiConnection api = new(
            [new ComplianceCriterion { Id = kCriterionId }],
            nullPayload ? null : []);

        DesignatedZoneMatrixReadSuccess success = Success(await Reader(api).ReadAsync(CancellationToken.None));

        Assert.That(success.NetworkZones, Is.Empty);
    }

    [TestCase("matrix")]
    [TestCase("zones")]
    public async Task ReadAsync_MapsQueryExceptionsWithoutSensitiveDiagnosticOrLaterWork(string operation)
    {
        RecordingApiConnection api = new(
            [new ComplianceCriterion { Id = kCriterionId, Name = "secret-matrix" }],
            [new ComplianceNetworkZone { Id = 1, Name = "secret-zone" }])
        {
            ThrowOperation = operation
        };

        DesignatedZoneMatrixReadFailure failure = Failure(await Reader(api).ReadAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.QueryFailed));
            Assert.That(failure.Diagnostic, Does.Not.Contain(kSentinel));
            Assert.That(failure.Diagnostic, Does.Not.Contain(ComplianceQueries.getMatrixById));
            Assert.That(failure.Diagnostic, Does.Not.Contain("secret-matrix"));
            Assert.That(failure.Diagnostic, Does.Not.Contain("secret-zone"));
            Assert.That(failure.Diagnostic, Does.Not.Contain("192.0.2.1"));
            Assert.That(api.Operations, Is.EqualTo(operation == "matrix" ? ["matrix"] : new[] { "matrix", "zones" }));
        });
    }

    [Test]
    public void ReadAsync_PropagatesPreCancellationWithoutApiCall()
    {
        RecordingApiConnection api = new([], []);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await Reader(api).ReadAsync(cancellation.Token));
        Assert.That(api.Operations, Is.Empty);
    }

    [TestCase("matrix")]
    [TestCase("zones")]
    public async Task ReadAsync_PropagatesCancellationWhileQueryAwaitsAndStops(string operation)
    {
        RecordingApiConnection api = new([new ComplianceCriterion { Id = kCriterionId }], [])
        {
            PendingOperation = operation
        };
        using CancellationTokenSource cancellation = new();
        Task<DesignatedZoneMatrixReadResult> read = Reader(api).ReadAsync(cancellation.Token);
        await api.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await read);
        Assert.That(api.Operations, Is.EqualTo(operation == "matrix" ? ["matrix"] : new[] { "matrix", "zones" }));
    }

    [Test]
    public async Task ReadAsync_CopiesZoneCollectionAndExposesReadOnlyInterface()
    {
        List<ComplianceNetworkZone> zones = [new() { Id = 1, CriterionId = kCriterionId }];
        DesignatedZoneMatrixReadSuccess success = Success(await Reader(new(
            [new ComplianceCriterion { Id = kCriterionId }], zones)).ReadAsync(CancellationToken.None));

        zones.Add(new ComplianceNetworkZone { Id = 2, CriterionId = kCriterionId });
        zones.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(success.NetworkZones, Has.Count.EqualTo(1));
            Assert.That(success.NetworkZones, Is.Not.InstanceOf<List<ComplianceNetworkZone>>());
            Assert.Throws<NotSupportedException>(() => ((IList<ComplianceNetworkZone>)success.NetworkZones)
                .Add(new ComplianceNetworkZone()));
        });
    }

    [Test]
    public void ResultContracts_RejectNullAndUseConservativeUnknownDiagnostics()
    {
        Assert.Throws<ArgumentNullException>(() => new DesignatedZoneMatrixReadSuccess(null!));
        DesignatedZoneMatrixReadFailure unknown = new((DesignatedZoneMatrixReadFailureCategory)999);

        Assert.That(unknown.Diagnostic, Is.EqualTo("The designated zone matrix could not be loaded."));
    }

    private static DesignatedZoneMatrixReader Reader(RecordingApiConnection api) =>
        new(api, new SimulatedGlobalConfig { DesignatedZoneMatrixId = kCriterionId });

    private static DesignatedZoneMatrixReadSuccess Success(DesignatedZoneMatrixReadResult result) =>
        (DesignatedZoneMatrixReadSuccess)result;

    private static DesignatedZoneMatrixReadFailure Failure(DesignatedZoneMatrixReadResult result) =>
        (DesignatedZoneMatrixReadFailure)result;

    private sealed class RecordingApiConnection(
        List<ComplianceCriterion>? matrices,
        List<ComplianceNetworkZone>? zones) : SimulatedApiConnection
    {
        public List<string> Operations { get; } = [];
        public List<int> Criteria { get; } = [];
        public Action? AfterMatrixQuery { get; set; }
        public string? ThrowOperation { get; init; }
        public string? PendingOperation { get; init; }
        public TaskCompletionSource QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<T> SendQueryAsync<T>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            string operation = query == ComplianceQueries.getMatrixById ? "matrix" :
                query == NetworkZoneQueries.getNetworkZonesForMatrix ? "zones" : "unknown";
            Operations.Add(operation);
            Criteria.Add((int)variables!.GetType().GetProperty("criterionId")!.GetValue(variables)!);
            if (ThrowOperation == operation)
            {
                throw new InvalidOperationException($"{kSentinel} 192.0.2.1");
            }
            if (PendingOperation == operation)
            {
                QueryStarted.TrySetResult();
                return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }
            if (operation == "matrix")
            {
                AfterMatrixQuery?.Invoke();
                return Task.FromResult((T)(object?)matrices!);
            }
            if (operation == "zones")
            {
                return Task.FromResult((T)(object?)zones!);
            }
            throw new NotImplementedException();
        }
    }
}

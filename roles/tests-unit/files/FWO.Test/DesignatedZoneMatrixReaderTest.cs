using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Compliance.RulePositioning;
using FWO.Data;
using FWO.Data.RulePositioning;
using NUnit.Framework;
using DesignatedZoneMatrixReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadValue,
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadFailureCategory>;

namespace FWO.Test;

[TestFixture]
internal class DesignatedZoneMatrixReaderTest
{
    private const int kCriterionId = 12;
    private const string kSentinel = "reader-secret-sentinel";
    private static readonly string[] kMatrixAndZoneOperations = ["matrix", "zones"];

    [Test]
    public async Task ReadAsync_LoadsMatrixThenZonesWithRequestedId()
    {
        RecordingApiConnection api = new(
            [new ComplianceCriterion { Id = kCriterionId, Name = "Matrix" }],
            [new NetworkZone { Id = 21, CriterionId = kCriterionId, Name = "Zone" }]);

        DesignatedZoneMatrixReadValue success = Success(await new DesignatedZoneMatrixReader(api)
            .ReadAsync(kCriterionId, CancellationToken.None));

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

        DesignatedZoneMatrixReadFailureCategory failure = Failure(await new DesignatedZoneMatrixReader(api)
            .ReadAsync(criterionId, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid));
            Assert.That(api.Operations, Is.Empty);
        });
    }

    [Test]
    public async Task ReadAsync_ReturnsUnavailableForNoMatrixWithoutZoneQuery()
    {
        RecordingApiConnection api = new([], []);

        DesignatedZoneMatrixReadFailureCategory failure = Failure(await Reader(api).ReadAsync(kCriterionId, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable));
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

        DesignatedZoneMatrixReadFailureCategory failure = Failure(await Reader(api).ReadAsync(kCriterionId, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid));
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

        DesignatedZoneMatrixReadValue success = Success(await Reader(api).ReadAsync(kCriterionId, CancellationToken.None));

        Assert.That(success.NetworkZones, Is.Empty);
    }

    [TestCase("matrix")]
    [TestCase("zones")]
    public async Task ReadAsync_MapsQueryExceptionsWithoutSensitiveDiagnosticOrLaterWork(string operation)
    {
        RecordingApiConnection api = new(
            [new ComplianceCriterion { Id = kCriterionId, Name = "secret-matrix" }],
            [new NetworkZone { Id = 1, Name = "secret-zone" }])
        {
            ThrowOperation = operation
        };

        DesignatedZoneMatrixReadFailureCategory failure = Failure(await Reader(api).ReadAsync(kCriterionId, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.EqualTo(DesignatedZoneMatrixReadFailureCategory.QueryFailed));
            Assert.That(failure.GetDiagnostic(), Does.Not.Contain(kSentinel));
            Assert.That(failure.GetDiagnostic(), Does.Not.Contain(ComplianceQueries.getMatrixById));
            Assert.That(failure.GetDiagnostic(), Does.Not.Contain("secret-matrix"));
            Assert.That(failure.GetDiagnostic(), Does.Not.Contain("secret-zone"));
            Assert.That(failure.GetDiagnostic(), Does.Not.Contain("192.0.2.1"));
            Assert.That(api.Operations, Is.EqualTo(operation == "matrix" ? ["matrix"] : kMatrixAndZoneOperations));
        });
    }

    [Test]
    public void ReadAsync_PropagatesPreCancellationWithoutApiCall()
    {
        RecordingApiConnection api = new([], []);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Reader(api).ReadAsync(kCriterionId, cancellation.Token));
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
        Task<DesignatedZoneMatrixReadResult> read = Reader(api).ReadAsync(kCriterionId, cancellation.Token);
        await api.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await read);
        Assert.That(api.Operations, Is.EqualTo(operation == "matrix" ? ["matrix"] : kMatrixAndZoneOperations));
    }

    [Test]
    public async Task ReadAsync_CopiesZoneCollectionAndExposesReadOnlyInterface()
    {
        List<NetworkZone> zones = [new() { Id = 1, CriterionId = kCriterionId }];
        DesignatedZoneMatrixReadValue success = Success(await Reader(new(
            [new ComplianceCriterion { Id = kCriterionId }], zones))
            .ReadAsync(kCriterionId, CancellationToken.None));

        zones.Add(new NetworkZone { Id = 2, CriterionId = kCriterionId });
        zones.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(success.NetworkZones, Has.Count.EqualTo(1));
            Assert.That(success.NetworkZones, Is.Not.InstanceOf<List<NetworkZone>>());
            Assert.Throws<NotSupportedException>(() => ((IList<NetworkZone>)success.NetworkZones)
                .Add(new NetworkZone()));
        });
    }

    [Test]
    public void ResultContracts_RejectNullAndUseConservativeUnknownDiagnostics()
    {
        Assert.Throws<ArgumentNullException>(() => new DesignatedZoneMatrixReadValue(null!));

        Assert.That(((DesignatedZoneMatrixReadFailureCategory)999).GetDiagnostic(),
            Is.EqualTo("The designated zone matrix could not be loaded."));
    }

    private static DesignatedZoneMatrixReader Reader(RecordingApiConnection api) =>
        new(api);

    private static DesignatedZoneMatrixReadValue Success(DesignatedZoneMatrixReadResult result)
    {
        Assert.That(result.TryGetValue(out DesignatedZoneMatrixReadValue? value), Is.True);
        return value!;
    }

    private static DesignatedZoneMatrixReadFailureCategory Failure(DesignatedZoneMatrixReadResult result)
    {
        Assert.That(result.TryGetFailure(out DesignatedZoneMatrixReadFailureCategory failure), Is.True);
        return failure;
    }

    private sealed class RecordingApiConnection(
        List<ComplianceCriterion>? matrices,
        List<NetworkZone>? zones) : SimulatedApiConnection
    {
        public List<string> Operations { get; } = [];
        public List<int> Criteria { get; } = [];
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

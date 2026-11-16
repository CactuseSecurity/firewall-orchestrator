using FWO.Config.Api;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning;
using FWO.Test.Mocks;

namespace FWO.Test;

/// <summary>Records workflow activation calls without loading positioning context.</summary>
internal sealed class RecordingRulePositioningWorkflowCaller : IRulePositioningWorkflowCaller
{
    public Func<WfReqTask?, WfImplTask?, bool> Applicability { get; init; } = (_, _) => true;
    public RulePositioningWorkflowOutcome Outcome { get; init; } = new(RulePositioningWorkflowStatus.Stored);
    public Exception? ExceptionToThrow { get; init; }
    public Func<CancellationToken, Task<RulePositioningWorkflowOutcome>>? Calculation { get; init; }
    public int ApplicabilityCount { get; private set; }
    public int CalculationCount { get; private set; }
    public WfReqTask? LastRequestTask { get; private set; }
    public WfImplTask? LastImplementationTask { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public List<(WfReqTask RequestTask, WfImplTask ImplementationTask)> Calls { get; } = [];

    /// <summary>Creates a recorder that reuses the production task-shape predicate.</summary>
    public static RecordingRulePositioningWorkflowCaller WithProductionApplicability()
    {
        SimulatedApiConnection apiConnection = new();
        RulePositioningWorkflowCaller productionCaller = new(
            apiConnection,
            new RulePositioningManager([]),
            new DeviceSpecificSettingsManager(apiConnection),
            new SimulatedGlobalConfig());
        return new() { Applicability = productionCaller.IsApplicable };
    }

    /// <inheritdoc />
    public bool IsApplicable(WfReqTask? requestTask, WfImplTask? implementationTask)
    {
        ApplicabilityCount++;
        return Applicability(requestTask, implementationTask);
    }

    /// <inheritdoc />
    public Task<RulePositioningWorkflowOutcome> CalculateAndPersistAsync(
        WfReqTask requestTask,
        WfImplTask implementationTask,
        CancellationToken cancellationToken)
    {
        CalculationCount++;
        LastRequestTask = requestTask;
        LastImplementationTask = implementationTask;
        LastCancellationToken = cancellationToken;
        Calls.Add((requestTask, implementationTask));
        if (Calculation != null)
        {
            return Calculation(cancellationToken);
        }
        return ExceptionToThrow == null
            ? Task.FromResult(Outcome)
            : Task.FromException<RulePositioningWorkflowOutcome>(ExceptionToThrow);
    }
}

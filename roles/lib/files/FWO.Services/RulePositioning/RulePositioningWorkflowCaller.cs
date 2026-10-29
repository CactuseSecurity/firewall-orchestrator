using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.RulePositioning;

/// <summary>Calculates and persists semantic positions without changing workflow state.</summary>
public sealed class RulePositioningWorkflowCaller : IRulePositioningWorkflowCaller
{
    private const string kLogTitle = "Rule positioning";

    private readonly ApiConnection apiConnection;
    private readonly RulePositioningManager positioningManager;
    private readonly DeviceSpecificSettingsManager settingsManager;

    /// <summary>Creates an independently callable rule-positioning service.</summary>
    public RulePositioningWorkflowCaller(
        ApiConnection apiConnection,
        RulePositioningManager positioningManager,
        DeviceSpecificSettingsManager settingsManager)
    {
        this.apiConnection = apiConnection ?? throw new ArgumentNullException(nameof(apiConnection));
        this.positioningManager = positioningManager ?? throw new ArgumentNullException(nameof(positioningManager));
        this.settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
    }

    /// <inheritdoc />
    public async Task<RulePositioningWorkflowOutcome> CalculateAndPersistAsync(
        WfReqTask requestTask,
        WfImplTask implementationTask,
        CancellationToken cancellationToken)
    {
        if (!IsApplicable(requestTask, implementationTask))
        {
            return LoggedOutcome(
                implementationTask?.Id ?? 0,
                RulePositioningWorkflowStatus.NotApplicable);
        }

        RulePositioningFailureCategory failureCategory;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            (Device Gateway, Management Management) target = await LoadTargetAsync(
                implementationTask.DeviceId!.Value,
                EffectiveManagementId(requestTask, implementationTask));
            cancellationToken.ThrowIfCancellationRequested();
            DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> settings =
                await settingsManager.LoadLevelAsync<GatewayDeviceSpecificSettings>(
                    DeviceSpecificScopePath.ForGateway(target.Management, target.Gateway));
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyCollection<WfReqTask>? ticketTasks = await LoadTicketTasksIfRequiredAsync(
                requestTask,
                target.Management.Id,
                settings.Settings.PositioningAlgorithm,
                cancellationToken);

            RulePositioningContext context = new(
                implementationTask,
                requestTask,
                target.Gateway,
                target.Management,
                settings.Settings,
                ticketTasks);
            RulePositioningResult calculation = await positioningManager.CalculateAsync(
                context,
                cancellationToken);
            if (calculation is RulePositioningSuccess success)
            {
                return await PersistAsync(
                    implementationTask.Id,
                    success.Position,
                    RulePositioningWorkflowStatus.Stored,
                    null,
                    cancellationToken);
            }

            failureCategory = calculation is RulePositioningFailure failure
                ? failure.Category
                : RulePositioningFailureCategory.CalculationFailed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            failureCategory = RulePositioningFailureCategory.CalculationFailed;
        }

        return await PersistAsync(
            implementationTask.Id,
            null,
            RulePositioningWorkflowStatus.ClearedAfterFailure,
            failureCategory,
            cancellationToken);
    }

    /// <summary>Loads and validates the management-specific ticket slice only for ticket-aware positioning.</summary>
    private async Task<IReadOnlyCollection<WfReqTask>?> LoadTicketTasksIfRequiredAsync(
        WfReqTask requestTask,
        int managementId,
        ProvisioningPositioningAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (algorithm != ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection)
        {
            return null;
        }
        if (requestTask.TicketId <= 0 || managementId <= 0)
        {
            throw new InvalidOperationException("The rule-positioning ticket context is invalid.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(
            RequestQueries.getTicketById,
            new { id = requestTask.TicketId }).WaitAsync(cancellationToken);
        if (ticket == null || ticket.Id != requestTask.TicketId || ticket.Tasks == null)
        {
            throw new InvalidOperationException("The rule-positioning ticket is unavailable or inconsistent.");
        }

        List<WfReqTask> tasks = [.. ticket.Tasks.Where(task =>
                task != null && task.Id > 0 && task.TaskNumber > 0 &&
                task.TicketId == ticket.Id && task.ManagementId == managementId)
            .OrderBy(task => task.TaskNumber).ThenBy(task => task.Id)];
        if (tasks.Count(task => task.Id == requestTask.Id) != 1)
        {
            throw new InvalidOperationException("The rule-positioning ticket does not contain the request task.");
        }
        return tasks.AsReadOnly();
    }

    /// <summary>Checks task shape and identity before loading any external context.</summary>
    public bool IsApplicable(WfReqTask? requestTask, WfImplTask? implementationTask)
    {
        if (requestTask == null
            || implementationTask == null
            || requestTask.Id <= 0
            || implementationTask.Id <= 0
            || implementationTask.ReqTaskId != requestTask.Id
            || implementationTask.DeviceId is null or <= 0
            || EffectiveManagementId(requestTask, implementationTask) <= 0)
        {
            return false;
        }

        string accessTaskType = WfTaskType.access.ToString();
        string createAction = RequestAction.create.ToString();
        if (requestTask.TaskType != accessTaskType
            || implementationTask.TaskType != accessTaskType
            || requestTask.RequestAction != createAction
            || implementationTask.ImplAction != createAction)
        {
            return false;
        }

        List<int> requestGatewayIds = [.. requestTask.GetDeviceList().Distinct()];
        bool hasExplicitFiniteGatewaySelection = requestGatewayIds.Count > 0
            && !requestGatewayIds.Contains(WfReqTaskBase.kAllDevicesId);
        return (!hasExplicitFiniteGatewaySelection
                || requestGatewayIds.Contains(implementationTask.DeviceId.Value))
            && (requestTask.OnManagement == null
                || requestTask.OnManagement.Id == requestTask.ManagementId);
    }

    /// <summary>Returns the implementation override or the request task management.</summary>
    private static int EffectiveManagementId(WfReqTask requestTask, WfImplTask implementationTask)
    {
        return implementationTask.ManagementId ?? requestTask.ManagementId ?? 0;
    }

    /// <summary>Loads exactly one gateway and validates its effective management.</summary>
    private async Task<(Device Gateway, Management Management)> LoadTargetAsync(
        int gatewayId,
        int effectiveManagementId)
    {
        List<Device> gateways = await apiConnection.SendQueryAsync<List<Device>>(
            DeviceQueries.getRulePositioningTarget,
            new { gatewayId });
        if (gateways.Count != 1
            || gateways[0].Id != gatewayId
            || gateways[0].Management == null
            || gateways[0].Management.Id != effectiveManagementId)
        {
            throw new InvalidOperationException("The rule-positioning target is unavailable or inconsistent.");
        }

        return (gateways[0], gateways[0].Management);
    }

    /// <summary>Stores or clears one position without constructing workflow persistence services.</summary>
    private async Task<RulePositioningWorkflowOutcome> PersistAsync(
        long implementationTaskId,
        RulePosition? position,
        RulePositioningWorkflowStatus successfulStatus,
        RulePositioningFailureCategory? failureCategory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RulePositioningWorkflowStatus status;
        try
        {
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(
                RequestQueries.updateImplementationTaskRulePosition,
                new { id = implementationTaskId, rulePosition = position });
            status = result.UpdatedIdLong == implementationTaskId
                ? successfulStatus
                : RulePositioningWorkflowStatus.PersistenceFailed;
        }
        catch (Exception)
        {
            status = RulePositioningWorkflowStatus.PersistenceFailed;
        }

        return LoggedOutcome(implementationTaskId, status, failureCategory);
    }

    /// <summary>Logs only identifiers and stable typed outcome data.</summary>
    private static RulePositioningWorkflowOutcome LoggedOutcome(
        long implementationTaskId,
        RulePositioningWorkflowStatus status,
        RulePositioningFailureCategory? failureCategory = null)
    {
        string category = failureCategory?.ToString() ?? "None";
        Log.WriteInfo(
            kLogTitle,
            $"Implementation task {implementationTaskId}: {status}; failure category: {category}.");
        return new RulePositioningWorkflowOutcome(status, failureCategory);
    }
}

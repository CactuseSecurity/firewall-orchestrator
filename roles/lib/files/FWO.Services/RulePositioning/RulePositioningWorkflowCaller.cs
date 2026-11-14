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
    private readonly GlobalConfig startupGlobalConfig;

    /// <summary>Creates an independently callable rule-positioning service.</summary>
    public RulePositioningWorkflowCaller(
        ApiConnection apiConnection,
        RulePositioningManager positioningManager,
        DeviceSpecificSettingsManager settingsManager,
        GlobalConfig startupGlobalConfig)
    {
        this.apiConnection = apiConnection ?? throw new ArgumentNullException(nameof(apiConnection));
        this.positioningManager = positioningManager ?? throw new ArgumentNullException(nameof(positioningManager));
        this.settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        this.startupGlobalConfig = startupGlobalConfig ?? throw new ArgumentNullException(nameof(startupGlobalConfig));
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
                requestTask,
                implementationTask);
            cancellationToken.ThrowIfCancellationRequested();
            DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> settings =
                await settingsManager.LoadLevelAsync<GatewayDeviceSpecificSettings>(
                    DeviceSpecificScopePath.ForGateway(target.Management, target.Gateway));
            cancellationToken.ThrowIfCancellationRequested();
            if (settings.Settings.PositioningAlgorithm ==
                    ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection &&
                requestTask.ManagementId is not > 0)
            {
                return LoggedOutcome(
                    implementationTask.Id,
                    RulePositioningWorkflowStatus.NotApplicable);
            }
            IReadOnlyCollection<WfReqTask>? ticketTasks = await LoadTicketTasksIfRequiredAsync(
                requestTask,
                settings.Settings.PositioningAlgorithm,
                cancellationToken);
            RulePositioningConfiguration? configuration = await LoadConfigurationIfRequiredAsync(
                settings.Settings.PositioningAlgorithm,
                cancellationToken);

            RulePositioningContext context = new(
                implementationTask,
                requestTask,
                target.Gateway,
                target.Management,
                settings.Settings,
                ticketTasks,
                configuration);
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

    /// <summary>Loads one current immutable global-settings snapshot for customer positioning.</summary>
    private async Task<RulePositioningConfiguration?> LoadConfigurationIfRequiredAsync(
        ProvisioningPositioningAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (algorithm != ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection)
        {
            return null;
        }

        using GlobalConfig currentConfig = await startupGlobalConfig
            .WithCurrentConfigItemsAsync(apiConnection)
            .WaitAsync(cancellationToken);
        return new RulePositioningConfiguration(
            currentConfig.DesignatedZoneMatrixId,
            currentConfig.AutoCalculateInternetZone,
            currentConfig.GetText("internet_local_zone"));
    }

    /// <summary>Loads and validates the request-management ticket slice only for ticket-aware positioning.</summary>
    private async Task<IReadOnlyCollection<WfReqTask>?> LoadTicketTasksIfRequiredAsync(
        WfReqTask requestTask,
        ProvisioningPositioningAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (algorithm != ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection)
        {
            return null;
        }
        int managementId = requestTask.ManagementId ?? 0;
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
                IsValidTicketTask(task, ticket.Id, managementId))
            .OrderBy(task => task.TaskNumber).ThenBy(task => task.Id)];
        if (tasks.Count(task => task.Id == requestTask.Id) != 1)
        {
            throw new InvalidOperationException("The rule-positioning ticket does not contain the request task.");
        }
        return tasks.AsReadOnly();
    }

    /// <summary>Checks one ticket task against the requested ticket and management slice.</summary>
    private static bool IsValidTicketTask(WfReqTask? task, long ticketId, int managementId) =>
        task != null && task.Id > 0 && task.TaskNumber > 0 &&
        task.TicketId == ticketId && task.ManagementId == managementId;

    /// <summary>Checks task shape and identity before loading any external context.</summary>
    public bool IsApplicable(WfReqTask? requestTask, WfImplTask? implementationTask)
    {
        return RulePositioningContextValidator.IsApplicable(requestTask, implementationTask);
    }

    /// <summary>Loads exactly one gateway and resolves its canonical target management.</summary>
    private async Task<(Device Gateway, Management Management)> LoadTargetAsync(
        WfReqTask requestTask,
        WfImplTask implementationTask)
    {
        int gatewayId = implementationTask.DeviceId!.Value;
        List<Device> gateways = await apiConnection.SendQueryAsync<List<Device>>(
            DeviceQueries.getImplementationTaskGatewayTarget,
            new { gatewayId });
        if (gateways.Count != 1 ||
            !WfImplTaskTargetManagementResolver.TryResolveManagementId(
                requestTask,
                implementationTask,
                gateways[0],
                out int managementId) ||
            gateways[0].Management.Id != managementId)
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

using System.Diagnostics;
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
        using IDisposable attempt = RulePositioningDiagnosticLog.BeginAttempt(
            implementationTask?.Id ?? 0,
            requestTask?.Id ?? 0,
            requestTask?.TicketId ?? 0,
            implementationTask?.DeviceId ?? 0);
        bool applicable = IsApplicable(requestTask, implementationTask);
        RulePositioningDiagnosticLog.WriteInfo(
            "workflow.applicability",
            "summary",
            Facts(
                ("request_task_present", requestTask != null),
                ("implementation_task_present", implementationTask != null),
                ("request_task_id_valid", requestTask?.Id > 0),
                ("implementation_task_id_valid", implementationTask?.Id > 0),
                ("gateway_id_present", implementationTask?.DeviceId > 0),
                ("applicable", applicable)));
        if (!applicable)
        {
            return LoggedOutcome(
                implementationTask?.Id ?? 0,
                RulePositioningWorkflowStatus.NotApplicable);
        }
        WfReqTask applicableRequestTask = requestTask!;
        WfImplTask applicableImplementationTask = implementationTask!;
        return await CalculateApplicableAndPersistAsync(
            applicableRequestTask,
            applicableImplementationTask,
            cancellationToken);
    }

    /// <summary>Calculates and persists one already-applicable workflow task.</summary>
    private async Task<RulePositioningWorkflowOutcome> CalculateApplicableAndPersistAsync(
        WfReqTask applicableRequestTask,
        WfImplTask applicableImplementationTask,
        CancellationToken cancellationToken)
    {
        RulePositioningFailureCategory failureCategory;
        string currentStage = "workflow.start";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentStage = "target.query";
            (Device Gateway, Management Management) target = await LoadTargetAsync(
                applicableRequestTask,
                applicableImplementationTask);
            cancellationToken.ThrowIfCancellationRequested();
            currentStage = "settings.load";
            long settingsStart = Stopwatch.GetTimestamp();
            DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> settings =
                await settingsManager.LoadLevelAsync<GatewayDeviceSpecificSettings>(
                    DeviceSpecificScopePath.ForGateway(target.Management, target.Gateway));
            RulePositioningDiagnosticLog.WriteInfo(
                currentStage,
                "success",
                Facts(
                    ("duration_ms", ElapsedMilliseconds(settingsStart)),
                    ("management_id", target.Management.Id),
                    ("gateway_id", target.Gateway.Id),
                    ("device_type_id", target.Gateway.DeviceType?.Id ?? 0),
                    ("algorithm", settings.Settings.PositioningAlgorithm.ToString())));
            cancellationToken.ThrowIfCancellationRequested();
            currentStage = "ticket.load";
            IReadOnlyCollection<WfReqTask>? ticketTasks = await LoadTicketTasksIfRequiredAsync(
                applicableRequestTask,
                settings.Settings.PositioningAlgorithm,
                cancellationToken);
            currentStage = "configuration.load";
            RulePositioningConfiguration? configuration = await LoadConfigurationIfRequiredAsync(
                settings.Settings.PositioningAlgorithm,
                cancellationToken);

            currentStage = "context.build";
            RulePositioningContext context = new(
                applicableImplementationTask,
                applicableRequestTask,
                target.Gateway,
                target.Management,
                settings.Settings,
                ticketTasks,
                configuration);
            RulePositioningDiagnosticLog.WriteInfo(
                currentStage,
                "success",
                Facts(
                    ("ticket_task_count", ticketTasks?.Count ?? 0),
                    ("configuration_present", configuration != null)));
            currentStage = "manager.calculate";
            long calculationStart = Stopwatch.GetTimestamp();
            RulePositioningResult calculation = await positioningManager.CalculateAsync(
                context,
                cancellationToken);
            RulePositioningDiagnosticLog.WriteInfo(
                currentStage,
                calculation is RulePositioningSuccess ? "success" : "failure",
                Facts(
                    ("duration_ms", ElapsedMilliseconds(calculationStart)),
                    ("result_type", calculation?.GetType().FullName ?? "null"),
                    ("failure_category", (calculation as RulePositioningFailure)?.Category.ToString() ?? "None")));
            if (calculation is RulePositioningSuccess success)
            {
                return await PersistAsync(
                    applicableImplementationTask.Id,
                    success.Position,
                    RulePositioningWorkflowStatus.Stored,
                    null,
                    cancellationToken);
            }

            failureCategory = calculation is RulePositioningFailure failure
                ? failure.Category
                : RulePositioningFailureCategory.CalculationFailed;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            RulePositioningDiagnosticLog.WriteException(currentStage, exception);
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException(currentStage, exception);
            failureCategory = RulePositioningFailureCategory.CalculationFailed;
        }

        return await PersistAsync(
            applicableImplementationTask.Id,
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
            RulePositioningDiagnosticLog.WriteInfo(
                "configuration.load",
                "skipped",
                Facts(("algorithm", algorithm.ToString())));
            return null;
        }

        long started = Stopwatch.GetTimestamp();
        using GlobalConfig currentConfig = await startupGlobalConfig
            .WithCurrentConfigItemsAsync(apiConnection)
            .WaitAsync(cancellationToken);
        RulePositioningConfiguration configuration = new(
            currentConfig.DesignatedZoneMatrixId,
            currentConfig.AutoCalculateInternetZone,
            currentConfig.GetText("internet_local_zone"));
        RulePositioningDiagnosticLog.WriteInfo(
            "configuration.load",
            "success",
            Facts(
                ("duration_ms", ElapsedMilliseconds(started)),
                ("designated_zone_matrix_id", configuration.DesignatedZoneMatrixId),
                ("auto_calculate_internet_zone", configuration.AutoCalculateInternetZone),
                ("internet_zone", RulePositioningDiagnosticLog.Fingerprint(configuration.InternetLocalZoneName))));
        return configuration;
    }

    /// <summary>Loads and validates the request-management ticket slice only for ticket-aware positioning.</summary>
    private async Task<IReadOnlyCollection<WfReqTask>?> LoadTicketTasksIfRequiredAsync(
        WfReqTask requestTask,
        ProvisioningPositioningAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (algorithm != ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection)
        {
            RulePositioningDiagnosticLog.WriteInfo(
                "ticket.load",
                "skipped",
                Facts(("algorithm", algorithm.ToString())));
            return null;
        }
        int managementId = requestTask.ManagementId ?? 0;
        if (requestTask.TicketId <= 0 || managementId <= 0)
        {
            RulePositioningDiagnosticLog.WriteFailure(
                "ticket.load",
                Facts(
                    ("invariant", "invalid_ticket_scope"),
                    ("ticket_id", requestTask.TicketId),
                    ("management_id", managementId)));
            throw new InvalidOperationException("The rule-positioning ticket context is invalid.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(
            RequestQueries.getTicketById,
            new { id = requestTask.TicketId }).WaitAsync(cancellationToken);
        if (ticket == null || ticket.Id != requestTask.TicketId || ticket.Tasks == null)
        {
            RulePositioningDiagnosticLog.WriteFailure(
                "ticket.load",
                Facts(
                    ("invariant", "ticket_result_inconsistent"),
                    ("duration_ms", ElapsedMilliseconds(started)),
                    ("ticket_present", ticket != null),
                    ("returned_ticket_id", ticket?.Id ?? 0),
                    ("tasks_present", ticket?.Tasks != null)));
            throw new InvalidOperationException("The rule-positioning ticket is unavailable or inconsistent.");
        }

        List<WfReqTask> tasks = [.. ticket.Tasks.Where(task =>
                IsValidTicketTask(task, ticket.Id, managementId))
            .OrderBy(task => task.TaskNumber).ThenBy(task => task.Id)];
        if (tasks.Count(task => task.Id == requestTask.Id) != 1)
        {
            RulePositioningDiagnosticLog.WriteFailure(
                "ticket.load",
                TicketFacts(ticket.Tasks, tasks, requestTask, managementId, started, "parent_task_cardinality"));
            throw new InvalidOperationException("The rule-positioning ticket does not contain the request task.");
        }
        RulePositioningDiagnosticLog.WriteInfo(
            "ticket.load",
            "success",
            TicketFacts(ticket.Tasks, tasks, requestTask, managementId, started, "valid"));
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
        long started = Stopwatch.GetTimestamp();
        List<Device> gateways = await apiConnection.SendQueryAsync<List<Device>>(
            DeviceQueries.getImplementationTaskGatewayTarget,
            new { gatewayId });
        Device? returnedGateway = gateways.Count == 1 ? gateways[0] : null;
        int managementId = 0;
        bool managementResolved = returnedGateway != null &&
            WfImplTaskTargetManagementResolver.TryResolveManagementId(
                requestTask,
                implementationTask,
                returnedGateway,
                out managementId);
        bool gatewayMatches = returnedGateway?.Id == gatewayId;
        bool managementMatches = managementResolved && returnedGateway?.Management?.Id == managementId;
        Dictionary<string, object?> facts = Facts(
            ("duration_ms", ElapsedMilliseconds(started)),
            ("requested_gateway_id", gatewayId),
            ("result_count", gateways.Count),
            ("returned_gateway_id", returnedGateway?.Id ?? 0),
            ("returned_management_id", returnedGateway?.Management?.Id ?? 0),
            ("implementation_management_id", implementationTask.ManagementId ?? 0),
            ("request_management_id", requestTask.ManagementId ?? 0),
            ("management_resolved", managementResolved),
            ("resolved_management_id", managementResolved ? managementId : 0),
            ("gateway_matches", gatewayMatches),
            ("management_matches", managementMatches));
        RulePositioningDiagnosticLog.WriteInfo("target.query", "success", facts);
        if (returnedGateway == null || !managementResolved || !gatewayMatches || !managementMatches)
        {
            RulePositioningDiagnosticLog.WriteFailure("target.validation", facts);
            throw new InvalidOperationException("The rule-positioning target is unavailable or inconsistent.");
        }

        RulePositioningDiagnosticLog.WriteInfo("target.validation", "success", facts);
        return (returnedGateway, returnedGateway.Management);
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
            long started = Stopwatch.GetTimestamp();
            RulePositioningDiagnosticLog.WriteInfo(
                "persistence.write",
                "start",
                Facts(
                    ("implementation_task_id", implementationTaskId),
                    ("position_present", position != null)));
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(
                RequestQueries.updateImplementationTaskRulePosition,
                new { id = implementationTaskId, rulePosition = position });
            status = result.UpdatedIdLong == implementationTaskId
                ? successfulStatus
                : RulePositioningWorkflowStatus.PersistenceFailed;
            RulePositioningDiagnosticLog.WriteInfo(
                "persistence.write",
                status == RulePositioningWorkflowStatus.PersistenceFailed ? "failure" : "success",
                Facts(
                    ("duration_ms", ElapsedMilliseconds(started)),
                    ("requested_task_id", implementationTaskId),
                    ("returned_id", result.UpdatedIdLong),
                    ("status", status.ToString())));
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException(
                "persistence.write",
                exception,
                Facts(("requested_task_id", implementationTaskId)));
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
        RulePositioningDiagnosticLog.WriteInfo(
            "workflow.final",
            status == RulePositioningWorkflowStatus.Stored ? "success"
                : status == RulePositioningWorkflowStatus.NotApplicable ? "skipped"
                : "failure",
            Facts(
                ("status", status.ToString()),
                ("failure_category", category)));
        Log.WriteInfo(
            kLogTitle,
            $"Implementation task {implementationTaskId}: {status}; failure category: {category}.");
        return new RulePositioningWorkflowOutcome(status, failureCategory);
    }

    /// <summary>Builds bounded ticket-load counts without retaining task text or elements.</summary>
    private static Dictionary<string, object?> TicketFacts(
        IReadOnlyCollection<WfReqTask> returnedTasks,
        IReadOnlyCollection<WfReqTask> validTasks,
        WfReqTask requestTask,
        int managementId,
        long started,
        string invariant)
    {
        return Facts(
            ("invariant", invariant),
            ("duration_ms", ElapsedMilliseconds(started)),
            ("returned_task_count", returnedTasks.Count),
            ("valid_slice_count", validTasks.Count),
            ("parent_match_count", validTasks.Count(Task => Task.Id == requestTask.Id)),
            ("invalid_identity_count", returnedTasks.Count(Task => Task == null || Task.Id <= 0 || Task.TaskNumber <= 0)),
            ("wrong_ticket_count", returnedTasks.Count(Task => Task != null && Task.TicketId != requestTask.TicketId)),
            ("wrong_management_count", returnedTasks.Count(Task => Task != null && Task.ManagementId != managementId)));
    }

    /// <summary>Returns elapsed whole milliseconds from a timestamp captured by Stopwatch.</summary>
    private static long ElapsedMilliseconds(long started) =>
        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    /// <summary>Builds one event's bounded fact map.</summary>
    private static Dictionary<string, object?> Facts(params (string Key, object? Value)[] values) =>
        values.ToDictionary(Value => Value.Key, Value => Value.Value);
}

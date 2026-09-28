using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Provides workflow ticket data for workflow REST endpoints.
/// </summary>
public sealed class WorkflowTicketService : IDisposable
{
    private readonly ApiConnection apiConnection;
    private readonly GlobalConfig globalConfig;
    private readonly WorkflowTicketFlowReferenceService flowReferenceService;
    private readonly WorkflowTicketBuilder ticketBuilder;
    private readonly ApiSubscription? configSubscription;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public WorkflowTicketService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.apiConnection = apiConnection;
        this.globalConfig = globalConfig;
        flowReferenceService = new WorkflowTicketFlowReferenceService(apiConnection);
        ticketBuilder = new WorkflowTicketBuilder(globalConfig);
        try
        {
            configSubscription = this.apiConnection.GetSubscription<ConfigItem[]>(
                GraphqlExceptionHandler.Handle,
                OnGlobalConfigChange,
                ConfigQueries.subscribeFlowRequestConfigChanges);
        }
        catch (Exception exception)
        {
            Log.WriteError("Flow request config", "Could not start flow-request config subscription.", exception);
        }
    }

    /// <summary>
    /// Applies refreshed request-flow config values to the shared config snapshot.
    /// </summary>
    private void OnGlobalConfigChange(ConfigItem[] configItems)
    {
        globalConfig.MergeSubscriptionUpdateHandler(configItems);
    }

    /// <summary>
    /// Creates a new workflow ticket from the high-level request payload.
    /// </summary>
    /// <param name="request">The high-level request payload.</param>
    /// <param name="requesterId">Database id of the authenticated caller.</param>
    /// <param name="callerName">Login name of the authenticated caller, recorded as changer in the change history.</param>
    /// <param name="aggregateValidationErrors">Whether semantic validation errors should be returned as an aggregate.</param>
    public async Task<CreateTicketResponse> CreateTicketAsync(CreateTicketRequest request, int requesterId, string? callerName = null,
        bool aggregateValidationErrors = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateTicket(request);
        if (requesterId <= 0)
        {
            throw new ArgumentException("'requesterId' must be a positive integer.");
        }

        (WorkflowPhases ticketPhase, int ticketStateId) = await ResolveInitialRequestPhaseAndStateAsync();
        Dictionary<int, FwoOwner> ownersById = await ResolveOwnersAsync();
        Dictionary<string, int> ruleActionIds = await ResolveRuleActionIdsAsync();
        Dictionary<string, int> protocolIds = await ResolveProtocolIdsAsync();
        FlowReferenceCatalog flowReferences = await flowReferenceService.ResolveAsync(request);
        WfTicket ticket;
        try
        {
            ticket = ticketBuilder.Build(request, ticketStateId, requesterId, ownersById, ruleActionIds, protocolIds, flowReferences);
        }
        catch (CreateTicketValidationException exception) when (!aggregateValidationErrors)
        {
            throw new ArgumentException(exception.Errors.Errors[0].Message, exception);
        }
        // requesterId is the id of the authenticated caller, so it is also the changer - but only when that
        // caller is named. An internal caller supplies a requester without being the user who made the change,
        // and attributing the change history entry to that requester would be wrong.
        int? changerId = string.IsNullOrWhiteSpace(callerName) ? null : requesterId;
        ticket = await SaveTicketAsync(ticket, ticketPhase, callerName, changerId);
        string status = await BuildTicketStatusAsync(ticket.StateId, tolerateExternalStateErrors: true);

        return new CreateTicketResponse
        {
            Status = status,
            TicketId = ticket.Id
        };
    }

    /// <summary>
    /// Returns the workflow ticket status and latest ticket comment.
    /// </summary>
    public async Task<GetTicketStatusResponse?> GetTicketStatusAsync(long ticketId)
    {
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });
        if (ticket == null)
        {
            return null;
        }

        return new GetTicketStatusResponse
        {
            Status = await BuildTicketStatusAsync(ticket.StateId, tolerateExternalStateErrors: false),
            StatusComment = GetLatestTicketComment(ticket)
        };
    }

    /// <summary>
    /// Returns a workflow ticket with all of its tasks, approvals, implementation tasks, elements,
    /// owners and comments.
    /// </summary>
    /// <param name="ticketId">Database id of the workflow ticket.</param>
    /// <param name="filter">Optional request task filter; null returns every task.</param>
    /// <returns>The ticket, or null when no workflow ticket with that id exists.</returns>
    /// <remarks>
    /// The filter restricts the returned tasks only: a ticket whose tasks the filter excludes is
    /// still returned, with an empty task list, so it cannot be mistaken for a missing ticket.
    /// </remarks>
    public async Task<GetTicketResponse?> GetTicketAsync(long ticketId, TicketTaskFilter? filter)
    {
        WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticketId });
        if (ticket == null)
        {
            return null;
        }

        WfStateDict states = await GetStateDictAsync();
        string status = await BuildTicketStatusAsync(ticket.StateId, states, tolerateExternalStateErrors: false);
        return TicketResponseMapper.Map(ticket, states, status, filter);
    }

    /// <summary>
    /// Validates the create-ticket payload before the ticket is built.
    /// </summary>
    private static void ValidateCreateTicket(CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestorName))
        {
            throw new ArgumentException("'requestorName' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.RequestorId))
        {
            throw new ArgumentException("'requestorId' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.RuleContactName))
        {
            throw new ArgumentException("'ruleContactName' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.RuleContactId))
        {
            throw new ArgumentException("'ruleContactId' must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("'title' must not be empty.");
        }
        if (request.Rules.Count == 0)
        {
            throw new ArgumentException("At least one rule is required.");
        }
    }

    /// <summary>
    /// Resolves the initial workflow phase and state id for a newly created request ticket.
    /// </summary>
    private async Task<(WorkflowPhases Phase, int StateId)> ResolveInitialRequestPhaseAndStateAsync()
    {
        List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates) ?? [];
        StateMatrixConfigurationSnapshot stateMatrix = await StateMatrixConfigurationRepository.Load(apiConnection, WfTaskType.master);
        int configuredStateId = globalConfig.ReqApiTicketInitialStateId;
        if (configuredStateId >= 0)
        {
            if (states.Any(state => state.Id == configuredStateId))
            {
                List<WorkflowPhases> matchingPhases = StateMatrixConfigurationRepository.GetMatchingActiveWorkflowPhases(stateMatrix, configuredStateId);
                if (matchingPhases.Count == 1)
                {
                    return (matchingPhases[0], configuredStateId);
                }

                if (matchingPhases.Count > 1)
                {
                    throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} matches multiple active workflow phases: {string.Join(", ", matchingPhases)}.");
                }

                throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} does not belong to any active workflow phase.");
            }

            throw new InvalidOperationException($"Configured API ticket state id {configuredStateId} does not exist in the current state list.");
        }

        WorkflowPhases phase = ResolveInitialWorkflowPhase(stateMatrix);
        return (phase, stateMatrix.Matrices[phase].LowestInputState);
    }

    /// <summary>
    /// Resolves the first active workflow phase starting at request.
    /// </summary>
    private static WorkflowPhases ResolveInitialWorkflowPhase(StateMatrixConfigurationSnapshot stateMatrix)
    {
        foreach (WorkflowPhases phase in Enum.GetValues<WorkflowPhases>())
        {
            if (stateMatrix.Matrices.TryGetValue(phase, out StateMatrix? matrix) && matrix.Active)
            {
                return phase;
            }
        }

        throw new InvalidOperationException("No active workflow phase is configured for request creation.");
    }

    /// <summary>
    /// Resolves the available STM rule actions by name.
    /// </summary>
    private async Task<Dictionary<string, int>> ResolveRuleActionIdsAsync()
    {
        List<RuleAction> ruleActions = await apiConnection.SendQueryAsync<List<RuleAction>>(StmQueries.getRuleActions) ?? [];
        return ruleActions
            .Where(ruleAction => !string.IsNullOrWhiteSpace(ruleAction.Name))
            .GroupBy(ruleAction => ruleAction.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the owners visible to the middleware role.
    /// </summary>
    private async Task<Dictionary<int, FwoOwner>> ResolveOwnersAsync()
    {
        List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners) ?? [];
        return owners
            .Where(owner => owner.Id > 0)
            .GroupBy(owner => owner.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    /// <summary>
    /// Resolves the available STM IP protocol ids by name.
    /// </summary>
    private async Task<Dictionary<string, int>> ResolveProtocolIdsAsync()
    {
        List<IpProtocol> protocols = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols) ?? [];
        return protocols
            .Where(protocol => !string.IsNullOrWhiteSpace(protocol.Name))
            .GroupBy(protocol => protocol.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Persists the created ticket through the workflow save path so request actions are executed consistently.
    /// </summary>
    /// <param name="ticket">Ticket to persist.</param>
    /// <param name="phase">Workflow phase the ticket is created in.</param>
    /// <param name="callerName">Login name of the authenticated caller, empty for unauthenticated internal callers.</param>
    /// <param name="changerId">Database id of the authenticated caller, null for unauthenticated internal callers.</param>
    private async Task<WfTicket> SaveTicketAsync(WfTicket ticket, WorkflowPhases phase, string? callerName, int? changerId)
    {
        using UserConfig userConfig = CreateWorkflowUserConfig(callerName);
        WfHandler wfHandler = new(userConfig, apiConnection, phase, (List<UserGroup>?)null) { SystemContext = true, ChangerId = changerId };
        if (!await wfHandler.InitForActionExecution() || wfHandler.ActionHandler == null)
        {
            throw new InvalidOperationException($"Could not initialize workflow actions for request ticket creation in phase {phase}.");
        }

        WfDbAccess dbAccess = new((_, _, _, _) => { }, userConfig, apiConnection, wfHandler.ActionHandler, true, phase, false) { ChangerId = changerId };

        WfTicket createdTicket = await dbAccess.AddTicketToDb(ticket);

        long ticketId = createdTicket.Id;
        if (ticketId <= 0)
        {
            throw new InvalidOperationException("Could not create the request ticket.");
        }

        return createdTicket;
    }

    /// <summary>
    /// Builds the workflow config used to save a ticket. It carries the global settings like every other
    /// middleware entry point, plus the login name of the authenticated caller so the change history names
    /// that caller instead of the middleware server. It is created per request because the name differs
    /// between concurrent callers.
    /// </summary>
    /// <param name="callerName">Login name of the authenticated caller, empty for unauthenticated internal callers.</param>
    private UserConfig CreateWorkflowUserConfig(string? callerName)
    {
        UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, globalConfig.DefaultLanguage);
        userConfig.User.Name = callerName ?? "";
        return userConfig;
    }

    /// <summary>
    /// Loads workflow state names.
    /// </summary>
    private async Task<WfStateDict> GetStateDictAsync()
    {
        WfStateDict loadedStateDict = new();
        await loadedStateDict.Init(apiConnection);
        return loadedStateDict;
    }

    /// <summary>
    /// Builds the public status string for a workflow request.
    /// </summary>
    private async Task<string> BuildTicketStatusAsync(int stateId, bool tolerateExternalStateErrors)
    {
        return await BuildTicketStatusAsync(stateId, await GetStateDictAsync(), tolerateExternalStateErrors);
    }

    /// <summary>
    /// Builds the public status string for a workflow request from already loaded state names.
    /// </summary>
    private async Task<string> BuildTicketStatusAsync(int stateId, WfStateDict states, bool tolerateExternalStateErrors)
    {
        string status = states.GetName(stateId);
        ApiResponse<List<WfExtState>> extStateResponse = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(RequestQueries.getExtStates);
        if (extStateResponse.HasErrors || extStateResponse.Result == null)
        {
            if (tolerateExternalStateErrors)
            {
                return status;
            }

            throw new InvalidOperationException("Could not fetch external workflow states.");
        }

        string? mappedStatus = ExtStateHandler.GetPreferredExternalStateName(extStateResponse.Result, stateId, true);
        if (!string.IsNullOrWhiteSpace(mappedStatus))
        {
            status = mappedStatus;
        }

        return status;
    }

    /// <summary>
    /// Returns the newest non-empty ticket-level comment text.
    /// </summary>
    private static string GetLatestTicketComment(WfTicket ticket)
    {
        return ticket.Comments?
            .Where(comment => comment?.Comment != null && !string.IsNullOrWhiteSpace(comment.Comment.CommentText))
            .OrderByDescending(comment => comment!.Comment.CreationDate)
            .Select(comment => comment!.Comment.CommentText)
            .FirstOrDefault() ?? string.Empty;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        configSubscription?.Dispose();
    }
}

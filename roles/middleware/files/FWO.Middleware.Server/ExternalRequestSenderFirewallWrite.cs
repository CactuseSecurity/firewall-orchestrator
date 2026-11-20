using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Logging;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Sending of the external requests writing an implementation task to the firewall with the firewall write library.
    /// </summary>
    public partial class ExternalRequestSender
    {
        private const string kFirewallWriteLogTitle = "Firewall Write Request";

        /// <summary>
        /// Creates the request handler taking over the result of a firewall write (settable for unit testing only).
        /// </summary>
        internal Func<ExternalRequestHandler>? HandlerFactory { get; init; }

        /// <summary>
        /// A request writing an implementation task carries no ticket system; one whose implementation task was deleted
        /// meanwhile has lost its reference as well.
        /// </summary>
        private static bool IsFirewallWriteRequest(ExternalRequest request)
        {
            return request.ImplTaskId != null || string.IsNullOrWhiteSpace(request.ExtTicketSystem);
        }

        /// <summary>
        /// Writes the implementation task of an open request to the firewall: success sets the request to done, an error
        /// that may pass is retried after some cycles up to the maximum number of attempts, any other error rejects it.
        /// An implementation task already finished otherwise, e.g. set to done by hand, is not written again.
        /// </summary>
        private async Task HandleFirewallWriteRequest(ExternalRequest request)
        {
            if (request.ExtRequestState != ExtStates.ExtReqInitialized.ToString() && request.ExtRequestState != ExtStates.ExtReqFailed.ToString())
            {
                Log.WriteDebug(kFirewallWriteLogTitle, $"{RequestInfo(request)} is {request.ExtRequestState}; a firewall write has no state to poll.");
                return;
            }
            if (request.WaitCycles > 0)
            {
                await CountDownWaitCycle(request);
                return;
            }
            using ExternalRequestHandler extReqHandler = HandlerFactory?.Invoke() ?? new(userConfig, apiConnection);
            (WfImplTask? implTask, bool isFinished) = await extReqHandler.LoadImplTaskOfRequest(request);
            if (implTask == null)
            {
                await DiscardRequestWithoutImplTask(request, extReqHandler);
                return;
            }
            if (isFinished)
            {
                request.LastMessage = $"Implementation task {implTask.Id} is already finished, nothing written.";
                request.ExtRequestState = ExtStates.ExtReqDone.ToString();
            }
            else if (!await WriteImplTask(request, implTask, extReqHandler))
            {
                return;
            }
            await UpdateRequestCreation(request);
            await FollowUp(request, extReqHandler);
        }

        /// <summary>
        /// Writes the implementation task and sets the resulting request state. Returns false if the write is retried
        /// later, as configured for its management; the request is then already stored.
        /// </summary>
        private async Task<bool> WriteImplTask(ExternalRequest request, WfImplTask implTask, ExternalRequestHandler extReqHandler)
        {
            (CoveringResult covering, int writerTaskNumber) = await extReqHandler.GetCoveringResult(request, implTask);
            if (covering != CoveringResult.NotCovered)
            {
                return await TakeOverCoveringResult(request, covering, writerTaskNumber, extReqHandler);
            }
            ModellingPlaceholderResult substitution = await extReqHandler.SubstituteModellingPlaceholders(implTask);
            if (!substitution.IsWritable)
            {
                Log.WriteWarning(kFirewallWriteLogTitle, $"{RequestInfo(request)}: not written, {substitution.NotWritableReason}");
                await extReqHandler.DiscardNotWritableRequest(request, implTask, substitution.NotWritableReason!);
                return false;
            }
            Log.WriteInfo(kFirewallWriteLogTitle, $"Writing implementation task {implTask.Id} of {RequestInfo(request)}");
            request.Attempts++;
            string gatewaysNote = await EnforcingGatewaysNote(implTask, await extReqHandler.ResolveImplTaskManagementId(implTask));
            List<int>? installOnGatewayIds = ExternalRequestHandler.ExtQueryVariable(request, ExternalVarKeys.InstallOnGateways);
            ModifyFirewallResult result = await modifyFirewall(substitution.ImplTask, installOnGatewayIds);
            request.LastMessage = DescribeResult(result) + gatewaysNote + (substitution.Note == null ? "" : $" {substitution.Note}");
            if (result.Success)
            {
                request.ExtRequestState = ExtStates.ExtReqDone.ToString();
                if (substitution.Note != null)
                {
                    await extReqHandler.AddImplTaskComment(implTask, substitution.Note);
                }
                return true;
            }
            Log.WriteWarning(kFirewallWriteLogTitle, $"{RequestInfo(request)}, attempt {request.Attempts}: {request.LastMessage}");
            GlobalDeviceSpecificSettings settings = await extReqHandler.GetRequestSettings(await extReqHandler.ResolveImplTaskManagementId(implTask));
            if (result.IsRetryable && request.Attempts < settings.RequestMaxAttempts)
            {
                request.ExtRequestState = ExtStates.ExtReqFailed.ToString();
                request.WaitCycles = request.Attempts * settings.RequestCyclesBetweenAttempts;
                await UpdateRequestCreation(request);
                return false;
            }
            request.ExtRequestState = ExtStates.ExtReqRejected.ToString();
            return true;
        }

        /// <summary>
        /// Finishes the request of a task covered by the shared rule of another task without writing: it takes over the
        /// result of that write. If that task is not written yet, which the order of the work list prevents, the request
        /// is discarded and the provisioning continues with the next entry. Returns false if the request is already stored.
        /// </summary>
        private static async Task<bool> TakeOverCoveringResult(ExternalRequest request, CoveringResult covering, int writerTaskNumber,
            ExternalRequestHandler extReqHandler)
        {
            if (covering == CoveringResult.WriterPending)
            {
                Log.WriteWarning(kFirewallWriteLogTitle, $"{RequestInfo(request)}: the rule is written by implementation task {writerTaskNumber}, " +
                    "which is not finished yet; the request is discarded.");
                request.ExtRequestState = ExtStates.ExtReqDiscarded.ToString();
                await extReqHandler.PatchState(request);
                await extReqHandler.StartProvisioning(request.TicketId, null, request);
                return false;
            }
            bool rejected = covering == CoveringResult.Rejected;
            request.LastMessage = rejected
                ? $"Rejected with implementation task {writerTaskNumber}, whose rule covers this task; nothing written."
                : $"Covered by the rule of implementation task {writerTaskNumber}; nothing written.";
            request.ExtRequestState = rejected ? ExtStates.ExtReqRejected.ToString() : ExtStates.ExtReqDone.ToString();
            Log.WriteInfo(kFirewallWriteLogTitle, $"{RequestInfo(request)}: {request.LastMessage}");
            return true;
        }

        /// <summary>
        /// Takes over the finished request into the ticket. The firewall write is done already, so a failure here is
        /// only logged and does not repeat the write.
        /// </summary>
        private static async Task FollowUp(ExternalRequest request, ExternalRequestHandler extReqHandler)
        {
            try
            {
                await extReqHandler.HandleStateChange(request);
            }
            catch (Exception exception)
            {
                Log.WriteError(kFirewallWriteLogTitle, $"{RequestInfo(request)} is {request.ExtRequestState}, but follow-up processing failed.", exception);
            }
        }

        /// <summary>
        /// Closes a request whose implementation task no longer exists and continues the provisioning of the ticket,
        /// keeping a release by a button the request carries.
        /// </summary>
        private static async Task DiscardRequestWithoutImplTask(ExternalRequest request, ExternalRequestHandler extReqHandler)
        {
            Log.WriteWarning(kFirewallWriteLogTitle, $"{RequestInfo(request)}: implementation task {request.ImplTaskId} no longer exists, the request is discarded.");
            request.ExtRequestState = ExtStates.ExtReqDiscarded.ToString();
            await extReqHandler.PatchState(request);
            await extReqHandler.StartProvisioning(request.TicketId, null, request);
        }

        /// <summary>
        /// Names the gateways an existing rule to modify or delete takes effect on according to the import, as the change
        /// applies to all of them, not only to the gateway of the task. Empty for other tasks; a failure to read them only
        /// leaves the note out.
        /// </summary>
        private async Task<string> EnforcingGatewaysNote(WfImplTask implTask, int? targetManagementId)
        {
            if (!WfTaskBase.IsRuleChangeOrDelete(implTask.TaskType) || targetManagementId is not int managementId)
            {
                return "";
            }
            try
            {
                List<Device> gateways = await RuleEnforcingGateways.LoadOfImplTask(apiConnection, managementId, implTask);
                return gateways.Count == 0 ? "" : $" The rule takes effect on the gateways: {string.Join(", ", gateways.Select(gateway => gateway.Name))}.";
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kFirewallWriteLogTitle, $"The gateways of the rules of implementation task {implTask.Id} could not be read: {exception.Message}");
                return "";
            }
        }

        /// <summary>
        /// Describes the write for the request: the summary, the status of every call and, for a failure, the firewall's
        /// own message (e.g. the reason of a failed publish or of a refused TLS connection), so that the cause is visible
        /// without the middleware log.
        /// </summary>
        private static string DescribeResult(ModifyFirewallResult result)
        {
            string steps = string.Join("; ", result.Steps.Select(DescribeStep));
            string description = steps == "" ? result.Message : $"{result.Message} ({steps})";
            string? nativeMessage = result.Success ? null : result.Error?.NativeMessage;
            if (string.IsNullOrWhiteSpace(nativeMessage) || description.Contains(nativeMessage, StringComparison.Ordinal))
            {
                return description;
            }
            return $"{description} Firewall: {nativeMessage}";
        }

        /// <summary>
        /// Describes one call by its status. A call that failed on the firewall but counts as successful, because the
        /// object is already present or the rule already deleted, says so, as its status alone looks like an error.
        /// </summary>
        private static string DescribeStep(FirewallStepResult step)
        {
            string status = $"{step.Operation} {step.Target}: {step.StatusCode}";
            if (step.Message.StartsWith(FirewallWriteClient.kAlreadyPresentNote, StringComparison.Ordinal))
            {
                return $"{status} already present as requested";
            }
            return step.Message.StartsWith(FirewallWriteClient.kAlreadyDeletedNote, StringComparison.Ordinal) ? $"{status} already deleted" : status;
        }
    }
}

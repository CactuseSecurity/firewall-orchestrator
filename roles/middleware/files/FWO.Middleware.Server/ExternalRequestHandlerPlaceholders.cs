using FWO.Data;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Substitution of modelling placeholders in the implementation task of a firewall write request, see
    /// <see cref="ModellingPlaceholderSubstitution"/>.
    /// </summary>
    public partial class ExternalRequestHandler
    {
        private const string kPlaceholderLogTitle = "Modelling Placeholders";

        /// <summary>Ticket loaded with the implementation task of the firewall write request being handled.</summary>
        private WfTicket? firewallWriteTicket;

        /// <summary>
        /// Substitutes the modelling placeholders of the implementation task loaded by <see cref="LoadImplTaskOfRequest"/>
        /// by the objects the extra configurations of its modelled connection name. If its data cannot be loaded, the task
        /// is not writable, as it might contain a placeholder.
        /// </summary>
        /// <param name="implTask">Implementation task of the request.</param>
        /// <returns>The task to write with a note on the substitution, or why it cannot be written automatically.</returns>
        public async Task<ModellingPlaceholderResult> SubstituteModellingPlaceholders(WfImplTask implTask)
        {
            (WfReqTask? reqTask, _) = FindImplTask(firewallWriteTicket, implTask.Id);
            if (reqTask == null)
            {
                return new(implTask, null, null);
            }
            try
            {
                return await wfHandler.SubstituteModellingPlaceholders(reqTask, implTask);
            }
            catch (Exception exception)
            {
                Log.WriteError(kPlaceholderLogTitle, $"The modelling placeholders of implementation task {implTask.Id} could not be substituted.", exception);
                return new(implTask, null, "its modelling placeholders and extra configurations could not be loaded; implement it manually.");
            }
        }

        /// <summary>
        /// Discards a firewall write request whose implementation task cannot be written automatically, which the
        /// provisioning work list normally reports before: the task keeps its state for a person, a workflow alert names
        /// the reason, and the provisioning of the ticket continues, unless only this task was to be written.
        /// </summary>
        /// <param name="request">Request to discard.</param>
        /// <param name="implTask">Implementation task of the request.</param>
        /// <param name="reason">Why the task cannot be written automatically.</param>
        public async Task DiscardNotWritableRequest(ExternalRequest request, WfImplTask implTask, string reason)
        {
            request.LastMessage = $"Not written: {reason}";
            request.ExtRequestState = ExtStates.ExtReqDiscarded.ToString();
            await PatchState(request);
            (WfReqTask? reqTask, _) = FindImplTask(firewallWriteTicket, implTask.Id);
            if (firewallWriteTicket != null && reqTask != null)
            {
                await ReportImplTaskNotWritten(firewallWriteTicket, reqTask, implTask, reason);
            }
            if (!IsSingleImplTaskRequest(request))
            {
                await StartProvisioning(request.TicketId, null, request);
            }
        }

        /// <summary>
        /// Adds a comment to the implementation task loaded by <see cref="LoadImplTaskOfRequest"/>, e.g. which placeholders
        /// the firewall write substituted. The write is done already, so a failure is only logged.
        /// </summary>
        /// <param name="implTask">Implementation task of the request.</param>
        /// <param name="commentText">Text of the comment.</param>
        public async Task AddImplTaskComment(WfImplTask implTask, string commentText)
        {
            try
            {
                (WfReqTask? reqTask, WfImplTask? ticketImplTask) = FindImplTask(firewallWriteTicket, implTask.Id);
                if (firewallWriteTicket == null || reqTask == null || ticketImplTask == null)
                {
                    return;
                }
                wfHandler.SetTicketEnv(firewallWriteTicket);
                wfHandler.SetReqTaskEnv(reqTask);
                if (wfHandler.TrySetImplTaskEnv(ticketImplTask))
                {
                    await wfHandler.ConfAddCommentToImplTask(commentText);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(kPlaceholderLogTitle, $"The comment could not be added to implementation task {implTask.Id}.", exception);
            }
        }
    }
}

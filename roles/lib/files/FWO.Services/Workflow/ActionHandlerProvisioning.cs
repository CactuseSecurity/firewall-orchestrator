using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow;

/// <summary>State action "Create external requests" of the workflow action handler.</summary>
public partial class ActionHandler
{
    private const string kCreateExtRequestsLogTitle = "Create External Requests";

    /// <summary>
    /// Replaces the provisioning registered in the middleware server; used by tests.
    /// </summary>
    internal IExternalRequestProvisioning? ExternalRequestProvisioningOverride { get; set; }

    /// <summary>
    /// Starts the provisioning of the ticket: creates the external request for the next entry of its provisioning
    /// work list, i.e. hands a request task to its external ticket system or writes an implementation task to the
    /// firewall. In the request task scope only this task, a bundled task together with its bundle, is provisioned.
    /// In the implementation task scope only this implementation task is written, and nothing follows it.
    /// Offered as a button, implementation tasks on Manual targets in the scope are written as well.
    /// Runs in the middleware server only, which processes the external requests.
    /// </summary>
    /// <param name="action">The executed action.</param>
    /// <param name="statefulObject">Ticket, request task or implementation task the action is attached to.</param>
    /// <param name="scope">Scope of the stateful object.</param>
    public async Task CreateExtRequests(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope)
    {
        if (scope == WfObjectScopes.ImplementationTask && statefulObject is WfImplTask implTask)
        {
            await CreateExtRequestForImplTask(action, implTask);
            return;
        }
        WfTicket? ticket = GetTicketForBundling(statefulObject, scope);
        if (ticket == null)
        {
            Log.WriteWarning(kCreateExtRequestsLogTitle, $"Action '{action.Name}' found no ticket request tasks.");
            return;
        }
        IExternalRequestProvisioning? provisioning = GetExternalRequestProvisioning(action, ticket.Id);
        if (provisioning == null)
        {
            return;
        }
        long? scopeReqTaskId = scope == WfObjectScopes.RequestTask && statefulObject is WfReqTask reqTask ? reqTask.Id : null;
        if (!await provisioning.StartProvisioning(apiConnection, wfHandler.userConfig, ticket.Id, scopeReqTaskId, IsOfferedAsButton(action)))
        {
            Log.WriteWarning(kCreateExtRequestsLogTitle, $"Action '{action.Name}' could not start the provisioning of ticket {ticket.Id}.");
        }
    }

    /// <summary>
    /// Writes a single implementation task to the firewall, see <see cref="IExternalRequestProvisioning.ProvisionImplTask"/>.
    /// </summary>
    private async Task CreateExtRequestForImplTask(WfStateAction action, WfImplTask implTask)
    {
        long ticketId = implTask.TicketId > 0 ? implTask.TicketId : wfHandler.ActTicket.Id;
        if (ticketId <= 0)
        {
            Log.WriteWarning(kCreateExtRequestsLogTitle, $"Action '{action.Name}' found no ticket of implementation task {implTask.Id}.");
            return;
        }
        IExternalRequestProvisioning? provisioning = GetExternalRequestProvisioning(action, ticketId);
        if (provisioning == null)
        {
            return;
        }
        if (!await provisioning.ProvisionImplTask(apiConnection, wfHandler.userConfig, ticketId, implTask.Id, IsOfferedAsButton(action)))
        {
            Log.WriteWarning(kCreateExtRequestsLogTitle, $"Action '{action.Name}' could not write implementation task {implTask.Id} of ticket {ticketId}.");
        }
    }

    /// <summary>
    /// The provisioning of the middleware server; null, with a warning, outside of it.
    /// </summary>
    private IExternalRequestProvisioning? GetExternalRequestProvisioning(WfStateAction action, long ticketId)
    {
        IExternalRequestProvisioning? provisioning = ExternalRequestProvisioningOverride
            ?? (useInMwServer ? ServiceProvider.Services?.GetService(typeof(IExternalRequestProvisioning)) as IExternalRequestProvisioning : null);
        if (provisioning == null)
        {
            Log.WriteWarning(kCreateExtRequestsLogTitle, $"Action '{action.Name}' is only executed in the middleware server; ticket {ticketId} is not provisioned.");
        }
        return provisioning;
    }
}

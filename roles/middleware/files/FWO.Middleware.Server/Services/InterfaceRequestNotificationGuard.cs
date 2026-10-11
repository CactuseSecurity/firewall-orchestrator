using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Common eligibility checks for interface-request notifications.
    /// </summary>
    internal static class InterfaceRequestNotificationGuard
    {
        /// <summary>
        /// Determines whether an owner may receive an interface-request notification.
        /// </summary>
        /// <param name="owner">Owner to evaluate.</param>
        /// <returns><see langword="true"/> when the owner and its lifecycle state are active.</returns>
        public static bool IsActiveOwner(FwoOwner? owner)
        {
            return owner is { Id: > 0, Active: true }
                && (owner.OwnerLifeCycleState == null || owner.OwnerLifeCycleState.ActiveState);
        }

        /// <summary>
        /// Determines whether an interface request contains the legacy-required request context.
        /// </summary>
        /// <param name="ticket">Workflow ticket.</param>
        /// <param name="requestTask">Interface request task.</param>
        /// <returns><see langword="true"/> when requesting app and person data are present.</returns>
        public static bool HasRequiredRequestContext(WfTicket? ticket, WfReqTask? requestTask)
        {
            bool hasRequestingApp = requestTask?.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner) is > 0;
            bool hasRequester = ticket?.Requester is { DbId: > 0 }
                || !string.IsNullOrWhiteSpace(ticket?.Requester?.Name)
                || !string.IsNullOrWhiteSpace(ticket?.Requester?.Dn)
                || !string.IsNullOrWhiteSpace(ticket?.RequesterDn);
            return hasRequestingApp && hasRequester;
        }
    }
}

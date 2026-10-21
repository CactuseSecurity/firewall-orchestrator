using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        private const int kNoRequesterId = -1;

        /// <summary>
        /// Set by the request pages. Users who are not admins then only load and change tickets they requested themselves
        /// or that belong to an app they are responsible for, either as requested or as requesting app. Auditors still see
        /// all tickets, but can change only those. Callers that have to resolve tickets of other requesters, e.g. the
        /// interface request handling in modelling, leave it off.
        /// </summary>
        public bool RestrictToResponsibleRequests { get; init; }

        private List<int> responsibleOwnerIds = [];
        private List<long> responsibleOwnerTicketIds = [];

        /// <summary>
        /// Checks whether the active ticket may be changed. Without the request restriction this is always the case, as
        /// the other phases decide on their own who may change a ticket.
        /// </summary>
        public bool CanChangeActTicket()
        {
            return !RestrictToResponsibleRequests || ActTicket.Editable;
        }

        /// <summary>
        /// Checks whether the active ticket may be changed and tells the user if not.
        /// </summary>
        private bool CheckActTicketChangeable()
        {
            if (CanChangeActTicket())
            {
                return true;
            }
            DisplayMessageInUi(null, userConfig.GetText("save_request"), userConfig.GetText("E5114"), true);
            return false;
        }

        /// <summary>
        /// Loads the apps and app tickets the user is responsible for, if the request restriction applies.
        /// </summary>
        /// <param name="activeApiConnection">Connection running with the role of the user.</param>
        /// <param name="ownerIds">Ids of the apps the user is responsible for.</param>
        internal async Task LoadResponsibleRequestScope(ApiConnection activeApiConnection, List<int>? ownerIds)
        {
            responsibleOwnerIds = ownerIds ?? [];
            responsibleOwnerTicketIds = [];
            if (RestrictToResponsibleRequests && responsibleOwnerIds.Count > 0)
            {
                List<TicketId> ownerTickets = await activeApiConnection.SendQueryAsync<List<TicketId>>(RequestQueries.getOwnerTicketIds,
                    new { ownerIds = responsibleOwnerIds });
                responsibleOwnerTicketIds = ownerTickets.ConvertAll(ticket => ticket.Id);
            }
        }

        /// <summary>
        /// Combines the request restriction with the visibility group filter, so that ticket list, deep link and reload
        /// apply the same rules.
        /// </summary>
        private Func<WfTicket, bool>? GetTicketFilter()
        {
            Func<WfTicket, bool>? visibilityFilter = GetVisibilityTicketFilter();
            if (!RestrictToResponsibleRequests || userConfig.CanUseAnyRole(Roles.Admin))
            {
                return visibilityFilter;
            }
            return ticket => ApplyRequestRestriction(ticket) && (visibilityFilter == null || visibilityFilter(ticket));
        }

        /// <summary>
        /// Marks the ticket as changeable if the user is responsible for it and decides whether it is shown at all.
        /// </summary>
        private bool ApplyRequestRestriction(WfTicket ticket)
        {
            bool responsible = IsResponsibleForRequest(ticket);
            ticket.Editable = responsible;
            return responsible || userConfig.CanUseAnyRole(Roles.Auditor);
        }

        /// <summary>
        /// Checks whether the ticket was requested by the user or belongs to one of the apps the user is responsible for:
        /// as owner of a request task (the requested app of an interface request), as requesting app of an interface
        /// request, or as app the ticket was registered for.
        /// </summary>
        internal bool IsResponsibleForRequest(WfTicket ticket)
        {
            int requesterId = userConfig.User.DbId > 0 ? userConfig.User.DbId : kNoRequesterId;
            return ticket.IsVisibleForOwner(responsibleOwnerTicketIds, responsibleOwnerIds, requesterId);
        }
    }
}

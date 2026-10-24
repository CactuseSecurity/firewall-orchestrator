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

        private List<int>? scopedOwnerIds;
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
        /// Checks whether a configured state action may be offered and performed on the active ticket or its tasks. Read-only
        /// actions are always allowed, all others need a user who is not read-only and may change the ticket.
        /// </summary>
        /// <param name="action">State action to check.</param>
        public bool CanPerformActionOnActTicket(WfStateAction action)
        {
            return WfStateAction.IsReadonlyType(action.ActionType) || (!ReadOnlyMode && CanChangeActTicket());
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
        /// Keeps the apps the handler was initialized with and, if the request restriction applies, loads the tickets
        /// registered for them.
        /// </summary>
        /// <param name="activeApiConnection">Connection running with the role of the user.</param>
        /// <param name="ownerIds">Ids of the apps of the user, null if the caller does not scope the handler to them.</param>
        internal async Task LoadOwnerScope(ApiConnection activeApiConnection, List<int>? ownerIds)
        {
            scopedOwnerIds = ownerIds;
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
        /// Loads the ticket list of the phase with the same filters as deep links and reloads.
        /// </summary>
        /// <param name="ownerIds">Ids of the apps the tickets are filtered by in owner based mode.</param>
        /// <param name="allStates">True to load tickets of all states instead of the phase's state range.</param>
        /// <param name="fullTickets">True to load the full ticket details.</param>
        internal async Task<List<WfTicket>> FetchTicketList(List<int>? ownerIds, bool allStates, bool fullTickets)
        {
            return dbAcc == null ? [] : await dbAcc.FetchTickets(MasterStateMatrix, ownerIds, allStates, fullTickets, GetTicketFilter());
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
        /// Keeps the ticket changeable only if the user is responsible for it and decides whether it is shown at all.
        /// A ticket the owner based filter already marked as not changeable stays so.
        /// </summary>
        private bool ApplyRequestRestriction(WfTicket ticket)
        {
            bool responsible = IsResponsibleForRequest(ticket);
            ticket.Editable &= responsible;
            return responsible || userConfig.CanUseAnyRole(Roles.Auditor);
        }

        /// <summary>
        /// Checks whether the ticket was requested by the user or belongs to one of the apps the user is responsible for:
        /// as owner of a request task (the requested app of an interface request), as requesting app of an interface
        /// request, or as app the ticket was registered for. This is the rule of the owner based filter in the request phase.
        /// </summary>
        internal bool IsResponsibleForRequest(WfTicket ticket)
        {
            int requesterId = userConfig.User.DbId > 0 ? userConfig.User.DbId : kNoRequesterId;
            return ticket.IsEditableForOwner(responsibleOwnerTicketIds, responsibleOwnerIds, requesterId, true);
        }
    }
}

using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Projects a workflow ticket onto the getTicket response contract and applies its task filter.
/// </summary>
/// <remarks>
/// Nullable text columns are mapped to the empty string of the contract, so no null reaches a property
/// declared as non-nullable. Stored timestamps are emitted without a kind, as they come from
/// timezone-naive columns.
/// </remarks>
public static class TicketResponseMapper
{
    /// <summary>
    /// Maps a ticket with all of its tasks, approvals, implementation tasks, elements, owners and comments.
    /// </summary>
    /// <param name="ticket">Ticket as loaded from the API.</param>
    /// <param name="states">Workflow state names used to resolve every state id.</param>
    /// <param name="status">Ticket status as reported by getRequestStatus.</param>
    /// <param name="filter">Optional request task filter; null returns every task.</param>
    /// <returns>The response for the ticket.</returns>
    public static GetTicketResponse Map(WfTicket ticket, WfStateDict states, string status, TicketTaskFilter? filter)
    {
        return new GetTicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title ?? string.Empty,
            StateId = ticket.StateId,
            State = states.GetName(ticket.StateId),
            Status = status,
            CreationDate = WallClockTimestamp.NormalizeStored(ticket.CreationDate),
            CompletionDate = WallClockTimestamp.NormalizeStored(ticket.CompletionDate),
            Deadline = WallClockTimestamp.NormalizeStored(ticket.Deadline),
            Priority = ticket.Priority,
            RequesterName = ticket.Requester?.Name ?? string.Empty,
            RequesterDn = ticket.RequesterDn ?? string.Empty,
            RequesterGroup = ticket.RequesterGroup ?? string.Empty,
            TenantId = ticket.TenantId,
            Reason = ticket.Reason ?? string.Empty,
            ExternalTicketId = ticket.ExternalTicketId ?? string.Empty,
            ExternalTicketSource = ticket.ExternalTicketSource,
            Locked = ticket.Locked,
            Tasks = (ticket.Tasks ?? [])
                .Where(task => task != null)
                .OrderBy(task => task.TaskNumber)
                .Select(task => MapTask(task, states))
                .Where(task => Matches(task, filter))
                .ToList(),
            Comments = MapComments(ticket.Comments)
        };
    }

    /// <summary>
    /// Determines whether a mapped request task satisfies every supplied filter key.
    /// </summary>
    /// <param name="task">Mapped request task.</param>
    /// <param name="filter">Optional filter; null or a key set to null applies no restriction.</param>
    /// <returns>True when the task matches every supplied key.</returns>
    public static bool Matches(TicketTaskResponse task, TicketTaskFilter? filter)
    {
        return filter == null || (MatchesIdentity(task, filter) && MatchesContent(task, filter) && MatchesDates(task, filter));
    }

    private static bool MatchesIdentity(TicketTaskResponse task, TicketTaskFilter filter)
    {
        return MatchesValue(task.Id, filter.Id)
            && MatchesValue(task.TaskNumber, filter.TaskNumber)
            && MatchesText(task.Title, filter.Title)
            && MatchesText(task.TaskType, filter.TaskType)
            && MatchesValue(task.StateId, filter.StateId)
            && MatchesText(task.State, filter.State)
            && MatchesText(task.RequestAction, filter.RequestAction);
    }

    private static bool MatchesContent(TicketTaskResponse task, TicketTaskFilter filter)
    {
        return MatchesValue(task.RuleActionId, filter.RuleActionId)
            && MatchesValue(task.TrackingId, filter.TrackingId)
            && MatchesText(task.Reason, filter.Reason)
            && MatchesText(task.AdditionalInfo, filter.AdditionalInfo)
            && MatchesText(task.FreeText, filter.FreeText)
            && MatchesValue(task.ManagementId, filter.ManagementId)
            && MatchesText(task.ManagementName, filter.ManagementName)
            && MatchesText(task.AssignedGroup, filter.AssignedGroup)
            && MatchesText(task.CurrentHandlerName, filter.CurrentHandlerName)
            && MatchesValue(task.FlowAccessId, filter.FlowAccessId)
            && MatchesValue(task.Locked, filter.Locked);
    }

    private static bool MatchesDates(TicketTaskResponse task, TicketTaskFilter filter)
    {
        return WallClockTimestamp.Matches(task.Start, filter.Start)
            && WallClockTimestamp.Matches(task.Stop, filter.Stop)
            && WallClockTimestamp.Matches(task.TargetBeginDate, filter.TargetBeginDate)
            && WallClockTimestamp.Matches(task.TargetEndDate, filter.TargetEndDate)
            && WallClockTimestamp.Matches(task.LastRecertDate, filter.LastRecertDate);
    }

    private static bool MatchesValue<T>(T? value, T? expected) where T : struct
    {
        return expected == null || Nullable.Equals(value, expected);
    }

    private static bool MatchesValue<T>(T value, T? expected) where T : struct
    {
        return expected == null || value.Equals(expected.Value);
    }

    private static bool MatchesText(string value, string? expected)
    {
        return expected == null || string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static TicketTaskResponse MapTask(WfReqTask task, WfStateDict states)
    {
        return new TicketTaskResponse
        {
            Id = task.Id,
            TaskNumber = task.TaskNumber,
            Title = task.Title ?? string.Empty,
            TaskType = task.TaskType ?? string.Empty,
            StateId = task.StateId,
            State = states.GetName(task.StateId),
            RequestAction = task.RequestAction ?? string.Empty,
            RuleActionId = task.RuleAction,
            TrackingId = task.Tracking,
            Reason = task.Reason ?? string.Empty,
            AdditionalInfo = task.AdditionalInfo ?? string.Empty,
            FreeText = task.FreeText ?? string.Empty,
            Start = WallClockTimestamp.NormalizeStored(task.Start),
            Stop = WallClockTimestamp.NormalizeStored(task.Stop),
            TargetBeginDate = WallClockTimestamp.NormalizeStored(task.TargetBeginDate),
            TargetEndDate = WallClockTimestamp.NormalizeStored(task.TargetEndDate),
            LastRecertDate = WallClockTimestamp.NormalizeStored(task.LastRecertDate),
            ManagementId = task.ManagementId,
            ManagementName = task.OnManagement?.Name ?? string.Empty,
            DeviceIds = [.. task.GetDeviceList()],
            AssignedGroup = task.AssignedGroup ?? string.Empty,
            CurrentHandlerName = task.CurrentHandler?.Name ?? string.Empty,
            FlowAccessId = task.FlowAccessId,
            Locked = task.Locked,
            Elements = (task.Elements ?? []).Where(element => element != null).Select(MapRequestElement).ToList(),
            Approvals = (task.Approvals ?? []).Where(approval => approval != null).Select(approval => MapApproval(approval, states)).ToList(),
            ImplementationTasks = (task.ImplementationTasks ?? [])
                .Where(implTask => implTask != null)
                .OrderBy(implTask => implTask.TaskNumber)
                .Select(implTask => MapImplementationTask(implTask, states))
                .ToList(),
            Owners = (task.Owners ?? []).Where(owner => owner?.Owner != null).Select(owner => MapOwner(owner)).ToList(),
            Comments = MapComments(task.Comments)
        };
    }

    private static TicketApprovalResponse MapApproval(WfApproval approval, WfStateDict states)
    {
        return new TicketApprovalResponse
        {
            Id = approval.Id,
            StateId = approval.StateId,
            State = states.GetName(approval.StateId),
            DateOpened = WallClockTimestamp.NormalizeStored(approval.DateOpened),
            ApprovalDate = WallClockTimestamp.NormalizeStored(approval.ApprovalDate),
            Deadline = WallClockTimestamp.NormalizeStored(approval.Deadline),
            ApproverGroup = approval.ApproverGroup ?? string.Empty,
            ApproverDn = approval.ApproverDn ?? string.Empty,
            AssignedGroup = approval.AssignedGroup ?? string.Empty,
            TenantId = approval.TenantId,
            InitialApproval = approval.InitialApproval,
            Comments = MapComments(approval.Comments)
        };
    }

    private static TicketImplementationTaskResponse MapImplementationTask(WfImplTask implTask, WfStateDict states)
    {
        return new TicketImplementationTaskResponse
        {
            Id = implTask.Id,
            TaskNumber = implTask.TaskNumber,
            Title = implTask.Title ?? string.Empty,
            TaskType = implTask.TaskType ?? string.Empty,
            StateId = implTask.StateId,
            State = states.GetName(implTask.StateId),
            ImplementationAction = implTask.ImplAction ?? string.Empty,
            DeviceId = implTask.DeviceId,
            RuleActionId = implTask.RuleAction,
            TrackingId = implTask.Tracking,
            Start = WallClockTimestamp.NormalizeStored(implTask.Start),
            Stop = WallClockTimestamp.NormalizeStored(implTask.Stop),
            TargetBeginDate = WallClockTimestamp.NormalizeStored(implTask.TargetBeginDate),
            TargetEndDate = WallClockTimestamp.NormalizeStored(implTask.TargetEndDate),
            FreeText = implTask.FreeText ?? string.Empty,
            AssignedGroup = implTask.AssignedGroup ?? string.Empty,
            CurrentHandlerName = implTask.CurrentHandler?.Name ?? string.Empty,
            Elements = (implTask.ImplElements ?? []).Where(element => element != null).Select(MapImplementationElement).ToList(),
            Comments = MapComments(implTask.Comments)
        };
    }

    private static TicketElementResponse MapRequestElement(WfReqElement element)
    {
        TicketElementResponse response = MapElementBase(element, element.Id, element.RequestAction);
        response.DeviceId = element.DeviceId;
        response.FlowNetworkObjectId = element.FlowNetworkObjectId;
        response.FlowNetworkGroupId = element.FlowNetworkGroupId;
        response.FlowServiceObjectId = element.FlowServiceObjectId;
        response.FlowServiceGroupId = element.FlowServiceGroupId;
        return response;
    }

    /// <remarks>
    /// The implementation element query selects no device or flow object ids, so those stay null
    /// rather than echoing defaults that were never read.
    /// </remarks>
    private static TicketElementResponse MapImplementationElement(WfImplElement element)
    {
        return MapElementBase(element, element.Id, element.ImplAction);
    }

    private static TicketElementResponse MapElementBase(WfElementBase element, long id, string? action)
    {
        return new TicketElementResponse
        {
            Id = id,
            Field = element.Field ?? string.Empty,
            Action = action ?? string.Empty,
            Name = element.Name ?? string.Empty,
            GroupName = element.GroupName ?? string.Empty,
            Ip = element.IpString,
            IpEnd = element.IpEnd,
            Port = element.Port,
            PortEnd = element.PortEnd,
            ProtocolId = element.ProtoId,
            NetworkObjectId = element.NetworkId,
            ServiceId = element.ServiceId,
            UserId = element.UserId,
            OriginalNatId = element.OriginalNatId,
            RuleUid = element.RuleUid
        };
    }

    private static TicketOwnerResponse MapOwner(FWO.Data.FwoOwnerDataHelper owner)
    {
        return new TicketOwnerResponse
        {
            Id = owner.Owner.Id,
            Name = owner.Owner.Name ?? string.Empty,
            ExtAppId = owner.Owner.ExtAppId ?? string.Empty
        };
    }

    /// <summary>
    /// Maps comment wrappers oldest first, skipping wrappers the API returned without a comment.
    /// </summary>
    private static List<TicketCommentResponse> MapComments(List<WfCommentDataHelper>? comments)
    {
        return (comments ?? [])
            .Where(wrapper => wrapper?.Comment != null)
            .Select(wrapper => wrapper.Comment)
            .OrderBy(comment => comment.CreationDate)
            .Select(comment => new TicketCommentResponse
            {
                Id = comment.Id,
                CreationDate = WallClockTimestamp.NormalizeStored(comment.CreationDate),
                CreatorName = comment.Creator?.Name ?? string.Empty,
                Text = comment.CommentText ?? string.Empty
            })
            .ToList();
    }
}

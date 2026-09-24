using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents a workflow ticket with all of its details as returned by the getTicket endpoint.
/// </summary>
/// <remarks>
/// Timestamps come from timezone-naive columns: they carry the wall clock of the installation and no
/// UTC offset. Workflow state names are the internal FWO state names; <see cref="Status"/> additionally
/// carries the externally mapped status that getRequestStatus reports.
/// </remarks>
public sealed class GetTicketResponse
{
    /// <summary>Gets or sets the database id of the ticket.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Gets or sets the ticket title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the workflow state id of the ticket.</summary>
    [JsonPropertyName("stateId")]
    public int StateId { get; set; }

    /// <summary>Gets or sets the workflow state name of the ticket, or the state id when the state has no name.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>Gets or sets the ticket status as reported by getRequestStatus: the preferred external state name, else the workflow state name.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation time of the ticket.</summary>
    [JsonPropertyName("creationDate")]
    public DateTime? CreationDate { get; set; }

    /// <summary>Gets or sets the completion time of the ticket; null while the ticket is open.</summary>
    [JsonPropertyName("completionDate")]
    public DateTime? CompletionDate { get; set; }

    /// <summary>Gets or sets the ticket deadline; null when none is set.</summary>
    [JsonPropertyName("deadline")]
    public DateTime? Deadline { get; set; }

    /// <summary>Gets or sets the numeric ticket priority; null when none is set.</summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    /// <summary>Gets or sets the user name of the requester; empty when unknown.</summary>
    [JsonPropertyName("requesterName")]
    public string RequesterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the stored requester identifier (LDAP DN or the requestorId given to createRequest).</summary>
    [JsonPropertyName("requesterDn")]
    public string RequesterDn { get; set; } = string.Empty;

    /// <summary>Gets or sets the requester group (LDAP DN); empty when none is set.</summary>
    [JsonPropertyName("requesterGroup")]
    public string RequesterGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant id of the ticket; null when none is set.</summary>
    [JsonPropertyName("tenantId")]
    public int? TenantId { get; set; }

    /// <summary>Gets or sets the ticket reason; empty when none is set.</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the id of the linked external ticket; empty when none is linked.</summary>
    [JsonPropertyName("externalTicketId")]
    public string ExternalTicketId { get; set; } = string.Empty;

    /// <summary>Gets or sets the id of the external ticket source; null when none is linked.</summary>
    [JsonPropertyName("externalTicketSource")]
    public int? ExternalTicketSource { get; set; }

    /// <summary>Gets or sets whether the request task content of the ticket is locked.</summary>
    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    /// <summary>Gets or sets the request tasks of the ticket ordered by task number, restricted by options.filter.</summary>
    [JsonPropertyName("tasks")]
    public List<TicketTaskResponse> Tasks { get; set; } = [];

    /// <summary>Gets or sets the ticket-level comments, oldest first.</summary>
    [JsonPropertyName("comments")]
    public List<TicketCommentResponse> Comments { get; set; } = [];
}

/// <summary>
/// Holds the fields shared by request tasks and implementation tasks of a workflow ticket.
/// </summary>
public abstract class TicketTaskResponseBase
{
    // System.Text.Json writes derived-type properties first. Pinning the order keeps the identifying
    // fields at the top of each task and the comments at its end.
    private const int kSharedFieldsFirst = -1;
    private const int kCommentsLast = 1;

    /// <summary>Gets or sets the database id of the task.</summary>
    [JsonPropertyName("id"), JsonPropertyOrder(kSharedFieldsFirst)]
    public long Id { get; set; }

    /// <summary>Gets or sets the number of the task within its parent (ticket or request task).</summary>
    [JsonPropertyName("taskNumber"), JsonPropertyOrder(kSharedFieldsFirst)]
    public int TaskNumber { get; set; }

    /// <summary>Gets or sets the task title.</summary>
    [JsonPropertyName("title"), JsonPropertyOrder(kSharedFieldsFirst)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the task type, e.g. access or group_create.</summary>
    [JsonPropertyName("taskType"), JsonPropertyOrder(kSharedFieldsFirst)]
    public string TaskType { get; set; } = string.Empty;

    /// <summary>Gets or sets the workflow state id of the task.</summary>
    [JsonPropertyName("stateId"), JsonPropertyOrder(kSharedFieldsFirst)]
    public int StateId { get; set; }

    /// <summary>Gets or sets the workflow state name of the task, or the state id when the state has no name.</summary>
    [JsonPropertyName("state"), JsonPropertyOrder(kSharedFieldsFirst)]
    public string State { get; set; } = string.Empty;

    /// <summary>Gets or sets the rule action id; null when not applicable.</summary>
    [JsonPropertyName("ruleActionId"), JsonPropertyOrder(kSharedFieldsFirst)]
    public int? RuleActionId { get; set; }

    /// <summary>Gets or sets the rule tracking id; null when not applicable.</summary>
    [JsonPropertyName("trackingId"), JsonPropertyOrder(kSharedFieldsFirst)]
    public int? TrackingId { get; set; }

    /// <summary>Gets or sets the free text of the task; empty when none is set.</summary>
    [JsonPropertyName("freeText"), JsonPropertyOrder(kSharedFieldsFirst)]
    public string FreeText { get; set; } = string.Empty;

    /// <summary>Gets or sets the rule validity start; null when none is set.</summary>
    [JsonPropertyName("start"), JsonPropertyOrder(kSharedFieldsFirst)]
    public DateTime? Start { get; set; }

    /// <summary>Gets or sets the rule validity end; null when none is set.</summary>
    [JsonPropertyName("stop"), JsonPropertyOrder(kSharedFieldsFirst)]
    public DateTime? Stop { get; set; }

    /// <summary>Gets or sets the target begin date; null when none is set.</summary>
    [JsonPropertyName("targetBeginDate"), JsonPropertyOrder(kSharedFieldsFirst)]
    public DateTime? TargetBeginDate { get; set; }

    /// <summary>Gets or sets the target end date; null when none is set.</summary>
    [JsonPropertyName("targetEndDate"), JsonPropertyOrder(kSharedFieldsFirst)]
    public DateTime? TargetEndDate { get; set; }

    /// <summary>Gets or sets the assigned group (LDAP DN); empty when none is assigned.</summary>
    [JsonPropertyName("assignedGroup"), JsonPropertyOrder(kSharedFieldsFirst)]
    public string AssignedGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name of the current handler; empty when none is set.</summary>
    [JsonPropertyName("currentHandlerName"), JsonPropertyOrder(kSharedFieldsFirst)]
    public string CurrentHandlerName { get; set; } = string.Empty;

    /// <summary>Gets or sets the comments of the task, oldest first. Not filterable.</summary>
    [JsonPropertyName("comments"), JsonPropertyOrder(kCommentsLast)]
    public List<TicketCommentResponse> Comments { get; set; } = [];
}

/// <summary>
/// Represents one request task of a workflow ticket.
/// </summary>
public sealed class TicketTaskResponse : TicketTaskResponseBase
{
    /// <summary>Gets or sets the request action, e.g. create or delete.</summary>
    [JsonPropertyName("requestAction")]
    public string RequestAction { get; set; } = string.Empty;

    /// <summary>Gets or sets the task reason, e.g. the violation justification; empty when none is set.</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the stored additional info of the task as JSON text; empty when none is set.</summary>
    [JsonPropertyName("additionalInfo")]
    public string AdditionalInfo { get; set; } = string.Empty;

    /// <summary>Gets or sets the last recertification date; null when never recertified.</summary>
    [JsonPropertyName("lastRecertDate")]
    public DateTime? LastRecertDate { get; set; }

    /// <summary>Gets or sets the id of the management the task applies to; null when none is set.</summary>
    [JsonPropertyName("managementId")]
    public int? ManagementId { get; set; }

    /// <summary>Gets or sets the name of the management the task applies to; empty when none is set.</summary>
    [JsonPropertyName("managementName")]
    public string ManagementName { get; set; } = string.Empty;

    /// <summary>Gets or sets the ids of the selected devices; -1 stands for all devices. Not filterable.</summary>
    [JsonPropertyName("deviceIds")]
    public List<int> DeviceIds { get; set; } = [];

    /// <summary>Gets or sets the id of the flow access the task was created from; null when none.</summary>
    [JsonPropertyName("flowAccessId")]
    public long? FlowAccessId { get; set; }

    /// <summary>Gets or sets whether the task content is locked.</summary>
    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    /// <summary>Gets or sets the requested elements of the task. Not filterable.</summary>
    [JsonPropertyName("elements")]
    public List<TicketElementResponse> Elements { get; set; } = [];

    /// <summary>Gets or sets the approvals of the task. Not filterable.</summary>
    [JsonPropertyName("approvals")]
    public List<TicketApprovalResponse> Approvals { get; set; } = [];

    /// <summary>Gets or sets the implementation tasks of the task. Not filterable.</summary>
    [JsonPropertyName("implementationTasks")]
    public List<TicketImplementationTaskResponse> ImplementationTasks { get; set; } = [];

    /// <summary>Gets or sets the owners assigned to the task. Not filterable.</summary>
    [JsonPropertyName("owners")]
    public List<TicketOwnerResponse> Owners { get; set; } = [];
}

/// <summary>
/// Represents one approval of a request task.
/// </summary>
public sealed class TicketApprovalResponse
{
    /// <summary>Gets or sets the database id of the approval.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Gets or sets the workflow state id of the approval.</summary>
    [JsonPropertyName("stateId")]
    public int StateId { get; set; }

    /// <summary>Gets or sets the workflow state name of the approval, or the state id when the state has no name.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>Gets or sets the time the approval was opened.</summary>
    [JsonPropertyName("dateOpened")]
    public DateTime? DateOpened { get; set; }

    /// <summary>Gets or sets the time the approval was decided; null while open.</summary>
    [JsonPropertyName("approvalDate")]
    public DateTime? ApprovalDate { get; set; }

    /// <summary>Gets or sets the approval deadline; null when none is set.</summary>
    [JsonPropertyName("deadline")]
    public DateTime? Deadline { get; set; }

    /// <summary>Gets or sets the approver group (LDAP DN); empty when none is set.</summary>
    [JsonPropertyName("approverGroup")]
    public string ApproverGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the approver (LDAP DN); empty while undecided.</summary>
    [JsonPropertyName("approverDn")]
    public string ApproverDn { get; set; } = string.Empty;

    /// <summary>Gets or sets the assigned group (LDAP DN); empty when none is assigned.</summary>
    [JsonPropertyName("assignedGroup")]
    public string AssignedGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant id of the approval; null when none is set.</summary>
    [JsonPropertyName("tenantId")]
    public int? TenantId { get; set; }

    /// <summary>Gets or sets whether this is the initial approval created with the task.</summary>
    [JsonPropertyName("initialApproval")]
    public bool InitialApproval { get; set; }

    /// <summary>Gets or sets the comments of the approval, oldest first.</summary>
    [JsonPropertyName("comments")]
    public List<TicketCommentResponse> Comments { get; set; } = [];
}

/// <summary>
/// Represents one implementation task of a request task.
/// </summary>
public sealed class TicketImplementationTaskResponse : TicketTaskResponseBase
{
    /// <summary>Gets or sets the implementation action, e.g. create or delete.</summary>
    [JsonPropertyName("implementationAction")]
    public string ImplementationAction { get; set; } = string.Empty;

    /// <summary>Gets or sets the id of the device to implement on; null when none is set.</summary>
    [JsonPropertyName("deviceId")]
    public int? DeviceId { get; set; }

    /// <summary>Gets or sets the elements of the implementation task.</summary>
    [JsonPropertyName("elements")]
    public List<TicketElementResponse> Elements { get; set; } = [];
}

/// <summary>
/// Represents one element of a request or implementation task: a network object, service, rule or user
/// reference.
/// </summary>
public sealed class TicketElementResponse
{
    /// <summary>Gets or sets the database id of the element.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Gets or sets the rule field the element belongs to, e.g. source, destination, service or rule.</summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested or implemented action, e.g. create or delete.</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the element name; empty when none is set.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the group the element belongs to; empty when none.</summary>
    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>Gets or sets the start IP address in CIDR notation; null for non-network elements.</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Gets or sets the end IP address in CIDR notation; null for non-network elements.</summary>
    [JsonPropertyName("ipEnd")]
    public string? IpEnd { get; set; }

    /// <summary>Gets or sets the start port; null for non-service elements or protocol-only services.</summary>
    [JsonPropertyName("port")]
    public int? Port { get; set; }

    /// <summary>Gets or sets the end port; null for non-service elements or protocol-only services.</summary>
    [JsonPropertyName("portEnd")]
    public int? PortEnd { get; set; }

    /// <summary>Gets or sets the IP protocol id; null for non-service elements.</summary>
    [JsonPropertyName("protocolId")]
    public int? ProtocolId { get; set; }

    /// <summary>Gets or sets the id of the referenced network object; null when none.</summary>
    [JsonPropertyName("networkObjectId")]
    public long? NetworkObjectId { get; set; }

    /// <summary>Gets or sets the id of the referenced service; null when none.</summary>
    [JsonPropertyName("serviceId")]
    public long? ServiceId { get; set; }

    /// <summary>Gets or sets the id of the referenced user; null when none.</summary>
    [JsonPropertyName("userId")]
    public long? UserId { get; set; }

    /// <summary>Gets or sets the id of the original NAT object; null when none.</summary>
    [JsonPropertyName("originalNatId")]
    public long? OriginalNatId { get; set; }

    /// <summary>Gets or sets the uid of the referenced rule; null for non-rule elements.</summary>
    [JsonPropertyName("ruleUid")]
    public string? RuleUid { get; set; }

    /// <summary>Gets or sets the id of the device of a rule element; always null for implementation task elements.</summary>
    [JsonPropertyName("deviceId")]
    public int? DeviceId { get; set; }

    /// <summary>Gets or sets the id of the referenced flow network object; always null for implementation task elements.</summary>
    [JsonPropertyName("flowNetworkObjectId")]
    public long? FlowNetworkObjectId { get; set; }

    /// <summary>Gets or sets the id of the referenced flow network group; always null for implementation task elements.</summary>
    [JsonPropertyName("flowNetworkGroupId")]
    public long? FlowNetworkGroupId { get; set; }

    /// <summary>Gets or sets the id of the referenced flow service object; always null for implementation task elements.</summary>
    [JsonPropertyName("flowServiceObjectId")]
    public long? FlowServiceObjectId { get; set; }

    /// <summary>Gets or sets the id of the referenced flow service group; always null for implementation task elements.</summary>
    [JsonPropertyName("flowServiceGroupId")]
    public long? FlowServiceGroupId { get; set; }
}

/// <summary>
/// Represents one owner assigned to a request task.
/// </summary>
public sealed class TicketOwnerResponse
{
    /// <summary>Gets or sets the database id of the owner.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Gets or sets the owner name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the external application id of the owner; empty when none is set.</summary>
    [JsonPropertyName("extAppId")]
    public string ExtAppId { get; set; } = string.Empty;
}

/// <summary>
/// Represents one comment on a ticket, request task, approval or implementation task.
/// </summary>
public sealed class TicketCommentResponse
{
    /// <summary>Gets or sets the database id of the comment.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Gets or sets the creation time of the comment.</summary>
    [JsonPropertyName("creationDate")]
    public DateTime? CreationDate { get; set; }

    /// <summary>Gets or sets the user name of the comment author; empty when unknown.</summary>
    [JsonPropertyName("creatorName")]
    public string CreatorName { get; set; } = string.Empty;

    /// <summary>Gets or sets the comment text.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

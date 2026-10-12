using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

internal sealed record RuleTaskLookups(Dictionary<int, FwoOwner> OwnersById, Dictionary<string, int> RuleActionIds);

internal sealed class TicketBuildContext
{
    public int TaskNumber { get; set; } = 1;
    public List<RequestValidationError> ValidationErrors { get; } = [];
    public HashSet<long> InvalidEntityIds { get; } = [];
}

internal sealed record WorkflowTicketTaskContext(Dictionary<long, WorkflowTicketEntity> Entities, int TicketStateId,
    FlowReferenceCatalog FlowReferences, TicketBuildContext Validation);

internal sealed record WorkflowTicketRuleTaskContext(Dictionary<long, WorkflowTicketEntity> Entities, int TicketStateId,
    RuleTaskLookups Lookups, FlowReferenceCatalog FlowReferences, int TaskNumber, int RuleIndex, TicketBuildContext Validation);

internal sealed record WorkflowTicketReferenceContext(Dictionary<long, WorkflowTicketEntity> Entities, ElemFieldType Field,
    WorkflowTicketEntityKind ExpectedKind, FlowReferenceCatalog FlowReferences, string Path, TicketBuildContext Validation);

internal sealed record WorkflowTicketElementContext(HashSet<long> InvalidEntityIds, List<RequestValidationError> ValidationErrors,
    string Path, Func<long, WfReqElement> BuildElement);

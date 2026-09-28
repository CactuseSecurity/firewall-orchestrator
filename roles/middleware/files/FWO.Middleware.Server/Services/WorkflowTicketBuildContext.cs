using FWO.Data;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

internal sealed record RuleTaskLookups(Dictionary<int, FwoOwner> OwnersById, Dictionary<string, int> RuleActionIds);

internal sealed class TicketBuildContext
{
    public int TaskNumber { get; set; } = 1;
    public List<RequestValidationError> ValidationErrors { get; } = [];
    public HashSet<long> InvalidEntityIds { get; } = [];
}

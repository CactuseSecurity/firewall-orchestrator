using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;

namespace FWO.Ui.Services;

/// <summary>
/// Loads lookup data and derives ticket-local task selections for the approval view.
/// </summary>
internal sealed class ApprovalTaskDetailsLookupService
{
    private readonly ApprovalTaskDetailsEditorContext context;
    private readonly ApprovalTaskDetailsEditorState state;

    internal ApprovalTaskDetailsLookupService(
        ApprovalTaskDetailsEditorContext context,
        ApprovalTaskDetailsEditorState state)
    {
        this.context = context;
        this.state = state;
    }

    internal async Task InitializeAsync(ApiConnection apiConnection)
    {
        try
        {
            state.RuleActions = await apiConnection.SendQueryAsync<List<RuleAction>>(StmQueries.getRuleActions) ?? [];
            state.Trackings = await apiConnection.SendQueryAsync<List<Tracking>>(StmQueries.getTracking) ?? [];
            List<IpProtocol> protocols = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols) ?? [];
            state.IpProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, null, false);
            state.SelectableIpProtocols = ProtocolDisplayHelper.CustomSortProtocols(
                ProtocolDisplayHelper.GetSelectableProtocols(protocols),
                ProtocolDisplayHelper.ParseProtocolNames(context.UserConfig.ReducedProtocolSetProtocols),
                context.UserConfig.ReducedProtocolSet);
            if (context.UserConfig.DesignatedZoneMatrixId > 0)
            {
                state.NetworkZones = await apiConnection.SendQueryAsync<List<NetworkZone>>(
                    NetworkZoneQueries.getNetworkZonesForMatrix,
                    new { criterionId = context.UserConfig.DesignatedZoneMatrixId }) ?? [];
            }
        }
        catch
        {
            // The IDs remain useful as a fallback when the lookup data is unavailable.
            state.RuleActions = [];
            state.Trackings = [];
            state.IpProtocols = [];
            state.SelectableIpProtocols = [];
        }
    }

    internal IEnumerable<List<WfReqTask>> GetTaskGroups()
    {
        IEnumerable<WfReqTask> tasks = context.WfHandler.ActTicket.Tasks
            .Where(task => task.TaskType == WfTaskType.access.ToString()
                || task.TaskType == WfTaskType.generic.ToString()
                || task.TaskType == WfTaskType.new_interface.ToString()
                || WfObjectTaskHelper.IsManagementObjectTask(task.TaskType));

        if (!context.UserConfig.ReqDisplayBundledTasksAsOne || !context.UserConfig.ReqConsiderBundling)
        {
            return tasks.Select(task => new List<WfReqTask> { task });
        }

        return tasks.GroupBy(task =>
        {
            if (WfObjectTaskHelper.IsManagementObjectTask(task.TaskType))
            {
                return $"task:{task.Id}";
            }

            string bundleId = task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
            return string.IsNullOrWhiteSpace(bundleId)
                ? $"task:{task.Id}"
                : $"bundle:{bundleId}:state:{task.StateId}";
        }).Select(group => group.OrderBy(task => task.Id).ToList());
    }

    internal List<FlowTimeObject> GetTicketTimeObjects()
    {
        return [.. TimeObjectReferenceResolver.GetTicketTimeElements(context.WfHandler.ActTicket.Tasks)
            .Select(element => new FlowTimeObject
            {
                Id = TimeObjectReferenceResolver.GetSelectorId(element),
                Name = element.Name ?? "",
                StartTime = element.StartTime,
                EndTime = element.EndTime
            })
            .Where(timeObject => timeObject.Id != 0)
            .DistinctBy(timeObject => timeObject.Id)];
    }

    internal Dictionary<long, long> GetTicketTimeObjectElementIds()
    {
        return TimeObjectReferenceResolver.GetTicketTimeElements(context.WfHandler.ActTicket.Tasks)
            .Where(element => element.Id != 0)
            .GroupBy(TimeObjectReferenceResolver.GetSelectorId)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }
}

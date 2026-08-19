using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public sealed class WorkflowEmailBundleCollector
    {
        public bool IsFlushing { get; set; }
        public List<WorkflowEmailBundleItem> PendingItems { get; } = [];

        public void Add(WfStateAction action, WfReqTask requestTask, FwoOwner? owner, string? userGrpDn)
        {
            PendingItems.Add(new WorkflowEmailBundleItem(action, requestTask, owner, userGrpDn));
        }
    }

    public sealed class WorkflowEmailBundleItem
    {
        public WfStateAction Action { get; }
        public WfReqTask RequestTask { get; }
        public FwoOwner? Owner { get; }
        public string? UserGrpDn { get; }
        public string BundleKey { get; }

        public WorkflowEmailBundleItem(WfStateAction action, WfReqTask requestTask, FwoOwner? owner, string? userGrpDn)
        {
            Action = new WfStateAction(action);
            RequestTask = new WfReqTask(requestTask);
            Owner = owner;
            UserGrpDn = userGrpDn;
            BundleKey = BuildBundleKey(Action, RequestTask, Owner, UserGrpDn);
        }

        public static string BuildBundleKey(WfStateAction action, WfReqTask requestTask, FwoOwner? owner, string? userGrpDn)
        {
            return string.Join("|",
                requestTask.TicketId,
                requestTask.TaskType,
                requestTask.StateId,
                requestTask.GetAddInfoValue(AdditionalInfoKeys.FwConfigChangeTarget),
                requestTask.AssignedGroup,
                requestTask.CurrentHandler?.Dn,
                requestTask.RecentHandler?.Dn,
                action.Id,
                action.ExternalParams,
                owner?.Id,
                userGrpDn);
        }
    }
}

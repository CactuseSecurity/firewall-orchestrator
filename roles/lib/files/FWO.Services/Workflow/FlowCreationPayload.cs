using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Snapshot of one flow prepared from workflow task data.
    /// </summary>
    public class FlowCreationPayload
    {
        public FlowCreationPayload()
        { }

        public FlowCreationPayload(FlowCreationPayload payload)
        {
            TicketId = payload.TicketId;
            OwnerId = payload.OwnerId;
            TaskType = payload.TaskType;
            TaskAction = payload.TaskAction;
            RuleActionId = payload.RuleActionId;
            ManagementId = payload.ManagementId;
            BundleId = payload.BundleId;
            GroupName = payload.GroupName;
            OriginRequestTaskIds = [.. payload.OriginRequestTaskIds];
            Sources = [.. payload.Sources];
            Destinations = [.. payload.Destinations];
            Services = [.. payload.Services];
        }

        public long? TicketId { get; set; }
        public int? OwnerId { get; set; }
        public string TaskType { get; set; } = "";
        public string TaskAction { get; set; } = "";
        public int? RuleActionId { get; set; }
        public int? ManagementId { get; set; }
        public string BundleId { get; set; } = "";
        public string GroupName { get; set; } = "";
        public List<long> OriginRequestTaskIds { get; set; } = [];
        public List<FlowObjectSnapshot> Sources { get; set; } = [];
        public List<FlowObjectSnapshot> Destinations { get; set; } = [];
        public List<FlowServiceSnapshot> Services { get; set; } = [];
    }

    public class FlowObjectSnapshot
    {
        public long WorkflowElementId { get; set; }
        public ElemFieldType Field { get; set; }
        public long? OriginalNetworkObjectId { get; set; }
        public long? FlowNetworkObjectId { get; set; }
        public long? FlowNetworkGroupId { get; set; }
        public string? Ip { get; set; }
        public string? IpEnd { get; set; }
        public string? Name { get; set; }
        public string? GroupName { get; set; }
        public string RequestAction { get; set; } = "";
    }

    public class FlowServiceSnapshot
    {
        public long WorkflowElementId { get; set; }
        public long? OriginalServiceId { get; set; }
        public long? FlowServiceObjectId { get; set; }
        public long? FlowServiceGroupId { get; set; }
        public int? ProtoId { get; set; }
        public int? Port { get; set; }
        public int? PortEnd { get; set; }
        public string? Name { get; set; }
        public string? GroupName { get; set; }
        public string RequestAction { get; set; } = "";
    }
}

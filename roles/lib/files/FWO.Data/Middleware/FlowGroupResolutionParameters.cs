using FWO.Data.Flow;

namespace FWO.Data.Middleware
{
    public sealed class FlowGroupResolutionParameters
    {
        public List<long> NetworkGroupIds { get; set; } = [];
        public List<string> NetworkGroupNames { get; set; } = [];
        public List<long> ServiceGroupIds { get; set; } = [];
        public List<string> ServiceGroupNames { get; set; } = [];
    }

    public sealed class FlowGroupResolutionResult
    {
        public List<FlowNwGroup> NetworkGroups { get; set; } = [];
        public List<FlowSvcGroup> ServiceGroups { get; set; } = [];
    }
}

using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public class FlowPayloadMerger
    {
        public List<FlowCreationPayload> MergeBundled(IEnumerable<FlowCreationPayload> payloads)
        {
            return
            [
                .. payloads
                    .GroupBy(BuildMergeKey)
                    .SelectMany(group => string.IsNullOrWhiteSpace(group.Key.BundleId)
                        ? group.Select(payload => new FlowCreationPayload(payload))
                        : [MergeGroup(group)])
            ];
        }

        private static FlowPayloadMergeKey BuildMergeKey(FlowCreationPayload payload)
        {
            return new FlowPayloadMergeKey(
                payload.BundleId,
                payload.TicketId,
                payload.OwnerId,
                payload.TaskType,
                payload.TaskAction,
                payload.RuleActionId,
                payload.ManagementId ?? 0,
                payload.GroupName,
                payload.TimeStart,
                payload.TimeEnd);
        }

        private static FlowCreationPayload MergeGroup(IEnumerable<FlowCreationPayload> payloads)
        {
            return payloads
                .OrderBy(GetFirstOriginRequestTaskId)
                .Select(payload => new FlowCreationPayload(payload))
                .Aggregate(MergePayloads);
        }

        private static FlowCreationPayload MergePayloads(FlowCreationPayload first, FlowCreationPayload second)
        {
            FlowCreationPayload merged = new(first);
            merged.OriginRequestTaskIds = MergeValues(first.OriginRequestTaskIds, second.OriginRequestTaskIds);
            merged.Sources = MergeObjects(first.Sources, second.Sources);
            merged.Destinations = MergeObjects(first.Destinations, second.Destinations);
            merged.Services = MergeServices(first.Services, second.Services);
            return merged;
        }

        private static List<FlowObjectSnapshot> MergeObjects(List<FlowObjectSnapshot> first, List<FlowObjectSnapshot> second)
        {
            return [.. first.Concat(second)];
        }

        private static List<FlowServiceSnapshot> MergeServices(List<FlowServiceSnapshot> first, List<FlowServiceSnapshot> second)
        {
            return [.. first.Concat(second)];
        }

        private static List<long> MergeValues(List<long> first, List<long> second)
        {
            return [.. first.Concat(second).Distinct().Order()];
        }

        private static long GetFirstOriginRequestTaskId(FlowCreationPayload payload)
        {
            return payload.OriginRequestTaskIds.Count > 0 ? payload.OriginRequestTaskIds.Min() : long.MaxValue;
        }

        private sealed record FlowPayloadMergeKey(string BundleId, long? TicketId, int? OwnerId, string TaskType, string TaskAction, int? RuleActionId,
            int ManagementId, string GroupName, DateTime? TimeStart, DateTime? TimeEnd);
    }
}

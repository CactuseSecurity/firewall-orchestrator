using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public class RequestTaskBundler
    {
        private const string BundleIdPrefix = "bundle-";

        public Dictionary<long, string> BuildBundleAssignments(IEnumerable<WfReqTask> requestTasks, BundleTaskType bundleType)
        {
            return bundleType switch
            {
                BundleTaskType.TwoOutOfThree => BuildTwoOutOfThreeAssignments(requestTasks),
                _ => []
            };
        }

        private Dictionary<long, string> BuildTwoOutOfThreeAssignments(IEnumerable<WfReqTask> requestTasks)
        {
            Dictionary<long, string> assignments = [];
            foreach (RequestTaskBundleSignature signature in BundleTwoOutOfThree(requestTasks.Select(RequestTaskBundleSignature.FromTask))
                .Where(signature => signature.RequestTaskIds.Count > 1))
            {
                List<long> taskIds = [.. signature.RequestTaskIds.Distinct().Order()];
                string bundleId = $"{BundleIdPrefix}{string.Join("-", taskIds)}";
                foreach (long taskId in taskIds)
                {
                    assignments[taskId] = bundleId;
                }
            }
            return assignments;
        }

        private static List<RequestTaskBundleSignature> BundleTwoOutOfThree(IEnumerable<RequestTaskBundleSignature> signatures)
        {
            List<RequestTaskBundleSignature> mergedSignatures = [.. signatures];
            bool merged;
            do
            {
                merged = MergeFirstMatchingPair(mergedSignatures);
            }
            while (merged);

            return mergedSignatures;
        }

        private static bool MergeFirstMatchingPair(List<RequestTaskBundleSignature> signatures)
        {
            for (int index = 0; index < signatures.Count; ++index)
            {
                for (int compareIndex = index + 1; compareIndex < signatures.Count; ++compareIndex)
                {
                    if (CanBundle(signatures[index], signatures[compareIndex]))
                    {
                        signatures[index] = RequestTaskBundleSignature.Merge(signatures[index], signatures[compareIndex]);
                        signatures.RemoveAt(compareIndex);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool CanBundle(RequestTaskBundleSignature first, RequestTaskBundleSignature second)
        {
            return first.IsTwoOutOfThreeCandidate()
                && second.IsTwoOutOfThreeCandidate()
                && first.SameBaseContext(second)
                && first.CountEqualDimensions(second) >= 2;
        }

        private sealed class RequestTaskBundleSignature
        {
            public long? TicketId { get; private set; }
            public int? OwnerId { get; private set; }
            public string TaskType { get; private set; } = "";
            public string TaskAction { get; private set; } = "";
            public int? RuleActionId { get; private set; }
            public int? ManagementId { get; private set; }
            public List<long> RequestTaskIds { get; private set; } = [];
            public HashSet<string> SourceKeys { get; private set; } = [];
            public HashSet<string> DestinationKeys { get; private set; } = [];
            public HashSet<string> ServiceKeys { get; private set; } = [];

            public static RequestTaskBundleSignature FromTask(WfReqTask task)
            {
                return new RequestTaskBundleSignature
                {
                    TicketId = task.TicketId,
                    OwnerId = task.Owners.FirstOrDefault()?.Owner.Id,
                    TaskType = task.TaskType,
                    TaskAction = task.RequestAction,
                    RuleActionId = task.RuleAction,
                    ManagementId = task.ManagementId,
                    RequestTaskIds = task.Id > 0 ? [task.Id] : [],
                    SourceKeys = ElementKeys(task.Elements, ElemFieldType.source),
                    DestinationKeys = ElementKeys(task.Elements, ElemFieldType.destination),
                    ServiceKeys = ElementKeys(task.Elements, ElemFieldType.service)
                };
            }

            public static RequestTaskBundleSignature Merge(RequestTaskBundleSignature first, RequestTaskBundleSignature second)
            {
                return new RequestTaskBundleSignature
                {
                    TicketId = first.TicketId,
                    OwnerId = first.OwnerId,
                    TaskType = first.TaskType,
                    TaskAction = first.TaskAction,
                    RuleActionId = first.RuleActionId,
                    ManagementId = first.ManagementId,
                    RequestTaskIds = [.. first.RequestTaskIds.Concat(second.RequestTaskIds).Distinct().Order()],
                    SourceKeys = [.. first.SourceKeys.Concat(second.SourceKeys)],
                    DestinationKeys = [.. first.DestinationKeys.Concat(second.DestinationKeys)],
                    ServiceKeys = [.. first.ServiceKeys.Concat(second.ServiceKeys)]
                };
            }

            public bool SameBaseContext(RequestTaskBundleSignature other)
            {
                return TicketId == other.TicketId
                    && OwnerId == other.OwnerId
                    && TaskType == other.TaskType
                    && TaskAction == other.TaskAction
                    && RuleActionId == other.RuleActionId
                    && ManagementId == other.ManagementId;
            }

            public bool IsTwoOutOfThreeCandidate()
            {
                return TaskType == WfTaskType.access.ToString()
                    && SourceKeys.Count > 0
                    && DestinationKeys.Count > 0
                    && ServiceKeys.Count > 0;
            }

            public int CountEqualDimensions(RequestTaskBundleSignature other)
            {
                int equalDimensions = 0;
                equalDimensions += SourceKeys.SetEquals(other.SourceKeys) ? 1 : 0;
                equalDimensions += DestinationKeys.SetEquals(other.DestinationKeys) ? 1 : 0;
                equalDimensions += ServiceKeys.SetEquals(other.ServiceKeys) ? 1 : 0;
                return equalDimensions;
            }

            private static HashSet<string> ElementKeys(IEnumerable<WfReqElement> elements, ElemFieldType field)
            {
                return [.. elements.Where(element => element.Field == field.ToString() && IsMeaningfulElement(element)).Select(ElementKey)];
            }

            private static bool IsMeaningfulElement(WfReqElement element)
            {
                return element.NetworkId.HasValue
                    || element.ServiceId.HasValue
                    || element.FlowNetworkObjectId.HasValue
                    || element.FlowNetworkGroupId.HasValue
                    || element.FlowServiceObjectId.HasValue
                    || element.FlowServiceGroupId.HasValue
                    || !string.IsNullOrWhiteSpace(element.IpString)
                    || !string.IsNullOrWhiteSpace(element.Cidr?.CidrString)
                    || !string.IsNullOrWhiteSpace(element.IpEnd)
                    || !string.IsNullOrWhiteSpace(element.CidrEnd?.CidrString)
                    || element.ProtoId.HasValue
                    || element.Port.HasValue
                    || element.PortEnd.HasValue
                    || !string.IsNullOrWhiteSpace(element.Name)
                    || !string.IsNullOrWhiteSpace(element.GroupName);
            }

            private static string ElementKey(WfReqElement element)
            {
                return string.Join("|",
                [
                    element.Field,
                    element.NetworkId?.ToString() ?? "",
                    element.ServiceId?.ToString() ?? "",
                    element.FlowNetworkObjectId?.ToString() ?? "",
                    element.FlowNetworkGroupId?.ToString() ?? "",
                    element.FlowServiceObjectId?.ToString() ?? "",
                    element.FlowServiceGroupId?.ToString() ?? "",
                    element.IpString ?? element.Cidr?.CidrString ?? "",
                    element.IpEnd ?? element.CidrEnd?.CidrString ?? "",
                    element.ProtoId?.ToString() ?? "",
                    element.Port?.ToString() ?? "",
                    element.PortEnd?.ToString() ?? "",
                    element.Name ?? "",
                    element.GroupName ?? "",
                    element.RequestAction
                ]);
            }
        }
    }
}

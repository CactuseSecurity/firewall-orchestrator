using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Compliance
{
    public class ComplianceRequestedRulePolicyChecker(UserConfig userConfig, ApiConnection apiConnection) : IRequestedRulePolicyChecker
    {
        public async Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
        {
            List<int> selectedPolicyIds = policyIds.Where(id => id > 0).Distinct().ToList();
            List<Rule> rules = await BuildRulesFromRequestTasks(requestTasks);
            if (selectedPolicyIds.Count == 0 || rules.Count == 0)
            {
                return false;
            }

            ComplianceCheck complianceCheck = new(userConfig, apiConnection);
            return await complianceCheck.AreRulesCompliant(selectedPolicyIds, rules);
        }

        private async Task<List<Rule>> BuildRulesFromRequestTasks(IEnumerable<WfReqTask> requestTasks)
        {
            List<WfReqTask> tasks = requestTasks.ToList();
            Dictionary<string, List<NwObjectElement>> networkGroupMembers = BuildNetworkGroupMembers(tasks);
            Dictionary<string, List<NwServiceElement>> serviceGroupMembers = BuildServiceGroupMembers(tasks);
            await AddFlowNetworkGroupMembers(tasks, networkGroupMembers);
            await AddFlowServiceGroupMembers(tasks, serviceGroupMembers);
            List<Rule> rules = [];

            foreach (WfReqTask task in tasks
                .Where(task => !string.Equals(task.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase))
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0)
                .Where(task => task.GetNwObjectElements(ElemFieldType.destination).Count > 0)
                .Where(task => task.GetServiceElements().Count > 0))
            {
                Rule? rule = BuildRuleFromRequestTask(task, networkGroupMembers, serviceGroupMembers);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }

            return rules;
        }

        private async Task AddFlowNetworkGroupMembers(IEnumerable<WfReqTask> tasks,
            Dictionary<string, List<NwObjectElement>> networkGroupMembers)
        {
            List<NwObjectElement> groupElements = tasks
                .SelectMany(task => task.GetNwObjectElements(ElemFieldType.source).Concat(task.GetNwObjectElements(ElemFieldType.destination)))
                .Where(element => !string.IsNullOrWhiteSpace(element.GroupName))
                .Where(element => !networkGroupMembers.ContainsKey(element.GroupName))
                .ToList();
            if (groupElements.Count == 0)
            {
                return;
            }

            HashSet<long> groupIds = groupElements
                .Where(element => element.FlowNetworkGroupId.HasValue)
                .Select(element => element.FlowNetworkGroupId!.Value)
                .ToHashSet();
            HashSet<string> groupNames = groupElements.Select(element => element.GroupName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<FlowNwGroup> groups = await apiConnection.SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, new { mgmId = 0 }) ?? [];
            List<FlowNwObject> objects = await apiConnection.SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, new { mgmId = 0 }) ?? [];
            Dictionary<long, FlowNwObject> objectsById = objects.ToDictionary(obj => obj.Id);
            foreach (FlowNwGroup group in groups.Where(group => groupIds.Contains(group.Id) || groupNames.Contains(group.Name)))
            {
                networkGroupMembers[group.Name] = group.NwGroupMembers
                    .Where(member => objectsById.ContainsKey(member.NwObjectId))
                    .Select(member => ToNetworkElement(objectsById[member.NwObjectId]))
                    .ToList();
            }
        }

        private async Task AddFlowServiceGroupMembers(IEnumerable<WfReqTask> tasks,
            Dictionary<string, List<NwServiceElement>> serviceGroupMembers)
        {
            List<NwServiceElement> groupElements = tasks
                .SelectMany(task => task.GetServiceElements())
                .Where(element => !string.IsNullOrWhiteSpace(element.GroupName))
                .Where(element => !serviceGroupMembers.ContainsKey(element.GroupName!))
                .ToList();
            if (groupElements.Count == 0)
            {
                return;
            }

            HashSet<long> groupIds = groupElements
                .Where(element => element.FlowServiceGroupId.HasValue)
                .Select(element => element.FlowServiceGroupId!.Value)
                .ToHashSet();
            HashSet<string> groupNames = groupElements.Select(element => element.GroupName!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<FlowSvcGroup> groups = await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, new { mgmId = 0 }) ?? [];
            List<FlowSvcObject> objects = await apiConnection.SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, new { mgmId = 0 }) ?? [];
            Dictionary<long, FlowSvcObject> objectsById = objects.ToDictionary(obj => obj.Id);
            foreach (FlowSvcGroup group in groups.Where(group => groupIds.Contains(group.Id) || groupNames.Contains(group.Name)))
            {
                serviceGroupMembers[group.Name] = group.SvcGroupMembers
                    .Where(member => objectsById.ContainsKey(member.SvcObjectId))
                    .Select(member => ToServiceElement(objectsById[member.SvcObjectId]))
                    .ToList();
            }
        }

        private static NwObjectElement ToNetworkElement(FlowNwObject flowObject)
        {
            return new NwObjectElement
            {
                Name = flowObject.Name,
                IpString = flowObject.IpStart ?? "",
                IpEndString = flowObject.IpEnd ?? ""
            };
        }

        private static NwServiceElement ToServiceElement(FlowSvcObject flowObject)
        {
            return new NwServiceElement
            {
                Name = flowObject.Name,
                Port = flowObject.PortStart,
                PortEnd = flowObject.PortEnd,
                ProtoId = flowObject.ProtoId
            };
        }

        private static Rule? BuildRuleFromRequestTask(WfReqTask requestTask,
            IReadOnlyDictionary<string, List<NwObjectElement>> networkGroupMembers,
            IReadOnlyDictionary<string, List<NwServiceElement>> serviceGroupMembers)
        {
            List<NetworkLocation> froms = ExpandNetworkElements(requestTask.GetNwObjectElements(ElemFieldType.source), networkGroupMembers)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<NetworkLocation> tos = ExpandNetworkElements(requestTask.GetNwObjectElements(ElemFieldType.destination), networkGroupMembers)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<ServiceWrapper> services = ExpandServiceElements(requestTask.GetServiceElements(), serviceGroupMembers)
                .Select(BuildService)
                .Where(service => service != null)
                .Cast<ServiceWrapper>()
                .ToList();

            if (froms.Count == 0 || tos.Count == 0 || services.Count == 0)
            {
                return null;
            }

            return new Rule()
            {
                Uid = requestTask.GetRuleElements()
                    .Select(rule => rule.RuleUid)
                    .FirstOrDefault(uid => !string.IsNullOrWhiteSpace(uid)) ?? "",
                MgmtId = requestTask.ManagementId ?? 0,
                Name = requestTask.Title,
                Action = GetRuleAction(requestTask),
                Froms = [.. froms],
                Tos = [.. tos],
                Services = [.. services]
            };
        }

        private static Dictionary<string, List<NwObjectElement>> BuildNetworkGroupMembers(IEnumerable<WfReqTask> tasks)
        {
            Dictionary<string, List<NwObjectElement>> groups = new(StringComparer.OrdinalIgnoreCase);
            foreach (WfReqTask task in tasks.Where(IsGroupTask))
            {
                string groupName = task.GetAddInfoValue(AdditionalInfoKeys.GrpName);
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    groups[groupName] = task.GetNwObjectElements(ElemFieldType.source);
                }
            }
            return groups;
        }

        private static Dictionary<string, List<NwServiceElement>> BuildServiceGroupMembers(IEnumerable<WfReqTask> tasks)
        {
            Dictionary<string, List<NwServiceElement>> groups = new(StringComparer.OrdinalIgnoreCase);
            foreach (WfReqTask task in tasks.Where(IsGroupTask))
            {
                string groupName = task.GetAddInfoValue(AdditionalInfoKeys.GrpName);
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    groups[groupName] = task.GetServiceElements();
                }
            }
            return groups;
        }

        private static IEnumerable<NwObjectElement> ExpandNetworkElements(IEnumerable<NwObjectElement> elements,
            IReadOnlyDictionary<string, List<NwObjectElement>> networkGroupMembers)
        {
            foreach (NwObjectElement element in elements.Where(IsRequestedElementActive))
            {
                if (string.IsNullOrWhiteSpace(element.GroupName))
                {
                    yield return element;
                }
                else if (networkGroupMembers.TryGetValue(element.GroupName, out List<NwObjectElement>? members))
                {
                    foreach (NwObjectElement member in members.Where(IsRequestedElementActive))
                    {
                        yield return member;
                    }
                }
            }
        }

        private static IEnumerable<NwServiceElement> ExpandServiceElements(IEnumerable<NwServiceElement> elements,
            IReadOnlyDictionary<string, List<NwServiceElement>> serviceGroupMembers)
        {
            foreach (NwServiceElement element in elements.Where(IsRequestedElementActive))
            {
                if (string.IsNullOrWhiteSpace(element.GroupName))
                {
                    yield return element;
                }
                else if (serviceGroupMembers.TryGetValue(element.GroupName, out List<NwServiceElement>? members))
                {
                    foreach (NwServiceElement member in members.Where(IsRequestedElementActive))
                    {
                        yield return member;
                    }
                }
            }
        }

        private static bool IsGroupTask(WfReqTask task)
        {
            return (task.TaskType == WfTaskType.group_create.ToString()
                || task.TaskType == WfTaskType.group_modify.ToString())
                && !string.IsNullOrWhiteSpace(task.GetAddInfoValue(AdditionalInfoKeys.GrpName));
        }

        private static NetworkLocation? BuildNetworkLocation(NwObjectElement element)
        {
            if (string.IsNullOrWhiteSpace(element.IpString))
            {
                return null;
            }

            NetworkObject inlineObject = new()
            {
                Name = element.Name ?? "",
                IP = element.IpString,
                IpEnd = element.IpEndString
            };
            inlineObject.Type = new NetworkObjectType()
            {
                Name = !string.IsNullOrWhiteSpace(element.IpEndString) && !string.Equals(element.IpString, element.IpEndString, StringComparison.Ordinal)
                    ? ObjectType.IPRange
                    : ObjectType.Network
            };
            return new NetworkLocation(new(), inlineObject);
        }

        private static ServiceWrapper? BuildService(NwServiceElement element)
        {
            if (element.Port is null or 0 && element.ProtoId == 0)
            {
                return null;
            }

            return new ServiceWrapper()
            {
                Content = new NetworkService()
                {
                    Name = element.Name ?? "",
                    DestinationPort = element.Port,
                    DestinationPortEnd = element.PortEnd,
                    ProtoId = element.ProtoId
                }
            };
        }

        private static bool IsRequestedElementActive(NwObjectElement element)
        {
            return !string.Equals(element.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRequestedElementActive(NwServiceElement element)
        {
            return !string.Equals(element.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRuleAction(WfReqTask requestTask)
        {
            if (string.Equals(requestTask.TaskType, WfTaskType.rule_delete.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestTask.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase))
            {
                return RuleActions.Drop;
            }

            return RuleActions.Accept;
        }
    }

    public sealed class ComplianceRequestedRulePolicyCheckerFactory : IRequestedRulePolicyCheckerFactory
    {
        public IRequestedRulePolicyChecker Create(UserConfig userConfig, ApiConnection apiConnection)
        {
            return new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection);
        }
    }
}

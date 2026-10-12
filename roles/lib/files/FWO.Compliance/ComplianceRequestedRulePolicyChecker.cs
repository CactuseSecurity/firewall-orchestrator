using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Logging;
using FWO.Services.Workflow;
using Microsoft.Extensions.DependencyInjection;
using RestSharp;

namespace FWO.Compliance
{
    public class ComplianceRequestedRulePolicyChecker(UserConfig userConfig, ApiConnection apiConnection, MiddlewareClient? middlewareClient = null) : IRequestedRulePolicyChecker
    {
        // This fallback supports library consumers that do not register a resolver or middleware client.
        private const int kNoManagementFilter = 0;
        private readonly IFlowGroupResolver? flowGroupResolver = middlewareClient == null
            ? FWO.Services.ServiceProvider.Services?.GetService<IFlowGroupResolver>()
            : null;
        public async Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
        {
            List<int> selectedPolicyIds = policyIds.Where(id => id > 0).Distinct().ToList();
            RuleBuildResult assessment = await BuildRuleAssessmentFromRequestTasks(requestTasks);
            if (selectedPolicyIds.Count == 0 || assessment.Rules.Count == 0 || assessment.HasUnassessableTasks)
            {
                return false;
            }

            ComplianceCheck complianceCheck = new(userConfig, apiConnection);
            return await complianceCheck.AreRulesCompliant(selectedPolicyIds, assessment.Rules);
        }

        private async Task<RuleBuildResult> BuildRuleAssessmentFromRequestTasks(IEnumerable<WfReqTask> requestTasks)
        {
            List<WfReqTask> tasks = requestTasks.ToList();
            Dictionary<string, List<NwObjectElement>> networkGroupMembers = BuildNetworkGroupMembers(tasks);
            Dictionary<string, List<NwServiceElement>> serviceGroupMembers = BuildServiceGroupMembers(tasks);
            await AddFlowNetworkGroupMembers(tasks, networkGroupMembers);
            await AddFlowServiceGroupMembers(tasks, serviceGroupMembers);
            List<Rule> rules = [];
            bool hasUnassessableTasks = false;

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
                else if (HasUnresolvedGroup(task, networkGroupMembers, serviceGroupMembers))
                {
                    return new RuleBuildResult([], true);
                }
                else
                {
                    hasUnassessableTasks = true;
                    Log.WriteWarning("ComplianceRequestedRulePolicyChecker",
                        $"Skipping request task {task.Id} because it contains elements that cannot be mapped to a technical rule.");
                }
            }

            return new RuleBuildResult(rules, hasUnassessableTasks);
        }

        private sealed record RuleBuildResult(List<Rule> Rules, bool HasUnassessableTasks);

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
            HashSet<string> groupNames = groupElements
                .Where(element => !element.FlowNetworkGroupId.HasValue)
                .Select(element => element.GroupName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Dictionary<long, HashSet<string>> requestedNamesById = groupElements
                .Where(element => element.FlowNetworkGroupId.HasValue)
                .GroupBy(element => element.FlowNetworkGroupId!.Value)
                .ToDictionary(group => group.Key, group => group.Select(element => element.GroupName).ToHashSet(StringComparer.OrdinalIgnoreCase));
            HashSet<string> namesWithIds = requestedNamesById.Values.SelectMany(names => names).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (middlewareClient != null || flowGroupResolver != null)
            {
                FlowGroupResolutionResult resolution = flowGroupResolver != null
                    ? await flowGroupResolver.ResolveFlowGroupMembersAsync(new FlowGroupResolutionParameters
                    {
                        NetworkGroupIds = groupIds.ToList(),
                        NetworkGroupNames = groupNames.ToList()
                    })
                    : await ResolveFlowGroupsThroughMiddleware(middlewareClient!, groupIds, groupNames, true);
                foreach (FlowNetworkGroupResolution group in resolution.NetworkGroups)
                {
                    List<NwObjectElement> members = group.Members
                        .Select(ToNetworkElement)
                        .ToList();
                    foreach (string key in GetResolvedGroupKeys(group.Id, group.Name, requestedNamesById, namesWithIds))
                    {
                        MergeNetworkGroupMembers(networkGroupMembers, key, members);
                    }
                }
                return;
            }

            Task<List<FlowNwGroup>> groupsTask = apiConnection.SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, new { mgmId = kNoManagementFilter });
            Task<List<FlowNwObject>> objectsTask = apiConnection.SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, new { mgmId = kNoManagementFilter });
            await Task.WhenAll(groupsTask, objectsTask);
            List<FlowNwGroup> groups = await groupsTask ?? [];
            List<FlowNwObject> objects = await objectsTask ?? [];
            Dictionary<long, FlowNwObject> objectsById = objects
                .Where(IsActiveAndVisible)
                .ToDictionary(obj => obj.Id);
            foreach (FlowNwGroup group in ResolveFlowGroups(groups, groupIds, groupNames))
            {
                List<NwObjectElement> members = group.NwGroupMembers
                    .Where(member => objectsById.ContainsKey(member.NwObjectId))
                    .Select(member => ToNetworkElement(objectsById[member.NwObjectId]))
                    .ToList();
                foreach (string key in GetResolvedGroupKeys(group.Id, group.Name, requestedNamesById, namesWithIds))
                {
                    MergeNetworkGroupMembers(networkGroupMembers, key, members);
                }
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
            HashSet<string> groupNames = groupElements
                .Where(element => !element.FlowServiceGroupId.HasValue)
                .Select(element => element.GroupName!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Dictionary<long, HashSet<string>> requestedNamesById = groupElements
                .Where(element => element.FlowServiceGroupId.HasValue)
                .GroupBy(element => element.FlowServiceGroupId!.Value)
                .ToDictionary(group => group.Key, group => group.Select(element => element.GroupName!).ToHashSet(StringComparer.OrdinalIgnoreCase));
            HashSet<string> namesWithIds = requestedNamesById.Values.SelectMany(names => names).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (middlewareClient != null || flowGroupResolver != null)
            {
                FlowGroupResolutionResult resolution = flowGroupResolver != null
                    ? await flowGroupResolver.ResolveFlowGroupMembersAsync(new FlowGroupResolutionParameters
                    {
                        ServiceGroupIds = groupIds.ToList(),
                        ServiceGroupNames = groupNames.ToList()
                    })
                    : await ResolveFlowGroupsThroughMiddleware(middlewareClient!, groupIds, groupNames, false);
                foreach (FlowServiceGroupResolution group in resolution.ServiceGroups)
                {
                    List<NwServiceElement> members = group.Members
                        .Select(ToServiceElement)
                        .ToList();
                    foreach (string key in GetResolvedGroupKeys(group.Id, group.Name, requestedNamesById, namesWithIds))
                    {
                        MergeServiceGroupMembers(serviceGroupMembers, key, members);
                    }
                }
                return;
            }

            Task<List<FlowSvcGroup>> groupsTask = apiConnection.SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, new { mgmId = kNoManagementFilter });
            Task<List<FlowSvcObject>> objectsTask = apiConnection.SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, new { mgmId = kNoManagementFilter });
            await Task.WhenAll(groupsTask, objectsTask);
            List<FlowSvcGroup> groups = await groupsTask ?? [];
            List<FlowSvcObject> objects = await objectsTask ?? [];
            Dictionary<long, FlowSvcObject> objectsById = objects
                .Where(IsActiveAndVisible)
                .ToDictionary(obj => obj.Id);
            foreach (FlowSvcGroup group in ResolveFlowGroups(groups, groupIds, groupNames))
            {
                List<NwServiceElement> members = group.SvcGroupMembers
                    .Where(member => objectsById.ContainsKey(member.SvcObjectId))
                    .Select(member => ToServiceElement(objectsById[member.SvcObjectId]))
                    .ToList();
                foreach (string key in GetResolvedGroupKeys(group.Id, group.Name, requestedNamesById, namesWithIds))
                {
                    MergeServiceGroupMembers(serviceGroupMembers, key, members);
                }
            }
        }

        private static bool IsActive(FlowNwObject flowObject)
        {
            return !string.Equals(flowObject.State, FlowState.Removed, StringComparison.OrdinalIgnoreCase) && flowObject.RemovedDate == null;
        }

        private static bool IsActiveAndVisible(FlowNwObject flowObject)
        {
            return IsActive(flowObject)
                && flowObject.ShowInRequestModule
                && !string.Equals(flowObject.State, FlowState.Denied, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActive(FlowSvcObject flowObject)
        {
            return !string.Equals(flowObject.State, FlowState.Removed, StringComparison.OrdinalIgnoreCase) && flowObject.RemovedDate == null;
        }

        private static bool IsActiveAndVisible(FlowSvcObject flowObject)
        {
            return IsActive(flowObject)
                && flowObject.ShowInRequestModule
                && !string.Equals(flowObject.State, FlowState.Denied, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureSuccessful(RestResponse<FlowGroupResolutionResult> response)
        {
            if (!response.IsSuccessful || response.Data == null)
            {
                string details = response.ErrorException?.Message ?? response.ErrorMessage ?? response.StatusCode.ToString();
                throw new InvalidOperationException($"Flow group resolution failed: {details}");
            }
        }

        private static async Task<FlowGroupResolutionResult> ResolveFlowGroupsThroughMiddleware(
            MiddlewareClient middlewareClient, IEnumerable<long> groupIds, IEnumerable<string> groupNames, bool networkGroups)
        {
            List<FlowGroupResolutionResult> results = [];
            foreach (long[] idChunk in groupIds.Chunk(FlowGroupResolutionParameters.MaxSelectors))
            {
                RestResponse<FlowGroupResolutionResult> response = await middlewareClient.ResolveFlowGroupMembers(new()
                {
                    NetworkGroupIds = networkGroups ? idChunk.ToList() : [],
                    ServiceGroupIds = networkGroups ? [] : idChunk.ToList()
                });
                EnsureSuccessful(response);
                results.Add(response.Data!);
            }

            foreach (string[] nameChunk in groupNames.Chunk(FlowGroupResolutionParameters.MaxSelectors))
            {
                RestResponse<FlowGroupResolutionResult> response = await middlewareClient.ResolveFlowGroupMembers(new()
                {
                    NetworkGroupNames = networkGroups ? nameChunk.ToList() : [],
                    ServiceGroupNames = networkGroups ? [] : nameChunk.ToList()
                });
                EnsureSuccessful(response);
                results.Add(response.Data!);
            }

            return new FlowGroupResolutionResult
            {
                NetworkGroups = results.SelectMany(result => result.NetworkGroups).ToList(),
                ServiceGroups = results.SelectMany(result => result.ServiceGroups).ToList()
            };
        }

        private static HashSet<string> GetResolvedGroupKeys(long groupId, string? groupName,
            Dictionary<long, HashSet<string>> requestedNamesById, HashSet<string> namesWithIds)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return [];
            }

            if (requestedNamesById.TryGetValue(groupId, out HashSet<string>? requestedNames))
            {
                HashSet<string> resolvedKeys = new(requestedNames, StringComparer.OrdinalIgnoreCase)
                {
                    groupName
                };
                return resolvedKeys;
            }

            return namesWithIds.Contains(groupName) ? [] : [groupName];
        }

        private static IEnumerable<TGroup> ResolveFlowGroups<TGroup>(IEnumerable<TGroup> groups, HashSet<long> groupIds, HashSet<string> groupNames)
            where TGroup : FlowGroup
        {
            List<TGroup> activeGroups = groups
                .Where(group => !string.IsNullOrWhiteSpace(group.Name))
                .Where(group => !string.Equals(group.State, FlowState.Removed, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(group.State, FlowState.Denied, StringComparison.OrdinalIgnoreCase)
                    && group.ShowInRequestModule
                    && group.RemovedDate == null)
                .ToList();
            List<TGroup> idMatches = activeGroups.Where(group => groupIds.Contains(group.Id)).ToList();
            foreach (TGroup group in idMatches)
            {
                yield return group;
            }

            foreach (string groupName in groupNames)
            {
                if (idMatches.Any(group => string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                List<TGroup> nameMatches = activeGroups
                    .Where(group => string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (nameMatches.Count == 1)
                {
                    yield return nameMatches[0];
                }
            }
        }

        private static NwObjectElement ToNetworkElement(FlowNwObject flowObject)
        {
            return new NwObjectElement
            {
                Name = flowObject.Name,
                IpString = flowObject.IpStart ?? "",
                IpEndString = flowObject.IpEnd ?? "",
                FlowNetworkObjectId = flowObject.Id
            };
        }

        private static NwObjectElement ToNetworkElement(FlowNetworkMemberResolution member)
        {
            return new NwObjectElement
            {
                Name = member.Name,
                IpString = member.IpStart,
                IpEndString = member.IpEnd,
                FlowNetworkObjectId = member.Id
            };
        }

        private static NwServiceElement ToServiceElement(FlowSvcObject flowObject)
        {
            return new NwServiceElement
            {
                Name = flowObject.Name,
                Port = flowObject.PortStart,
                PortEnd = flowObject.PortEnd,
                ProtoId = flowObject.ProtoId,
                FlowServiceObjectId = flowObject.Id
            };
        }

        private static NwServiceElement ToServiceElement(FlowServiceMemberResolution member)
        {
            return new NwServiceElement
            {
                Name = member.Name,
                Port = member.PortStart,
                PortEnd = member.PortEnd,
                ProtoId = member.ProtoId,
                FlowServiceObjectId = member.Id
            };
        }

        private static Rule? BuildRuleFromRequestTask(WfReqTask requestTask,
            IReadOnlyDictionary<string, List<NwObjectElement>> networkGroupMembers,
            IReadOnlyDictionary<string, List<NwServiceElement>> serviceGroupMembers)
        {
            List<NwObjectElement> sourceElements = requestTask.GetNwObjectElements(ElemFieldType.source);
            List<NwObjectElement> destinationElements = requestTask.GetNwObjectElements(ElemFieldType.destination);
            List<NwServiceElement> serviceElements = requestTask.GetServiceElements();
            if (HasUnresolvedGroup(sourceElements, networkGroupMembers)
                || HasUnresolvedGroup(destinationElements, networkGroupMembers)
                || HasUnresolvedGroup(serviceElements, serviceGroupMembers))
            {
                return null;
            }

            List<NetworkLocation> froms = ExpandNetworkElements(sourceElements, networkGroupMembers)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<NetworkLocation> tos = ExpandNetworkElements(destinationElements, networkGroupMembers)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<ServiceWrapper> services = ExpandServiceElements(serviceElements, serviceGroupMembers)
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

        private static bool HasUnresolvedGroup(IEnumerable<NwObjectElement> elements, IReadOnlyDictionary<string, List<NwObjectElement>> groups)
        {
            return elements.Where(IsRequestedElementActive).Any(element =>
                !string.IsNullOrWhiteSpace(element.GroupName) && !groups.ContainsKey(element.GroupName));
        }

        private static bool HasUnresolvedGroup(IEnumerable<NwServiceElement> elements, IReadOnlyDictionary<string, List<NwServiceElement>> groups)
        {
            return elements.Where(IsRequestedElementActive).Any(element =>
                !string.IsNullOrWhiteSpace(element.GroupName) && !groups.ContainsKey(element.GroupName));
        }

        private static bool HasUnresolvedGroup(WfReqTask task,
            IReadOnlyDictionary<string, List<NwObjectElement>> networkGroups,
            IReadOnlyDictionary<string, List<NwServiceElement>> serviceGroups)
        {
            return HasUnresolvedGroup(task.GetNwObjectElements(ElemFieldType.source), networkGroups)
                || HasUnresolvedGroup(task.GetNwObjectElements(ElemFieldType.destination), networkGroups)
                || HasUnresolvedGroup(task.GetServiceElements(), serviceGroups);
        }

        private static Dictionary<string, List<NwObjectElement>> BuildNetworkGroupMembers(IEnumerable<WfReqTask> tasks)
        {
            Dictionary<string, List<NwObjectElement>> groups = new(StringComparer.OrdinalIgnoreCase);
            foreach (WfReqTask task in tasks.Where(IsGroupTask)
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0))
            {
                string groupName = task.GetAddInfoValue(AdditionalInfoKeys.GrpName);
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    MergeNetworkGroupMembers(groups, groupName, task.GetNwObjectElements(ElemFieldType.source));
                }
            }
            return groups;
        }

        private static Dictionary<string, List<NwServiceElement>> BuildServiceGroupMembers(IEnumerable<WfReqTask> tasks)
        {
            Dictionary<string, List<NwServiceElement>> groups = new(StringComparer.OrdinalIgnoreCase);
            foreach (WfReqTask task in tasks.Where(IsGroupTask)
                .Where(task => task.GetServiceElements().Count > 0))
            {
                string groupName = task.GetAddInfoValue(AdditionalInfoKeys.GrpName);
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    MergeServiceGroupMembers(groups, groupName, task.GetServiceElements());
                }
            }
            return groups;
        }

        private static void MergeNetworkGroupMembers(Dictionary<string, List<NwObjectElement>> groups, string groupName, IEnumerable<NwObjectElement> members)
        {
            if (!groups.TryGetValue(groupName, out List<NwObjectElement>? existing))
            {
                groups[groupName] = members.ToList();
                return;
            }

            foreach (NwObjectElement member in members)
            {
                NwObjectElement? duplicate = existing.FirstOrDefault(item => item.FlowNetworkObjectId == member.FlowNetworkObjectId
                    && string.Equals(item.Name, member.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.IpString, member.IpString, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.IpEndString, member.IpEndString, StringComparison.OrdinalIgnoreCase));
                if (duplicate == null)
                {
                    existing.Add(member);
                }
                else if (!IsRequestedElementActive(duplicate) && IsRequestedElementActive(member))
                {
                    existing[existing.IndexOf(duplicate)] = member;
                }
            }
        }

        private static void MergeServiceGroupMembers(Dictionary<string, List<NwServiceElement>> groups, string groupName, IEnumerable<NwServiceElement> members)
        {
            if (!groups.TryGetValue(groupName, out List<NwServiceElement>? existing))
            {
                groups[groupName] = members.ToList();
                return;
            }

            foreach (NwServiceElement member in members)
            {
                NwServiceElement? duplicate = existing.FirstOrDefault(item => item.FlowServiceObjectId == member.FlowServiceObjectId
                    && string.Equals(item.Name, member.Name, StringComparison.OrdinalIgnoreCase)
                    && item.Port == member.Port && item.PortEnd == member.PortEnd && item.ProtoId == member.ProtoId);
                if (duplicate == null)
                {
                    existing.Add(member);
                }
                else if (!IsRequestedElementActive(duplicate) && IsRequestedElementActive(member))
                {
                    existing[existing.IndexOf(duplicate)] = member;
                }
            }
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
        /// <summary>
        /// Creates the compliance-backed requested-rule policy checker.
        /// </summary>
        public IRequestedRulePolicyChecker Create(UserConfig userConfig, ApiConnection apiConnection, MiddlewareClient? middlewareClient = null)
        {
            return new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection, middlewareClient);
        }
    }
}

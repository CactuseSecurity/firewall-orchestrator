using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Compliance
{
    public class ComplianceRequestedRulePolicyChecker(UserConfig userConfig, ApiConnection apiConnection) : IRequestedRulePolicyChecker
    {
        public async Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
        {
            List<int> selectedPolicyIds = policyIds.Where(id => id > 0).Distinct().ToList();
            List<Rule> rules = BuildRulesFromRequestTasks(requestTasks);
            if (selectedPolicyIds.Count == 0 || rules.Count == 0)
            {
                return false;
            }

            ComplianceCheck complianceCheck = new(userConfig, apiConnection);
            return await complianceCheck.AreRulesCompliant(selectedPolicyIds, rules);
        }

        private static List<Rule> BuildRulesFromRequestTasks(IEnumerable<WfReqTask> requestTasks)
        {
            List<Rule> rules = [];

            foreach (WfReqTask task in requestTasks
                .Where(task => task.ManagementId != null)
                .Where(task => !string.Equals(task.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase))
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0)
                .Where(task => task.GetNwObjectElements(ElemFieldType.destination).Count > 0)
                .Where(task => task.GetServiceElements().Count > 0))
            {
                Rule? rule = BuildRuleFromRequestTask(task);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }

            return rules;
        }

        private static Rule? BuildRuleFromRequestTask(WfReqTask requestTask)
        {
            if (requestTask.ManagementId == null)
            {
                return null;
            }

            List<NetworkLocation> froms = requestTask.GetNwObjectElements(ElemFieldType.source)
                .Where(IsRequestedElementActive)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<NetworkLocation> tos = requestTask.GetNwObjectElements(ElemFieldType.destination)
                .Where(IsRequestedElementActive)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<ServiceWrapper> services = requestTask.GetServiceElements()
                .Where(IsRequestedElementActive)
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
                MgmtId = requestTask.ManagementId.Value,
                Uid = requestTask.GetRuleElements()
                    .Select(rule => rule.RuleUid)
                    .FirstOrDefault(uid => !string.IsNullOrWhiteSpace(uid)) ?? "",
                Name = requestTask.Title,
                Action = GetRuleAction(requestTask),
                Froms = [.. froms],
                Tos = [.. tos],
                Services = [.. services]
            };
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
            if (element.Port == 0 && element.ProtoId == 0)
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
}

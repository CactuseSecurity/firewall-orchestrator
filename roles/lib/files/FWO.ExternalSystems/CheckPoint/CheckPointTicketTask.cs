using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using NetTools;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.CheckPoint
{
    public class CheckPointTicketTask : ExternalTicketTask
    {
        public JsonNode TaskBody { get; private set; } = default!;
        public CheckPointTicketTask(WfReqTask reqTask, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention) : base(reqTask, ipProtos, namingConvention)
        { }

        public override void FillTaskText(ExternalTicketTemplate template)
        {
            ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement.ExtMgtData != null ? JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement.ExtMgtData) : new();

            string managementId = extMgt.ExtId ?? ReqTask.ManagementId?.ToString() ?? "0";
            string managementName = extMgt.ExtName ?? ReqTask.OnManagement?.Name ?? "";

            TaskText = (string.IsNullOrWhiteSpace(template.TasksTemplate) ? template.TicketTemplate : template.TasksTemplate)
                .Replace(Placeholder.ORDERNAME, "AR" + ReqTask.TaskNumber.ToString())
                .Replace(Placeholder.TASKCOMMENT, ReqTask.GetFirstCommentText())
                .Replace(Placeholder.REASON, ReqTask.Reason)
                .Replace(Placeholder.ACTION, MapActionType())
                .Replace(Placeholder.CHANGEACTION, MapChangeAction())
                .Replace(Placeholder.GROUPNAME, ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName))
                .Replace(Placeholder.MANAGEMENT_ID, managementId)
                .Replace(Placeholder.MANAGEMENT_NAME, managementName)
                .Replace(Placeholder.MEMBERS, "[]");

            if (!string.IsNullOrWhiteSpace(TaskText))
            {
                TaskBody = JsonNode.Parse(TaskText) ?? throw new ConfigException("TaskText could not be parsed into valid JSON.");
            }

        }

        private string MapActionType()
        {
            return ReqTask.TaskType switch
            {
                nameof(WfTaskType.rule_delete) => RuleActions.Drop,
                _ => RuleActions.Accept
            };
        }

        private string MapChangeAction()
        {
            return ReqTask.TaskType switch
            {
                nameof(WfTaskType.group_create) => "CREATE",
                nameof(WfTaskType.group_delete) => "DELETE",
                _ => "UPDATE"
            };
        }

        internal JsonNode RenderEmptyGroupCreateBody()
        {
            return new JsonObject
            {
                ["name"] = ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName)
            };
        }

        internal JsonNode RenderGroupMembersAddBody(List<string> memberNames)
        {
            JsonArray membersToAdd = new();
            foreach (string memberName in memberNames)
            {
                membersToAdd.Add(memberName);
            }

            return new JsonObject
            {
                ["name"] = ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName),
                ["members"] = new JsonObject
                {
                    ["add"] = membersToAdd
                }
            };
        }

        internal JsonNode RenderGroupMembersRemoveBody(List<string> memberNames)
        {
            JsonArray membersToRemove = new();
            foreach (string memberName in memberNames)
            {
                membersToRemove.Add(memberName);
            }

            return new JsonObject
            {
                ["name"] = ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName),
                ["members"] = new JsonObject
                {
                    ["remove"] = membersToRemove
                }
            };
        }

        internal List<CheckPointObjectRequest> GetRequiredMemberObjectSteps()
        {
            return ReqTask.Elements
                .Where(IsNetworkMember)
                .Where(RequiresMemberObjectStep)
                .Where(element => !string.IsNullOrWhiteSpace(element.Name))
                .Select(ToCheckPointObjectRequest)
                .ToList();
        }

        internal List<string> GetMembersToAdd()
        {
            return ReqTask.Elements
                .Where(IsNetworkMember)
                .Where(ShouldBeAddedToGroup)
                .Select(GetMemberName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal List<string> GetMembersToRemove()
        {
            return ReqTask.Elements
                .Where(IsNetworkMember)
                .Where(element => element.RequestAction == nameof(RequestAction.delete))
                .Select(GetMemberName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool ShouldBeAddedToGroup(WfReqElement element)
        {
            if (ReqTask.TaskType == nameof(WfTaskType.group_create))
            {
                return element.RequestAction == nameof(RequestAction.create)
                    || element.RequestAction == nameof(RequestAction.unchanged)
                    || element.RequestAction == nameof(RequestAction.addAfterCreation);
            }

            if (ReqTask.TaskType == nameof(WfTaskType.group_modify))
            {
                // "modify" is intentionally not treated as group membership add here.
                return element.RequestAction == nameof(RequestAction.create)
                    || element.RequestAction == nameof(RequestAction.addAfterCreation);
            }

            return false;
        }

        private static string GetMemberName(WfReqElement element)
        {
            return element.Name ?? "";
        }
       
        private static bool IsNetworkMember(WfReqElement element)
        {
            return element.Field != ElemFieldType.service.ToString()
                && element.Field != ElemFieldType.rule.ToString();
        }

        private static bool RequiresMemberObjectStep(WfReqElement element)
        {
            return element.RequestAction == nameof(RequestAction.create) || element.RequestAction == nameof(RequestAction.modify);
        }

        private CheckPointObjectRequest ToCheckPointObjectRequest(WfReqElement element)
        {
            string ipString = element.IpString ?? "";
            string ipEndString = element.IpEnd ?? "";

            string objectType = IpOperations.GetObjectType(ipString, ipEndString);
            IPAddressRange range = BuildRange(ipString, ipEndString, objectType);

            return new()
            {
                NetworkObjectType = objectType,
                Name = element.Name ?? "",
                RequestAction = element.RequestAction ?? nameof(RequestAction.create),
                Range = range,
                Comment = ""
            };
        }

        private static IPAddressRange BuildRange(string ipString, string ipEndString, string objectType)
        {
            if (string.IsNullOrWhiteSpace(ipString))
            {
                throw new ConfigException("Missing IP information for Check Point object request.");
            }

            if (objectType == ObjectType.IPRange)
            {
                string rangeStart = string.IsNullOrWhiteSpace(ipEndString)
                    ? IpOperations.SplitIpToRange(ipString).Item1
                    : ipString;

                string rangeEnd = string.IsNullOrWhiteSpace(ipEndString)
                    ? IpOperations.SplitIpToRange(ipString).Item2
                    : ipEndString;

                return new IPAddressRange(
                    IPAddress.Parse(rangeStart.StripOffNetmask()),
                    IPAddress.Parse(rangeEnd.StripOffNetmask()));
            }

            if (objectType == ObjectType.Network)
            {
                (string networkStart, string networkEnd) = IpOperations.SplitIpToRange(ipString);

                return new IPAddressRange(
                    IPAddress.Parse(networkStart.StripOffNetmask()),
                    IPAddress.Parse(networkEnd.StripOffNetmask()));
            }

            string hostIp = ipString.StripOffNetmask();
            IPAddress hostAddress = IPAddress.Parse(hostIp);
            return new IPAddressRange(hostAddress, hostAddress);
        }       
    }
}

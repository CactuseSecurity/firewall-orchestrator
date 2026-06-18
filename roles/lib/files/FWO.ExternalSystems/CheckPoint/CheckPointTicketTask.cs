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
                //.Replace(Placeholder.GROUPNAME, "AR" + ReqTask.TicketId.ToString())//
                .Replace(Placeholder.GROUPNAME, ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName))
                .Replace(Placeholder.MANAGEMENT_ID, managementId)
                .Replace(Placeholder.MANAGEMENT_NAME, managementName)
                .Replace(Placeholder.RULE_NUMBER, ReqTask.GetRuleDeviceId().ToString())
                .Replace(Placeholder.SOURCES, ConvertNetworkElems(template, ElemFieldType.source, managementId, managementName))
                .Replace(Placeholder.DESTINATIONS, ConvertNetworkElems(template, ElemFieldType.destination, managementId, managementName))
                //.Replace(Placeholder.MEMBERS, "[\"ChrisHost\"]")
                //.Replace(Placeholder.MEMBERS, "[\"AR" + ReqTask.TicketId.ToString() + "\"]") 
                .Replace(Placeholder.MEMBERS, "[]") //now delta tasks, not final group member list
                .Replace(Placeholder.SERVICES, ConvertServiceElems(template));

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

        private string GetGroupName()
        {
            return ReqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName);
        }

        internal JsonNode RenderEmptyGroupCreateBody()
        {
            return new JsonObject
            {
                ["name"] = GetGroupName()
            };
        }

        internal JsonNode RenderGroupMemberAddBody(string memberName)
        {
            return new JsonObject
            {
                ["name"] = GetGroupName(),
                ["members"] = new JsonObject
                {
                    ["add"] = new JsonArray(memberName)
                }
            };
        }

        internal JsonNode RenderGroupMemberRemoveBody(string memberName)
        {
            return new JsonObject
            {
                ["name"] = GetGroupName(),
                ["members"] = new JsonObject
                {
                    ["remove"] = new JsonArray(memberName)
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
                // TODO: If future workflow semantics require "modify" elements to also
                // trigger a group add, extend this condition here.
                return element.RequestAction == nameof(RequestAction.create)
                    || element.RequestAction == nameof(RequestAction.addAfterCreation);
            }

            return false;
        }

        private static string GetMemberName(WfReqElement element)
        {
            return element.Name ?? "";
        }

        private string ConvertNetworkElems(ExternalTicketTemplate template, ElemFieldType fieldType, string managementId, string managementName)
        {
            List<string> convertedElements = ReqTask.GetNwObjectElements(fieldType)
                .OrderBy(ResolveSortName, StringComparer.OrdinalIgnoreCase)
                .Select(element => ConvertNetworkElement(template, element, managementId, managementName))
                .ToList();
            return UsesNetworkSubTemplates(template) ? "[" + string.Join(",", convertedElements) + "]" : JsonSerializer.Serialize(convertedElements);
        }

        private string ConvertServiceElems(ExternalTicketTemplate template)
        {
            List<string> services = ReqTask.GetServiceElements()
                .Select(service => ConvertServiceElement(template, service))
                .Where(service => !string.IsNullOrWhiteSpace(service))
                .OrderBy(service => service, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return UsesServiceSubTemplates(template)
                ? "[" + string.Join(",", services) + "]"
                : JsonSerializer.Serialize(services);
        }

        private string ConvertNetworkElement(ExternalTicketTemplate template, NwObjectElement element, string managementId, string managementName)
        {
            string fallback = ConvertNetworkElement(element);

            if (!string.IsNullOrWhiteSpace(element.GroupName) && !string.IsNullOrWhiteSpace(template.NwObjGroupTemplate))
            {
                return FillNetworkGroupTemplate(template.NwObjGroupTemplate, element.GroupName, managementId, managementName);
            }

            if (!string.IsNullOrWhiteSpace(element.IpString) && !string.IsNullOrWhiteSpace(template.IpTemplate))
            {
                return FillIpTemplate(template.IpTemplate, element.IpString, element.IpEndString, fallback);
            }

            if (!string.IsNullOrWhiteSpace(template.ObjectTemplate))
            {
                return FillObjectTemplate(template.ObjectTemplate, element, fallback, managementId);
            }

            if (!string.IsNullOrWhiteSpace(template.ObjectTemplateShort))
            {
                return FillObjectShortTemplate(template.ObjectTemplateShort, fallback, element.RequestAction, managementId);
            }

            return UsesNetworkSubTemplates(template) || UsesObjectSubTemplates(template)
                ? JsonSerializer.Serialize(fallback)
                : fallback;
        }

        private static string ConvertNetworkElement(NwObjectElement element)
        {
            if (!string.IsNullOrWhiteSpace(element.GroupName))
            {
                return element.GroupName;
            }

            if (!string.IsNullOrWhiteSpace(element.Name))
            {
                return element.Name;
            }

            return element.IpString;
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

        private string ConvertServiceElement(ExternalTicketTemplate template, NwServiceElement service)
        {
            string fallback = ConvertServiceElement(service);
            string protocol = IpProtos.FirstOrDefault(proto => proto.Id == service.ProtoId)?.Name ?? service.ProtoId.ToString();
            if (IsIcmp(service, protocol) && !string.IsNullOrWhiteSpace(template.IcmpTemplate))
            {
                return template.IcmpTemplate
                    .Replace(Placeholder.SERVICENAME, JsonSafe(fallback))
                    .Replace(Placeholder.PROTOCOLNAME, protocol)
                    .Replace(Placeholder.PROTOCOLID, service.ProtoId.ToString());
            }
            if (service.Port > 0 && !string.IsNullOrWhiteSpace(template.ServiceTemplate))
            {
                return template.ServiceTemplate
                    .Replace(Placeholder.PROTOCOLNAME, protocol)
                    .Replace(Placeholder.PORT, FormatPort(service))
                    .Replace(Placeholder.SERVICENAME, JsonSafe(fallback));
            }
            if (!string.IsNullOrWhiteSpace(template.IpProtocolTemplate))
            {
                return template.IpProtocolTemplate
                    .Replace(Placeholder.PROTOCOLNAME, protocol)
                    .Replace(Placeholder.PROTOCOLID, service.ProtoId.ToString())
                    .Replace(Placeholder.SERVICENAME, JsonSafe(fallback));
            }
            return UsesServiceSubTemplates(template) ? JsonSerializer.Serialize(fallback) : fallback;
        }

        private string ConvertServiceElement(NwServiceElement service)
        {
            string protocol = IpProtos.FirstOrDefault(proto => proto.Id == service.ProtoId)?.Name ?? service.ProtoId.ToString();
            if (!string.IsNullOrWhiteSpace(service.Name))
            {
                return service.Name;
            }
            return service.Port > 0 ? $"{protocol}/{FormatPort(service)}" : protocol;
        }

        private static string ResolveSortName(NwObjectElement element)
        {
            if (!string.IsNullOrWhiteSpace(element.GroupName))
            {
                return element.GroupName;
            }
            if (!string.IsNullOrWhiteSpace(element.Name))
            {
                return element.Name;
            }
            return element.IpString;
        }

        private static bool UsesNetworkSubTemplates(ExternalTicketTemplate template)
        {
            return !string.IsNullOrWhiteSpace(template.IpTemplate) || !string.IsNullOrWhiteSpace(template.NwObjGroupTemplate);
        }

        private static bool UsesObjectSubTemplates(ExternalTicketTemplate template)
        {
            return !string.IsNullOrWhiteSpace(template.ObjectTemplate) || !string.IsNullOrWhiteSpace(template.ObjectTemplateShort);
        }

        private static bool UsesServiceSubTemplates(ExternalTicketTemplate template)
        {
            return !string.IsNullOrWhiteSpace(template.ServiceTemplate) ||
                !string.IsNullOrWhiteSpace(template.IcmpTemplate) ||
                !string.IsNullOrWhiteSpace(template.IpProtocolTemplate);
        }

        private static string FillNetworkGroupTemplate(string template, string groupName, string managementId, string managementName)
        {
            return template
                .Replace(Placeholder.GROUPNAME, JsonSafe(groupName))
                .Replace(Placeholder.OBJECTNAME, JsonSafe(groupName))
                .Replace(Placeholder.MANAGEMENT_ID, managementId)
                .Replace(Placeholder.MANAGEMENT_NAME, JsonSafe(managementName));
        }

        private static string FillIpTemplate(string template, string ipString, string ipEndString, string fallbackName)
        {
            string objectType = GetObjectType(ipString, ipEndString);
            return template
                .Replace(Placeholder.IP, JsonSafe(ipString))
                .Replace(Placeholder.OBJECTNAME, JsonSafe(fallbackName))
                .Replace(Placeholder.OBJECT_TYPE, objectType)
                .Replace(Placeholder.OBJECT_DETAILS, JsonSafe(GetObjectDetails(ipString, ipEndString, objectType)));
        }

        private string FillObjectTemplate(string template, NwObjectElement element, string fallbackName, string managementId)
        {
            string objectType = GetObjectType(element.IpString, element.IpEndString);
            return FillObjectTemplate(template, fallbackName, objectType, GetObjectDetails(element.IpString, element.IpEndString, objectType), element.Comment ?? "", element.RequestAction, managementId);
        }

        private static string FillObjectTemplate(string template, WfReqElement element, string fallbackName, string managementId)
        {
            string objectType = GetObjectType(element.IpString ?? "", element.IpEnd ?? "");
            return FillObjectTemplate(template, fallbackName, objectType, GetObjectDetails(element.IpString ?? "", element.IpEnd ?? "", objectType), "", element.RequestAction, managementId);
        }

        private static string FillObjectTemplate(string template, string name, string objectType, string details, string comment, string requestAction, string managementId)
        {
            return template
                .Replace(Placeholder.TYPE, ObjectUpdateStatus(requestAction) == "NEW" ? objectType : "Object")
                .Replace(Placeholder.OBJECTNAME, JsonSafe(name))
                .Replace(Placeholder.OBJECT_TYPE, objectType)
                .Replace(Placeholder.OBJECT_DETAILS, JsonSafe(details))
                .Replace(Placeholder.COMMENT, JsonSafe(comment))
                .Replace(Placeholder.STATUS, ObjectStatus(requestAction))
                .Replace(Placeholder.OBJUPDSTATUS, ObjectUpdateStatus(requestAction))
                .Replace(Placeholder.MANAGEMENT_ID, managementId);
        }

        private static string FillObjectShortTemplate(string template, string name, string requestAction, string managementId)
        {
            return template
                .Replace(Placeholder.OBJECTNAME, JsonSafe(name))
                .Replace(Placeholder.STATUS, ObjectStatus(requestAction))
                .Replace(Placeholder.OBJUPDSTATUS, ObjectUpdateStatus(requestAction))
                .Replace(Placeholder.MANAGEMENT_ID, managementId);
        }

        private static string GetObjectType(string ipString, string ipEndString)
        {
            if (string.IsNullOrWhiteSpace(ipString))
            {
                return ObjectType.Group;
            }
            return IpOperations.GetObjectType(ipString, ipEndString) switch
            {
                ObjectType.Network => ObjectType.Network,
                ObjectType.IPRange => "range",
                _ => ObjectType.Host
            };
        }

        private static string GetObjectDetails(string ipString, string ipEndString, string objectType)
        {
            if (string.IsNullOrWhiteSpace(ipString))
            {
                return "";
            }
            return objectType switch
            {
                ObjectType.Network => IpOperations.ToDotNotation(GetStartIp(ipString, ipEndString), GetEndIp(ipString, ipEndString)),
                "range" => $"{GetStartIp(ipString, ipEndString)}-{GetEndIp(ipString, ipEndString)}",
                _ => ipString
            };
        }

        private static string GetStartIp(string ipString, string ipEndString)
        {
            return string.IsNullOrWhiteSpace(ipEndString) ? IpOperations.SplitIpToRange(ipString).Item1 : ipString;
        }

        private static string GetEndIp(string ipString, string ipEndString)
        {
            return string.IsNullOrWhiteSpace(ipEndString) ? IpOperations.SplitIpToRange(ipString).Item2 : ipEndString;
        }

        private static string ObjectStatus(string requestAction)
        {
            return requestAction switch
            {
                nameof(RequestAction.create) => "ADDED",
                nameof(RequestAction.delete) => "DELETED",
                _ => "NOT_CHANGED"
            };
        }

        private static string ObjectUpdateStatus(string requestAction)
        {
            return requestAction == nameof(RequestAction.create) ? "NEW" : "EXISTING_NOT_EDITED";
        }

        private static bool IsIcmp(NwServiceElement service, string protocol)
        {
            return service.ProtoId == 1 || protocol.Equals("ICMP", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatPort(NwServiceElement service)
        {
            return service.PortEnd == null || service.PortEnd == 0 || service.Port == service.PortEnd ? service.Port.ToString() : $"{service.Port}-{service.PortEnd}";
        }

        private static string JsonSafe(string value)
        {
            bool shortened = false;
            return value.SanitizeJsonFieldMand(ref shortened);
        }
    }
}

using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Display;

namespace FWO.Ui.Services
{
    /// <summary>One displayed element of an implementation task: name, content and, for group members, the implementation action.</summary>
    /// <param name="Name">Name of the object or group.</param>
    /// <param name="Content">Ip address, service definition or (truncated) group content.</param>
    /// <param name="Action">Implementation action of a group member; empty for other elements.</param>
    public sealed record PlanningImplElementRow(string Name, string Content, string Action = "");

    /// <summary>
    /// Resolves the displayed values of implementation tasks in the planning views and applies the changes made by the
    /// planner (management, device, name), so that card and list view show and change them in the same way.
    /// </summary>
    public sealed class PlanningImplTaskDisplayLogic
    {
        /// <summary>Maximum number of members displayed for a referenced group, the remaining ones are only counted.</summary>
        public const int kMaxDisplayedElements = 5;

        private readonly WfHandler wfHandler;
        private readonly UserConfig userConfig;
        private readonly List<RuleAction> ruleActions;
        private readonly List<Tracking> trackings;
        private readonly List<IpProtocol> ipProtocols;
        private string? timeObjectPrecision;

        /// <summary>Initializes the display logic.</summary>
        /// <param name="wfHandler">The workflow handler holding the ticket, managements and devices.</param>
        /// <param name="userConfig">The current user configuration.</param>
        /// <param name="ruleActions">The available rule actions.</param>
        /// <param name="trackings">The available trackings.</param>
        /// <param name="ipProtocols">The protocols used to display services.</param>
        public PlanningImplTaskDisplayLogic(WfHandler wfHandler, UserConfig userConfig, List<RuleAction> ruleActions,
            List<Tracking> trackings, List<IpProtocol> ipProtocols)
        {
            this.wfHandler = wfHandler;
            this.userConfig = userConfig;
            this.ruleActions = ruleActions;
            this.trackings = trackings;
            this.ipProtocols = ipProtocols;
        }

        /// <summary>Returns whether the task creates, changes or deletes a rule.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool IsRuleImplementationTask(WfImplTask implementationTask) => IsRuleTask(implementationTask)
            || implementationTask.TaskType == WfTaskType.rule_delete.ToString();

        /// <summary>Returns whether the task creates or changes a rule, i.e. has rule action, tracking and rule position.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool IsRuleTask(WfImplTask implementationTask) => implementationTask.TaskType == WfTaskType.access.ToString()
            || implementationTask.TaskType == WfTaskType.rule_modify.ToString();

        /// <summary>Returns whether the task creates or changes a group.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool IsGroupTask(WfImplTask implementationTask) => implementationTask.TaskType == WfTaskType.group_create.ToString()
            || implementationTask.TaskType == WfTaskType.group_modify.ToString();

        /// <summary>Returns whether the task orders a time object.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool IsTimeObjectTask(WfImplTask implementationTask) => implementationTask.ImplElements
            .Any(element => element.Field == ElemFieldType.time.ToString());

        /// <summary>Returns whether the free text is displayed: if it is filled or the task is a generic task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool HasFreeText(WfImplTask implementationTask) => !string.IsNullOrWhiteSpace(implementationTask.FreeText)
            || implementationTask.TaskType == WfTaskType.generic.ToString();

        /// <summary>Returns whether the planner may change the free text; management object tasks keep it.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool CanEditFreeText(WfImplTask implementationTask) => !WfObjectTaskHelper.IsManagementObjectTask(implementationTask.TaskType);

        /// <summary>
        /// Returns whether the free text field of an implementation task is shown: if the free text is displayed anyway, or
        /// if the user may add one. Used by the card view; the list view offers the same in its free text column.
        /// </summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="canEditTask">Whether the user may change the implementation task.</param>
        public static bool ShowsFreeText(WfImplTask implementationTask, bool canEditTask) => HasFreeText(implementationTask)
            || (canEditTask && CanEditFreeText(implementationTask));

        /// <summary>Returns whether the planner may change the name: of a group, an object or a time object.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static bool HasEditableName(WfImplTask implementationTask) => IsGroupTask(implementationTask)
            || WfObjectTaskHelper.IsObjectTask(implementationTask.TaskType);

        /// <summary>Returns the element field holding the object of an object task: service or network (source).</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static ElemFieldType ObjectElementField(WfImplTask implementationTask) => implementationTask.GetServiceElements().Count > 0
            ? ElemFieldType.service
            : ElemFieldType.source;

        /// <summary>
        /// Returns the displayed elements of a field; object tasks show only the requested (created or modified) element,
        /// or the first element if none is marked.
        /// </summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="field">The element field.</param>
        public static IEnumerable<WfImplElement> ElementsForDisplay(WfImplTask implementationTask, ElemFieldType field)
        {
            IEnumerable<WfImplElement> elements = implementationTask.ImplElements
                .Where(element => element.Field == field.ToString());
            if (!WfObjectTaskHelper.IsObjectTask(implementationTask.TaskType))
            {
                return elements;
            }

            List<WfImplElement> requestedElements = [.. elements
                .Where(element => element.ImplAction == RequestAction.create.ToString()
                    || element.ImplAction == RequestAction.modify.ToString())
                .Take(1)];
            return requestedElements.Count > 0 ? requestedElements : elements.Take(1);
        }

        /// <summary>Returns the uid of the rule deleted by a rule delete task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static string RuleUid(WfImplTask implementationTask)
        {
            return implementationTask.ImplElements
                .FirstOrDefault(element => element.Field == ElemFieldType.rule.ToString())?.RuleUid ?? "";
        }

        /// <summary>Returns the name of the rule action.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public string RuleActionName(WfImplTask implementationTask)
        {
            return ruleActions.FirstOrDefault(action => action.Id == implementationTask.RuleAction)?.Name
                ?? implementationTask.RuleAction?.ToString()
                ?? "";
        }

        /// <summary>Returns the name of the tracking.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public string TrackingName(WfImplTask implementationTask)
        {
            return trackings.FirstOrDefault(tracking => tracking.Id == implementationTask.Tracking)?.Name
                ?? implementationTask.Tracking?.ToString()
                ?? "";
        }

        /// <summary>Returns the name of the management, falling back to the management of the device or of the request task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        public string ManagementName(WfImplTask implementationTask, WfReqTask requestTask)
        {
            return wfHandler.GetImplTaskManagementName(implementationTask, requestTask.ManagementId);
        }

        /// <summary>Returns the device name; access tasks for all devices without a selected device show "all".</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        public string DeviceName(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (implementationTask.DeviceId == null || implementationTask.DeviceId == 0)
            {
                return implementationTask.TaskType == WfTaskType.access.ToString() && requestTask.HasAllDevicesSelected()
                    ? userConfig.GetText("all")
                    : "";
            }
            return wfHandler.Devices.FirstOrDefault(device => device.Id == implementationTask.DeviceId)?.Name ?? "";
        }

        /// <summary>Returns the group name of a group task, falling back to the group name ordered by the request task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        public static string GroupName(WfImplTask implementationTask, WfReqTask requestTask)
        {
            string implementationGroupName = implementationTask.ImplElements
                .Where(element => element.Field == ElemFieldType.source.ToString()
                    || element.Field == ElemFieldType.service.ToString())
                .Select(element => element.GroupName ?? element.Name)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? "";
            return !string.IsNullOrWhiteSpace(implementationGroupName)
                ? implementationGroupName
                : requestTask.GetAddInfoValue(AdditionalInfoKeys.GrpName);
        }

        /// <summary>Returns the name of the object or time object of an object task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public static string ObjectName(WfImplTask implementationTask)
        {
            return NamedElement(implementationTask)?.Name ?? "";
        }

        /// <summary>Returns the name of a group task or object task, as changed by the name editor.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        public static string EditableName(WfImplTask implementationTask, WfReqTask requestTask)
        {
            return IsGroupTask(implementationTask) ? GroupName(implementationTask, requestTask) : ObjectName(implementationTask);
        }

        /// <summary>Returns the content of the object of an object task: its ip address or service definition.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public string ObjectContent(WfImplTask implementationTask)
        {
            ElemFieldType field = ObjectElementField(implementationTask);
            WfImplElement? element = ElementsForDisplay(implementationTask, field).FirstOrDefault();
            if (element == null)
            {
                return "";
            }
            return field == ElemFieldType.service
                ? DisplayBase.DisplayService(ToServiceElement(element), ipProtocols)
                : NetworkContent(element);
        }

        /// <summary>Returns the members of a group task with their implementation action.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public List<PlanningImplElementRow> GroupMemberRows(WfImplTask implementationTask)
        {
            bool isServiceGroup = implementationTask.GetServiceElements().Count > 0;
            string field = isServiceGroup ? ElemFieldType.service.ToString() : ElemFieldType.source.ToString();
            return [.. implementationTask.ImplElements
                .Where(element => element.Field == field)
                .Select(element => new PlanningImplElementRow(element.Name ?? element.GroupName ?? "",
                    isServiceGroup ? DisplayBase.DisplayService(ToServiceElement(element), ipProtocols) : NetworkContent(element),
                    element.ImplAction))];
        }

        /// <summary>Returns the network elements of a field; referenced groups of the ticket show their (truncated) members.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="field">The element field (source or destination).</param>
        public List<PlanningImplElementRow> NetworkDisplayRows(WfImplTask implementationTask, ElemFieldType field)
        {
            List<PlanningImplElementRow> rows = [];
            foreach (WfImplElement element in ElementsForDisplay(implementationTask, field))
            {
                List<WfReqElement> members = FindGroupMembers(element, field);
                rows.Add(members.Count > 0
                    ? new(element.GroupName ?? element.Name ?? "", TruncatedGroupContent(members.Select(NetworkMemberContent)))
                    : new(element.Name ?? element.GroupName ?? "", NetworkContent(element)));
            }
            return rows;
        }

        /// <summary>Returns the service elements; referenced groups of the ticket show their (truncated) members.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        public List<PlanningImplElementRow> ServiceDisplayRows(WfImplTask implementationTask)
        {
            List<PlanningImplElementRow> rows = [];
            foreach (WfImplElement element in ElementsForDisplay(implementationTask, ElemFieldType.service))
            {
                List<WfReqElement> members = FindGroupMembers(element, ElemFieldType.service);
                rows.Add(members.Count > 0
                    ? new(element.GroupName ?? element.Name ?? "",
                        TruncatedGroupContent(members.Select(member => DisplayBase.DisplayService(ToServiceElement(member), ipProtocols))))
                    : new(element.Name ?? element.GroupName ?? "", DisplayBase.DisplayService(ToServiceElement(element), ipProtocols)));
            }
            return rows;
        }

        /// <summary>
        /// Returns the time restriction as "name (begin - end)", or an empty string if there is none. The name is the first
        /// one given of the time element of the implementation task and the time object of the request task, as its
        /// request row shows it; without one, a name is built from the visible dates, like for the request row. A begin or
        /// end date hidden by the configured visibility is left out.
        /// </summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        /// <param name="visibility">The configured field visibility of access tasks, defining whether begin and end are shown.</param>
        public string TimeRestriction(WfImplTask implementationTask, WfReqTask requestTask, AccessTaskFieldVisibilityConfig visibility)
        {
            WfImplElement? timeElement = implementationTask.ImplElements
                .FirstOrDefault(element => element.Field == ElemFieldType.time.ToString());
            DateTime? beginDate = visibility.ShowValidFrom ? timeElement?.StartTime ?? implementationTask.TargetBeginDate : null;
            DateTime? endDate = visibility.ShowValidTo ? timeElement?.EndTime ?? implementationTask.TargetEndDate : null;
            // parsed once, as the time restriction is displayed for every implementation task
            timeObjectPrecision ??= FlowIntegrationConfig.Parse(userConfig.ReqFlowIntegration).TimeObjectPrecision;
            string? name = timeElement?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = TimeObjectReferenceResolver.TimeObjectName(wfHandler.ActTicket.Tasks, requestTask);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = TimeObjectNamingHelper.BuildDateBasedName(beginDate, endDate, timeObjectPrecision);
            }
            string begin = TimeObjectDisplayHelper.FormatValue(beginDate, timeObjectPrecision, false, "dd.MM.yyyy");
            string end = TimeObjectDisplayHelper.FormatValue(endDate, timeObjectPrecision, true, "dd.MM.yyyy");
            string validity = Validity(begin, end);
            if (string.IsNullOrWhiteSpace(validity))
            {
                return name;
            }
            return string.IsNullOrWhiteSpace(name) ? $"({validity})" : $"{name} ({validity})";
        }

        /// <summary>Returns the management the devices are restricted to: of the task, of its device or of the request task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        public int? EffectiveManagementId(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (implementationTask.ManagementId.HasValue)
            {
                return implementationTask.ManagementId;
            }

            if (implementationTask.DeviceId is int deviceId && deviceId > 0)
            {
                return wfHandler.Devices.FirstOrDefault(device => device.Id == deviceId)?.Management?.Id;
            }

            return requestTask.ManagementId;
        }

        /// <summary>Returns the devices selectable for the task: those of its effective management, or all devices.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        public List<Device> AvailableDevices(WfImplTask implementationTask, WfReqTask requestTask)
        {
            int? managementId = EffectiveManagementId(implementationTask, requestTask);
            return managementId is int id && id > 0
                ? [.. wfHandler.Devices.Where(device => device.Management?.Id == id)]
                : wfHandler.Devices;
        }

        /// <summary>Returns the device with the given id, or null if there is none.</summary>
        /// <param name="deviceId">The device id.</param>
        public Device? FindDevice(int? deviceId)
        {
            return deviceId is int id && id > 0
                ? wfHandler.Devices.FirstOrDefault(device => device.Id == id)
                : null;
        }

        /// <summary>Returns whether the device belongs to the configured management of the task (or no device or management is set).</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        /// <param name="device">The device.</param>
        public static bool IsDeviceCompatible(WfImplTask implementationTask, WfReqTask requestTask, Device? device)
        {
            int? managementId = ConfiguredManagementId(implementationTask, requestTask);
            return device == null || managementId is not int id || device.Management?.Id == id;
        }

        /// <summary>Sets the management and removes a device which does not belong to it.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        /// <param name="management">The selected management.</param>
        public void ApplyManagement(WfImplTask implementationTask, WfReqTask requestTask, Management? management)
        {
            implementationTask.ManagementId = management?.Id;
            if (!IsDeviceCompatible(implementationTask, requestTask, FindDevice(implementationTask.DeviceId)))
            {
                implementationTask.DeviceId = null;
            }
        }

        /// <summary>Sets the device, and its management if none is configured; a device of another management is rejected.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        /// <param name="device">The selected device.</param>
        /// <returns>True if the device was set.</returns>
        public static bool ApplyDevice(WfImplTask implementationTask, WfReqTask requestTask, Device? device)
        {
            if (!IsDeviceCompatible(implementationTask, requestTask, device))
            {
                return false;
            }

            if (device?.Management?.Id > 0 && !ConfiguredManagementId(implementationTask, requestTask).HasValue)
            {
                implementationTask.ManagementId = device.Management.Id;
            }
            implementationTask.DeviceId = device?.Id;
            return true;
        }

        /// <summary>Sets the name: the group name of all members of a group task, otherwise the name of the (time) object.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="name">The new name.</param>
        public static void ApplyName(WfImplTask implementationTask, string name)
        {
            if (IsGroupTask(implementationTask))
            {
                foreach (WfImplElement element in implementationTask.ImplElements
                    .Where(element => element.Field == ElemFieldType.source.ToString()
                        || element.Field == ElemFieldType.service.ToString()))
                {
                    element.GroupName = name;
                }
                return;
            }

            WfImplElement? namedElement = NamedElement(implementationTask);
            if (namedElement != null)
            {
                namedElement.Name = name;
            }
        }

        /// <summary>Converts an element to a service element for the service display.</summary>
        /// <param name="element">The request or implementation element.</param>
        public static NwServiceElement ToServiceElement(WfElementBase element) => new()
        {
            ElemId = element is WfReqElement requestElement ? requestElement.Id : (element as WfImplElement)?.Id ?? 0,
            Port = element.Port,
            PortEnd = element.PortEnd,
            ProtoId = element.ProtoId ?? 0,
            HasProtocol = element.ProtoId.HasValue,
            ServiceId = element.ServiceId,
            FlowServiceObjectId = element.FlowServiceObjectId,
            FlowServiceGroupId = element.FlowServiceGroupId,
            ObjectTaskId = element.ObjectTaskId,
            Name = element.Name,
            GroupName = element.GroupName
        };

        /// <summary>Returns the management set on the implementation task, or else on its request task; a device does not define it.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task of the implementation task.</param>
        private static int? ConfiguredManagementId(WfImplTask implementationTask, WfReqTask requestTask)
        {
            return implementationTask.ManagementId ?? requestTask.ManagementId;
        }

        /// <summary>Returns the element holding the name of a time object or object task: the time element, or else the first object element.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        private static WfImplElement? NamedElement(WfImplTask implementationTask)
        {
            return implementationTask.ImplElements.FirstOrDefault(candidate => candidate.Field == ElemFieldType.time.ToString())
                ?? ElementsForDisplay(implementationTask, ObjectElementField(implementationTask)).FirstOrDefault();
        }

        /// <summary>Returns the ip address, range or network of a network element.</summary>
        /// <param name="element">The network element.</param>
        private static string NetworkContent(WfImplElement element)
        {
            return NwObjDisplay.DisplayIp(element.IpString ?? element.Cidr?.CidrString, element.IpEnd ?? element.CidrEnd?.CidrString);
        }

        /// <summary>Returns the ip address of a group member, or its name if it has none.</summary>
        /// <param name="member">The group member.</param>
        private static string NetworkMemberContent(WfReqElement member)
        {
            string ip = NwObjDisplay.DisplayIp(member.IpString, member.IpEnd);
            return string.IsNullOrWhiteSpace(ip) ? member.Name ?? member.GroupName ?? "" : ip;
        }

        /// <summary>Joins the non-empty member values, showing at most the maximum number of elements followed by the count of the others.</summary>
        /// <param name="values">The displayed values of the group members.</param>
        private static string TruncatedGroupContent(IEnumerable<string> values)
        {
            List<string> content = [.. values.Where(value => !string.IsNullOrWhiteSpace(value))];
            string result = string.Join(", ", content.Take(kMaxDisplayedElements));
            return content.Count > kMaxDisplayedElements
                ? $"{result}, … (+{content.Count - kMaxDisplayedElements})"
                : result;
        }

        /// <summary>Returns the validity as "begin - end", or only the given one of both.</summary>
        /// <param name="begin">The formatted begin, or empty.</param>
        /// <param name="end">The formatted end, or empty.</param>
        private static string Validity(string begin, string end)
        {
            if (string.IsNullOrWhiteSpace(begin))
            {
                return end;
            }
            return string.IsNullOrWhiteSpace(end) ? begin : $"{begin} - {end}";
        }

        /// <summary>Finds the members of a group referenced by an element: of the referenced group task or the group task with the same name.</summary>
        /// <param name="element">The element referencing the group.</param>
        /// <param name="field">The element field (source, destination or service).</param>
        private List<WfReqElement> FindGroupMembers(WfImplElement element, ElemFieldType field)
        {
            string memberField = field == ElemFieldType.service
                ? ElemFieldType.service.ToString()
                : ElemFieldType.source.ToString();
            WfReqTask? groupTask = element.ObjectTaskId is > 0
                ? wfHandler.ActTicket.Tasks.FirstOrDefault(task => task.Id == element.ObjectTaskId)
                : null;
            string groupName = element.GroupName ?? element.Name ?? "";
            groupTask ??= wfHandler.ActTicket.Tasks.FirstOrDefault(task =>
                IsGroupRequestTask(task)
                && (string.Equals(task.GetAddInfoValue(AdditionalInfoKeys.GrpName), groupName, StringComparison.OrdinalIgnoreCase)
                    || task.Elements.Any(member => string.Equals(member.GroupName ?? member.Name, groupName, StringComparison.OrdinalIgnoreCase))));
            return groupTask?.Elements
                .Where(member => member.Field == memberField)
                .Where(member => member.RequestAction != RequestAction.delete.ToString())
                .ToList() ?? [];
        }

        /// <summary>Returns whether a request task creates or modifies a group.</summary>
        /// <param name="task">The request task.</param>
        private static bool IsGroupRequestTask(WfReqTask task) => task.TaskType is nameof(WfTaskType.group_create)
            or nameof(WfTaskType.group_modify);
    }
}

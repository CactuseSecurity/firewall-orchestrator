using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Display;

namespace FWO.Ui.Services
{
    /// <summary>Controls how access-task elements are rendered.</summary>
    public enum ApprovalTaskElementDisplayMode
    {
        /// <summary>Shows resolved object and group values.</summary>
        Resolved,
        /// <summary>Shows technical values without display-name decoration.</summary>
        Technical,
        /// <summary>Shows group references as groups instead of expanding their members.</summary>
        UnresolvedGroups
    }

    /// <summary>Structured network-object values for the approval-task tables.</summary>
    public sealed record ApprovalTaskNetworkElementRow(string Name, string Ip, string Zone);

    /// <summary>Structured service values for the approval-task tables.</summary>
    public sealed record ApprovalTaskServiceElementRow(string Name, string Content);

    /// <summary>
    /// Presentation-only logic for the task bodies shown by the approval task view.
    /// Workflow state changes remain in ApprovalTaskDetailsView.
    /// </summary>
    public sealed class ApprovalTaskDisplayLogic
    {
        private readonly WfHandler wfHandler;
        private readonly UserConfig userConfig;
        private readonly List<RuleAction> ruleActions;
        private readonly List<Tracking> trackings;
        private readonly List<IpProtocol> ipProtocols;
        private readonly List<NetworkZone> networkZones;
        private readonly ApprovalTaskElementDisplayMode displayMode;

        /// <summary>Initializes display logic using the legacy technical/resolved switch.</summary>
        /// <param name="wfHandler">The workflow handler providing ticket data.</param>
        /// <param name="userConfig">The current user configuration.</param>
        /// <param name="ruleActions">The configured rule actions.</param>
        /// <param name="trackings">The configured tracking options.</param>
        /// <param name="ipProtocols">The configured IP protocols.</param>
        /// <param name="networkZones">The configured compliance zones.</param>
        /// <param name="technicalRuleView">Whether technical values should be shown.</param>
        public ApprovalTaskDisplayLogic(WfHandler wfHandler, UserConfig userConfig,
            List<RuleAction> ruleActions, List<Tracking> trackings, List<IpProtocol> ipProtocols,
            List<NetworkZone> networkZones, bool technicalRuleView)
            : this(wfHandler, userConfig, ruleActions, trackings, ipProtocols, networkZones,
                GetDisplayMode(technicalRuleView))
        {
        }

        /// <summary>Initializes display logic for the selected element display mode.</summary>
        /// <param name="wfHandler">The workflow handler providing ticket data.</param>
        /// <param name="userConfig">The current user configuration.</param>
        /// <param name="ruleActions">The configured rule actions.</param>
        /// <param name="trackings">The configured tracking options.</param>
        /// <param name="ipProtocols">The configured IP protocols.</param>
        /// <param name="networkZones">The configured compliance zones.</param>
        /// <param name="displayMode">The element display mode.</param>
        public ApprovalTaskDisplayLogic(WfHandler wfHandler, UserConfig userConfig,
            List<RuleAction> ruleActions, List<Tracking> trackings, List<IpProtocol> ipProtocols,
            List<NetworkZone> networkZones, ApprovalTaskElementDisplayMode displayMode)
        {
            this.wfHandler = wfHandler;
            this.userConfig = userConfig;
            this.ruleActions = ruleActions;
            this.trackings = trackings;
            this.ipProtocols = ipProtocols;
            this.networkZones = networkZones;
            this.displayMode = displayMode;
        }

        private FlowIntegrationConfig FlowIntegration => FlowIntegrationConfig.Parse(userConfig.ReqFlowIntegration);

        private static ApprovalTaskElementDisplayMode GetDisplayMode(bool technicalRuleView)
        {
            return technicalRuleView ? ApprovalTaskElementDisplayMode.Technical : ApprovalTaskElementDisplayMode.Resolved;
        }

        /// <summary>Gets the active access-element display mode.</summary>
        public ApprovalTaskElementDisplayMode DisplayMode => displayMode;

        /// <summary>Returns distinct current handlers.</summary>
        public string Handlers(IEnumerable<WfReqTask> tasks) => string.Join(", ", tasks
            .Select(task => task.CurrentHandler?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct());

        /// <summary>Returns distinct assigned groups.</summary>
        public string AssignedGroups(IEnumerable<WfReqTask> tasks) => string.Join(", ", tasks
            .Select(task => task.AssignedGroup)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct());

        /// <summary>Returns distinct management IDs.</summary>
        public string ManagementIds(IEnumerable<WfReqTask> tasks) => string.Join(", ", tasks
            .Select(task => task.ManagementId?.ToString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct());

        /// <summary>Returns distinct owner names.</summary>
        public string Owners(IEnumerable<WfReqTask> tasks) => string.Join(", ", tasks
            .SelectMany(task => task.Owners)
            .Select(owner => owner.Owner)
            .Where(owner => owner != null)
            .Select(owner => owner!.Display(userConfig.GetText("common_service")))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct());

        /// <summary>Returns distinct requesting-owner names stored on interface request tasks.</summary>
        public string RequestingOwners(IEnumerable<WfReqTask> tasks) => string.Join(", ", tasks
            .Select(task => task.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner))
            .Where(id => id.HasValue)
            .Select(id => wfHandler.AllOwners.FirstOrDefault(owner => owner.Id == id!.Value)?.Display(userConfig.GetText("common_service")))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct());

        /// <summary>Returns distinct non-empty values selected from the tasks.</summary>
        public string DistinctValues(IEnumerable<WfReqTask> tasks, Func<WfReqTask, string?> selector) => string.Join("; ", tasks
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct());

        /// <summary>Formats time-object names and the visible validity interval.</summary>
        public string TimeObjectDisplay(IEnumerable<WfReqTask> tasks, AccessTaskFieldVisibilityConfig visibility)
        {
            string name = string.Join("; ", tasks
                .Select(GetTimeObjectName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct());
            string begin = "";
            if (visibility.ShowValidFrom)
            {
                begin = Dates(tasks, false);
            }

            string end = "";
            if (visibility.ShowValidTo)
            {
                end = Dates(tasks, true);
            }
            string validity = BuildValidity(begin, end);
            if (string.IsNullOrWhiteSpace(validity))
            {
                return name;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return $"({validity})";
            }

            return $"{name} ({validity})";
        }

        /// <summary>Resolves and formats the rule actions used by the tasks.</summary>
        public string RuleActions(IEnumerable<WfReqTask> tasks) => string.Join("; ", tasks
            .Select(task => ruleActions.FirstOrDefault(action => action.Id == task.RuleAction)?.Name ?? task.RuleAction?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct());

        /// <summary>Resolves and formats the tracking values used by the tasks.</summary>
        public string Trackings(IEnumerable<WfReqTask> tasks) => string.Join("; ", tasks
            .Select(task => trackings.FirstOrDefault(tracking => tracking.Id == task.Tracking)?.Name ?? task.Tracking?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct());

        /// <summary>Returns element values according to the selected display mode.</summary>
        public IEnumerable<string> RuleElementValues(IEnumerable<WfReqTask> tasks, ElemFieldType field)
        {
            return displayMode switch
            {
                ApprovalTaskElementDisplayMode.Technical => TechnicalElementValues(tasks, field),
                ApprovalTaskElementDisplayMode.UnresolvedGroups => UnresolvedGroupElementValues(tasks, field),
                _ => ResolvedElementValues(tasks, field)
            };
        }

        /// <summary>Builds the network-object columns for an access-task field.</summary>
        public IEnumerable<ApprovalTaskNetworkElementRow> NetworkElementRows(IEnumerable<WfReqTask> tasks, ElemFieldType field) =>
            DisplayElements(tasks, field).Select(BuildNetworkElementRow);

        /// <summary>Builds the service columns for an access-task field.</summary>
        public IEnumerable<ApprovalTaskServiceElementRow> ServiceElementRows(IEnumerable<WfReqTask> tasks, ElemFieldType field) =>
            DisplayElements(tasks, field).Select(BuildServiceElementRow);

        private ApprovalTaskNetworkElementRow BuildNetworkElementRow(WfReqElement element)
        {
            string name = displayMode == ApprovalTaskElementDisplayMode.Technical ? "" : ElementDisplayName(element);
            string ip = displayMode == ApprovalTaskElementDisplayMode.UnresolvedGroups
                ? ""
                : NwObjDisplay.DisplayIp(element.IpString, element.IpEnd);
            string zone = displayMode == ApprovalTaskElementDisplayMode.UnresolvedGroups ? "" : Zones(element);
            return new ApprovalTaskNetworkElementRow(name, ip, zone);
        }

        private ApprovalTaskServiceElementRow BuildServiceElementRow(WfReqElement element)
        {
            string name = "";
            if (displayMode == ApprovalTaskElementDisplayMode.Resolved)
            {
                name = ElementDisplayName(element);
            }

            string content = displayMode == ApprovalTaskElementDisplayMode.UnresolvedGroups
                ? FirstNonEmpty(element.GroupName, element.Name) ?? ""
                : DisplayBase.DisplayService(ToServiceElement(element), ipProtocols);
            return new ApprovalTaskServiceElementRow(name, content);
        }

        private string ElementDisplayName(WfReqElement element)
        {
            string? displayName = displayMode == ApprovalTaskElementDisplayMode.UnresolvedGroups
                ? FirstNonEmpty(element.GroupName, element.Name)
                : FirstNonEmpty(element.Name, element.GroupName);
            return displayName ?? "";
        }

        private string Dates(IEnumerable<WfReqTask> tasks, bool isEndDate) => string.Join("; ", tasks
            .Select(task => EffectiveTimeObjectDate(task, isEndDate))
            .OfType<DateTime>()
            .Select(date => FormatDate(date, isEndDate))
            .Distinct());

        private DateTime? EffectiveTimeObjectDate(WfReqTask task, bool isEndDate)
        {
            WfReqElement? timeElement = TimeObjectReferenceResolver.FindParallelTimeElement(
                wfHandler.ActTicket.Tasks, task, null, null);
            timeElement ??= TimeObjectElementHelper.Find(task.Elements);
            if (timeElement != null)
            {
                DateTime? elementDate = timeElement.StartTime;
                if (isEndDate)
                {
                    elementDate = timeElement.EndTime;
                }
                if (elementDate.HasValue)
                {
                    return elementDate;
                }
            }

            return isEndDate ? task.TargetEndDate : task.TargetBeginDate;
        }

        private string GetTimeObjectName(WfReqTask task)
        {
            WfReqElement? timeElement = TimeObjectElementHelper.Find(task.Elements);
            WfReqElement? effectiveTimeElement = TimeObjectReferenceResolver.FindParallelTimeElement(
                wfHandler.ActTicket.Tasks, task, null, null) ?? timeElement;
            string name = effectiveTimeElement?.Name ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                long? timeObjectId = TimeObjectElementHelper.GetSelectorId(timeElement);
                WfReqElement? parallelTimeElement = TimeObjectReferenceResolver.FindParallelTimeElement(
                    wfHandler.ActTicket.Tasks, task, timeObjectId, null);
                name = parallelTimeElement?.Name ?? TimeObjectNamingHelper.BuildDateBasedName(
                    task.TargetBeginDate, task.TargetEndDate, FlowIntegration.TimeObjectPrecision);
            }

            return name;
        }

        private static string BuildValidity(string begin, string end)
        {
            if (string.IsNullOrWhiteSpace(begin))
            {
                return end;
            }

            if (string.IsNullOrWhiteSpace(end))
            {
                return begin;
            }

            return $"{begin} - {end}";
        }

        private IEnumerable<string> ResolvedElementValues(IEnumerable<WfReqTask> tasks, ElemFieldType field) =>
            DisplayElements(tasks, field)
                .Select(ResolvedElementValue)
                .Where(value => !string.IsNullOrWhiteSpace(value));

        private IEnumerable<string> TechnicalElementValues(IEnumerable<WfReqTask> tasks, ElemFieldType field) =>
            DisplayElements(tasks, field)
                .Select(TechnicalElementValue)
                .Where(value => !string.IsNullOrWhiteSpace(value));

        private IEnumerable<string> UnresolvedGroupElementValues(IEnumerable<WfReqTask> tasks, ElemFieldType field) =>
            DisplayElements(tasks, field)
                .Select(UnresolvedGroupElementValue)
                .Where(value => !string.IsNullOrWhiteSpace(value));

        private IEnumerable<WfReqElement> DisplayElements(IEnumerable<WfReqTask> tasks, ElemFieldType field)
        {
            IEnumerable<WfReqElement> elements = tasks
                .SelectMany(task => task.Elements)
                .Where(element => element.Field == field.ToString())
                .SelectMany(element => DisplayCandidates(element, field))
                .Select(ResolveObjectTaskElement);

            return elements.DistinctBy(ElementIdentity, StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<WfReqElement> DisplayCandidates(WfReqElement element, ElemFieldType field)
        {
            if (displayMode == ApprovalTaskElementDisplayMode.UnresolvedGroups && IsGroupElement(element, field))
            {
                return Enumerable.Repeat(element, 1);
            }

            return ExpandTicketGroup(element, field);
        }

        private bool IsGroupElement(WfReqElement element, ElemFieldType field)
        {
            if (!string.IsNullOrWhiteSpace(element.GroupName)
                || element.FlowNetworkGroupId is > 0
                || element.FlowServiceGroupId is > 0)
            {
                return true;
            }

            string memberField = field == ElemFieldType.service
                ? ElemFieldType.service.ToString()
                : ElemFieldType.source.ToString();
            if (element.ObjectTaskId is > 0)
            {
                return wfHandler.ActTicket.Tasks.Any(task => task.Id == element.ObjectTaskId
                    && IsGroupTask(task)
                    && task.Elements.Any(candidate => candidate.Field == memberField));
            }

            if (HasConcreteElementData(element))
            {
                return false;
            }

            string groupName = element.Name ?? "";
            return !string.IsNullOrWhiteSpace(groupName)
                && wfHandler.ActTicket.Tasks.Any(task => IsGroupTask(task)
                    && MatchesTicketGroup(task, element, groupName, memberField));
        }

        private static bool IsGroupTask(WfReqTask task) => task.TaskType is nameof(WfTaskType.group_create)
            or nameof(WfTaskType.group_modify);

        private static bool HasConcreteElementData(WfReqElement element)
        {
            return element.ObjectTaskId is > 0
                || element.NetworkId is > 0
                || element.ServiceId is > 0
                || element.FlowNetworkObjectId is > 0
                || element.FlowServiceObjectId is > 0
                || !string.IsNullOrWhiteSpace(element.IpString)
                || !string.IsNullOrWhiteSpace(element.IpEnd)
                || element.Port.HasValue
                || element.PortEnd.HasValue
                || element.ProtoId.HasValue;
        }

        private static string ElementIdentity(WfReqElement element)
        {
            string? reference = GetElementReference(element);
            return !string.IsNullOrWhiteSpace(reference)
                ? string.Join('|', element.Field, reference, element.RequestAction)
                : BuildElementIdentityFromValues(element);
        }

        private static string? GetElementReference(WfReqElement element)
        {
            if (element.ObjectTaskId is > 0) return $"task:{element.ObjectTaskId}";
            if (element.FlowNetworkObjectId is > 0) return $"flow-nw:{element.FlowNetworkObjectId}";
            if (element.FlowNetworkGroupId is > 0) return $"flow-nw-group:{element.FlowNetworkGroupId}";
            if (element.FlowServiceObjectId is > 0) return $"flow-svc:{element.FlowServiceObjectId}";
            if (element.FlowServiceGroupId is > 0) return $"flow-svc-group:{element.FlowServiceGroupId}";
            if (element.NetworkId is > 0) return $"nw:{element.NetworkId}";
            if (element.ServiceId is > 0) return $"svc:{element.ServiceId}";
            return null;
        }

        private static string BuildElementIdentityFromValues(WfReqElement element)
        {
            return string.Join('|',
                element.Field,
                element.Name,
                element.GroupName,
                element.IpString,
                element.IpEnd,
                element.Port,
                element.PortEnd,
                element.ProtoId,
                element.RequestAction);
        }

        private string UnresolvedGroupElementValue(WfReqElement element)
        {
            ElemFieldType field = element.Field == ElemFieldType.service.ToString()
                ? ElemFieldType.service
                : ElemFieldType.source;
            bool isNetworkGroup = element.FlowNetworkGroupId is > 0;
            bool isServiceGroup = element.FlowServiceGroupId is > 0;
            if (IsGroupElement(element, field))
            {
                WfReqTask? groupTask = element.ObjectTaskId is > 0
                    ? wfHandler.ActTicket.Tasks.FirstOrDefault(task => task.Id == element.ObjectTaskId && IsGroupTask(task))
                    : null;
                string name = FirstNonEmpty(element.GroupName, element.Name,
                    groupTask?.GetAddInfoValue(AdditionalInfoKeys.GrpName),
                    groupTask?.Elements.FirstOrDefault(member => member.Field == field.ToString())?.GroupName,
                    isNetworkGroup ? element.FlowNetworkGroupId?.ToString() : null,
                    isServiceGroup ? element.FlowServiceGroupId?.ToString() : null) ?? "";
                return AddZones(element, name);
            }

            return ResolvedElementValue(element);
        }

        private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private IEnumerable<WfReqElement> ExpandTicketGroup(WfReqElement element, ElemFieldType field)
        {
            if (!IsGroupElement(element, field))
            {
                return Enumerable.Repeat(element, 1);
            }

            string groupName = element.Name ?? "";
            if (!string.IsNullOrWhiteSpace(element.GroupName))
            {
                groupName = element.GroupName;
            }
            string memberField = field == ElemFieldType.service
                ? ElemFieldType.service.ToString()
                : ElemFieldType.source.ToString();

            List<WfReqTask> groupTasks = [.. wfHandler.ActTicket.Tasks
                .Where(IsGroupTask)
                .Where(task => MatchesTicketGroup(task, element, groupName, memberField))];

            List<WfReqElement> members = [.. groupTasks
                .SelectMany(task => task.Elements)
                .Where(member => member.Field == memberField)
                .Where(member => displayMode == ApprovalTaskElementDisplayMode.UnresolvedGroups || !IsRemovedMember(member))
                .Where(member => !string.Equals(member.Name, groupName, StringComparison.OrdinalIgnoreCase)
                    || member.IpString != null || member.Port.HasValue || member.ProtoId.HasValue)];

            if (members.Count > 0)
            {
                return members;
            }

            if (displayMode == ApprovalTaskElementDisplayMode.Resolved && groupTasks.Count > 0)
            {
                return [];
            }

            return [element];
        }

        private static bool IsRemovedMember(WfReqElement element) =>
            string.Equals(element.RequestAction, RequestAction.delete.ToString(), StringComparison.OrdinalIgnoreCase);

        private static bool MatchesTicketGroup(WfReqTask task, WfReqElement element, string groupName, string memberField)
        {
            if (element.ObjectTaskId is > 0 && task.Id == element.ObjectTaskId && IsGroupTask(task))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(groupName)
                && string.Equals(task.GetAddInfoValue(AdditionalInfoKeys.GrpName).Trim(), groupName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return task.Elements.Any(candidate => candidate.Field == memberField
                && ((!string.IsNullOrWhiteSpace(groupName)
                        && (string.Equals(candidate.GroupName?.Trim(), groupName.Trim(), StringComparison.OrdinalIgnoreCase)
                            || string.Equals(candidate.Name?.Trim(), groupName.Trim(), StringComparison.OrdinalIgnoreCase)))
                    || element.FlowNetworkGroupId.HasValue && candidate.FlowNetworkGroupId == element.FlowNetworkGroupId
                    || element.FlowServiceGroupId.HasValue && candidate.FlowServiceGroupId == element.FlowServiceGroupId));
        }

        private string ResolvedElementValue(WfReqElement element)
        {
            WfReqElement displayElement = ResolveObjectTaskElement(element);
            string value = displayElement.Field == ElemFieldType.service.ToString()
                ? ServiceValue(displayElement)
                : ObjectValue(displayElement);
            return AddZones(displayElement, value);
        }

        private string TechnicalElementValue(WfReqElement element)
        {
            WfReqElement displayElement = ResolveObjectTaskElement(element);
            if (displayElement.Field == ElemFieldType.service.ToString())
            {
                string service = DisplayBase.DisplayService(ToServiceElement(displayElement), ipProtocols);
                return AddZones(displayElement, string.IsNullOrWhiteSpace(service) ? ServiceValue(displayElement) : service);
            }

            string ip = NwObjDisplay.DisplayIp(displayElement.IpString, displayElement.IpEnd);
            return AddZones(displayElement, string.IsNullOrWhiteSpace(ip) ? ObjectValue(displayElement) : ip);
        }

        /// <summary>
        /// Resolves the current requested state of a ticket-local object task for display.
        /// </summary>
        private WfReqElement ResolveObjectTaskElement(WfReqElement element)
        {
            return WfObjectTaskHelper.ResolveReferencedElement(element, wfHandler.ActTicket.Tasks);
        }

        private static string ObjectValue(WfReqElement element) =>
            DisplayBase.DisplayIpWithName(WfElementBase.ToNetworkObject(element));

        private string ServiceValue(WfReqElement element)
        {
            string name = element.Name ?? "";
            string details = DisplayBase.DisplayService(ToServiceElement(element), ipProtocols);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, details, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(name) ? details : name;
            }

            if (string.IsNullOrWhiteSpace(details))
            {
                return name;
            }

            return $"{name} ({details})";
        }

        private static NwServiceElement ToServiceElement(WfReqElement element) => new()
        {
            Port = element.Port,
            PortEnd = element.PortEnd,
            ProtoId = element.ProtoId ?? 0,
            HasProtocol = element.ProtoId.HasValue,
            Name = element.Name
        };

        private string AddZones(WfReqElement element, string value)
        {
            if (element.Field != ElemFieldType.source.ToString() && element.Field != ElemFieldType.destination.ToString())
            {
                return value;
            }

            string zones = Zones(element);
            return string.IsNullOrWhiteSpace(zones) ? value : $"{value} ({userConfig.GetText("zone")}: {zones})";
        }

        private string Zones(WfReqElement element)
        {
            var range = element.GetIpRange();
            if (range == null)
            {
                return "";
            }

            return string.Join(", ", networkZones
                .Where(zone => zone.IPRanges.Any(zoneRange => IpOperations.RangeOverlapExists(zoneRange, range)))
                .Select(zone => zone.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct());
        }

        private string FormatDate(DateTime date, bool isEndDate)
        {
            return TimeObjectDisplayHelper.FormatValue(
                date,
                FlowIntegration.TimeObjectPrecision,
                isEndDate,
                "dd.MM.yyyy");
        }

    }
}

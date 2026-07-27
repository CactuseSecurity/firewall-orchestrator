using System.Globalization;
using FWO.Basics;
using FWO.Data;

namespace FWO.Data.Flow
{
    public static class FlowAdminHelper
    {
        /// <summary>
        /// Builds the list of unresolved duplicate flow object links per management.
        /// A group only qualifies if it has multiple linked objects on the same management and none of them are active.
        /// </summary>
        public static List<FlowNwObjectDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowNwObject>? flowObjects, IEnumerable<Management>? managements)
        {
            Dictionary<long, FlowNwObject> flowObjectLookup = (flowObjects ?? []).ToDictionary(flowObject => flowObject.Id);
            List<FlowNwObjectDuplicateGroup> duplicateGroups = [];

            foreach (Management management in managements ?? [])
            {
                foreach (IGrouping<long, NetworkObject> linkedObjectsByFlowObject in (management.Objects ?? [])
                    .Where(nwObject => nwObject.FlowNetworkObjectId.HasValue)
                    .GroupBy(nwObject => nwObject.FlowNetworkObjectId!.Value))
                {
                    if (!flowObjectLookup.TryGetValue(linkedObjectsByFlowObject.Key, out FlowNwObject? flowObject))
                    {
                        continue;
                    }

                    List<NetworkObject> linkedObjects = [.. linkedObjectsByFlowObject
                        .OrderBy(nwObject => nwObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(nwObject => nwObject.Id)];
                    if (linkedObjects.Count <= 1 || linkedObjects.Any(nwObject => nwObject.FlowActive))
                    {
                        continue;
                    }

                    duplicateGroups.Add(new FlowNwObjectDuplicateGroup
                    {
                        FlowNwObjectId = flowObject.Id,
                        FlowNwObjectName = flowObject.Name ?? "",
                        ManagementId = management.Id,
                        ManagementName = management.Name,
                        Objects = linkedObjects
                    });
                }
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowNwObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ManagementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowNwObjectId)
                .ThenBy(group => group.ManagementId)];
        }

        /// <summary>
        /// Builds the list of unresolved duplicate flow network group links.
        /// A group only qualifies if it has multiple linked objects and none of them are active.
        /// </summary>
        public static List<FlowNwGroupDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowNwGroup>? flowGroups, IEnumerable<Management>? managements)
        {
            Dictionary<long, FlowNwGroup> flowGroupLookup = (flowGroups ?? []).ToDictionary(flowGroup => flowGroup.Id);
            List<FlowNwGroupDuplicateGroup> duplicateGroups = [];

            foreach (Management management in managements ?? [])
            {
                foreach (IGrouping<long, NetworkObject> linkedObjectsByFlowGroup in (management.Objects ?? [])
                    .Where(nwObject => nwObject.FlowNetworkGroupId.HasValue)
                    .GroupBy(nwObject => nwObject.FlowNetworkGroupId!.Value))
                {
                    if (!flowGroupLookup.TryGetValue(linkedObjectsByFlowGroup.Key, out FlowNwGroup? flowGroup))
                    {
                        continue;
                    }

                    List<NetworkObject> linkedObjects = [.. linkedObjectsByFlowGroup
                        .OrderBy(nwObject => nwObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(nwObject => nwObject.Id)];
                    if (linkedObjects.Count <= 1 || linkedObjects.Any(nwObject => nwObject.FlowActive))
                    {
                        continue;
                    }

                    duplicateGroups.Add(new FlowNwGroupDuplicateGroup
                    {
                        FlowNwGroupId = flowGroup.Id,
                        FlowNwGroupName = flowGroup.Name,
                        ManagementId = management.Id,
                        ManagementName = management.Name,
                        Objects = linkedObjects
                    });
                }
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowNwGroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ManagementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowNwGroupId)
                .ThenBy(group => group.ManagementId)];
        }

        /// <summary>
        /// Builds the list of unresolved duplicate flow service object links.
        /// A group only qualifies if it has multiple linked services and none of them are active.
        /// </summary>
        public static List<FlowSvcObjectDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowSvcObject>? flowObjects, IEnumerable<Management>? managements)
        {
            Dictionary<long, FlowSvcObject> flowObjectLookup = (flowObjects ?? []).ToDictionary(flowObject => flowObject.Id);
            List<FlowSvcObjectDuplicateGroup> duplicateGroups = [];

            foreach (Management management in managements ?? [])
            {
                foreach (IGrouping<long, NetworkService> linkedServicesByFlowObject in (management.Services ?? [])
                    .Where(service => service.FlowServiceObjectId.HasValue)
                    .GroupBy(service => service.FlowServiceObjectId!.Value))
                {
                    if (!flowObjectLookup.TryGetValue(linkedServicesByFlowObject.Key, out FlowSvcObject? flowObject))
                    {
                        continue;
                    }

                    List<NetworkService> linkedServices = [.. linkedServicesByFlowObject
                        .OrderBy(service => service.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(service => service.Id)];
                    if (linkedServices.Count <= 1 || linkedServices.Any(service => service.FlowActive))
                    {
                        continue;
                    }

                    duplicateGroups.Add(new FlowSvcObjectDuplicateGroup
                    {
                        FlowSvcObjectId = flowObject.Id,
                        FlowSvcObjectName = flowObject.Name,
                        ManagementId = management.Id,
                        ManagementName = management.Name,
                        Services = linkedServices
                    });
                }
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowSvcObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ManagementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowSvcObjectId)
                .ThenBy(group => group.ManagementId)];
        }

        /// <summary>
        /// Builds the list of unresolved duplicate flow service group links.
        /// A group only qualifies if it has multiple linked services and none of them are active.
        /// </summary>
        public static List<FlowSvcGroupDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowSvcGroup>? flowGroups, IEnumerable<Management>? managements)
        {
            Dictionary<long, FlowSvcGroup> flowGroupLookup = (flowGroups ?? []).ToDictionary(flowGroup => flowGroup.Id);
            List<FlowSvcGroupDuplicateGroup> duplicateGroups = [];

            foreach (Management management in managements ?? [])
            {
                foreach (IGrouping<long, NetworkService> linkedServicesByFlowGroup in (management.Services ?? [])
                    .Where(service => service.FlowServiceGroupId.HasValue)
                    .GroupBy(service => service.FlowServiceGroupId!.Value))
                {
                    if (!flowGroupLookup.TryGetValue(linkedServicesByFlowGroup.Key, out FlowSvcGroup? flowGroup))
                    {
                        continue;
                    }

                    List<NetworkService> linkedServices = [.. linkedServicesByFlowGroup
                        .OrderBy(service => service.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(service => service.Id)];
                    if (linkedServices.Count <= 1 || linkedServices.Any(service => service.FlowActive))
                    {
                        continue;
                    }

                    duplicateGroups.Add(new FlowSvcGroupDuplicateGroup
                    {
                        FlowSvcGroupId = flowGroup.Id,
                        FlowSvcGroupName = flowGroup.Name,
                        ManagementId = management.Id,
                        ManagementName = management.Name,
                        Services = linkedServices
                    });
                }
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowSvcGroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ManagementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowSvcGroupId)
                .ThenBy(group => group.ManagementId)];
        }

        /// <summary>
        /// Builds the list of unresolved duplicate flow time object links.
        /// A group only qualifies if it has multiple linked time objects and none of them are active.
        /// </summary>
        public static List<FlowTimeObjectDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowTimeObject>? flowObjects, IEnumerable<Management>? managements)
        {
            Dictionary<long, FlowTimeObject> flowObjectLookup = (flowObjects ?? []).ToDictionary(flowObject => flowObject.Id);
            List<FlowTimeObjectDuplicateGroup> duplicateGroups = [];

            foreach (Management management in managements ?? [])
            {
                foreach (IGrouping<long, TimeObject> linkedTimeObjectsByFlowObject in (management.TimeObjects ?? [])
                    .Where(timeObject => timeObject.FlowTimeObjectId.HasValue)
                    .GroupBy(timeObject => timeObject.FlowTimeObjectId!.Value))
                {
                    if (!flowObjectLookup.TryGetValue(linkedTimeObjectsByFlowObject.Key, out FlowTimeObject? flowObject))
                    {
                        continue;
                    }

                    List<TimeObject> linkedTimeObjects = [.. linkedTimeObjectsByFlowObject
                        .OrderBy(timeObject => timeObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(timeObject => timeObject.Id)];
                    if (linkedTimeObjects.Count <= 1 || linkedTimeObjects.Any(timeObject => timeObject.FlowActive))
                    {
                        continue;
                    }

                    duplicateGroups.Add(new FlowTimeObjectDuplicateGroup
                    {
                        FlowTimeObjectId = flowObject.Id,
                        FlowTimeObjectName = flowObject.Name,
                        ManagementId = management.Id,
                        ManagementName = management.Name,
                        TimeObjects = linkedTimeObjects
                    });
                }
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowTimeObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ManagementName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowTimeObjectId)
                .ThenBy(group => group.ManagementId)];
        }

        /// <summary>
        /// Returns true when the object does not have an IP address or range and can therefore be used as a custom
        /// firewall object candidate.
        /// </summary>
        public static bool HasNoTechnicalAddress(NetworkObject candidate)
        {
            return string.IsNullOrWhiteSpace(candidate.IP) &&
                   string.IsNullOrWhiteSpace(candidate.IpEnd);
        }

        /// <summary>
        /// Formats the technical details of a network object for duplicate resolution views.
        /// </summary>
        public static string FormatNetworkObjectTechnicalDetails(NetworkObject candidate, bool includeTechnicalId = true)
        {
            string details = HasNoTechnicalAddress(candidate)
                ? (candidate.Name ?? "")
                : DisplayBase.DisplayIpWithName(candidate);
            if (!includeTechnicalId)
            {
                return string.IsNullOrWhiteSpace(details) ? "-" : details;
            }

            string technicalId = string.IsNullOrWhiteSpace(candidate.Uid)
                ? $"#{candidate.Id}"
                : candidate.Uid;

            return string.IsNullOrWhiteSpace(details)
                ? technicalId
                : $"{details} [{technicalId}]";
        }

        /// <summary>
        /// Formats the technical details of a flow network object for duplicate resolution views.
        /// </summary>
        public static string FormatFlowNwObjectTechnicalDetails(FlowNwObject candidate)
        {
            string ipStart = candidate.IpStart ?? "";
            string ipEnd = candidate.IpEnd ?? "";
            return DisplayBase.DisplayIp(ipStart, ipEnd);
        }

        /// <summary>
        /// Formats the technical details of a flow network group for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowNwGroupTechnicalDetails(FlowNwGroup candidate, string membersLabel)
        {
            return $"{candidate.NwGroupMembers.Count} {membersLabel}";
        }

        /// <summary>
        /// Formats a compact member preview for flow network groups.
        /// </summary>
        public static string FormatFlowNwGroupMemberDetails(FlowNwGroup candidate, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(
                candidate.NwGroupMembers.Select(member => member.NwObject),
                maxItems,
                emptyLabel,
                moreTemplate,
                FormatFlowNwObjectTechnicalDetails);
        }

        /// <summary>
        /// Formats the technical details of a flow service object for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowSvcObjectTechnicalDetails(FlowSvcObject candidate, IEnumerable<IpProtocol>? protocols = null)
        {
            string portRange = DisplayBase.DisplayPort(candidate.PortStart, candidate.PortEnd);
            string protocol = protocols?.FirstOrDefault(protocol => protocol.Id == candidate.ProtoId)?.Name
                ?? (candidate.ProtoId > 0 ? candidate.ProtoId.ToString(CultureInfo.InvariantCulture) : "");

            if (string.IsNullOrWhiteSpace(protocol))
            {
                return portRange;
            }

            if (string.IsNullOrWhiteSpace(portRange))
            {
                return protocol;
            }

            return $"{portRange}/{protocol}";
        }

        /// <summary>
        /// Formats the technical details of a flow service group for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowSvcGroupTechnicalDetails(FlowSvcGroup candidate, string membersLabel)
        {
            return $"{candidate.SvcGroupMembers.Count} {membersLabel}";
        }

        /// <summary>
        /// Formats a compact member preview for flow service groups.
        /// </summary>
        public static string FormatFlowSvcGroupMemberDetails(FlowSvcGroup candidate, int maxItems, string emptyLabel, string moreTemplate, IEnumerable<IpProtocol>? protocols = null)
        {
            return FormatDuplicateObjectSummary(
                candidate.SvcGroupMembers.Select(member => member.SvcObject),
                maxItems,
                emptyLabel,
                moreTemplate,
                service => FormatFlowSvcObjectTechnicalDetails(service, protocols));
        }

        /// <summary>
        /// Formats the technical details of a flow time object for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowTimeObjectTechnicalDetails(FlowTimeObject candidate)
        {
            return FormatTimeRangeTechnicalDetails(candidate.StartTime, candidate.EndTime);
        }

        private static string FormatTimeRangeTechnicalDetails(DateTime? startTime, DateTime? endTime)
        {
            List<string> parts = [];
            if (startTime.HasValue)
            {
                parts.Add(startTime.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            }

            if (endTime.HasValue)
            {
                parts.Add(endTime.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            }

            return string.Join(" - ", parts);
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of network objects.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<NetworkObject>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, candidate => FormatNetworkObjectTechnicalDetails(candidate));
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of network services.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<NetworkService>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, candidate => FormatNetworkServiceTechnicalDetails(candidate));
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of time objects.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<TimeObject>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, FormatTimeObjectTechnicalDetails);
        }

        private static string FormatDuplicateObjectSummary<T>(IEnumerable<T>? objects, int maxItems, string emptyLabel, string moreTemplate, Func<T, string> technicalDetailsFormatter)
        {
            List<T> duplicateObjects = [.. (objects ?? [])];
            if (duplicateObjects.Count == 0)
            {
                return emptyLabel;
            }

            int previewCount = Math.Max(maxItems, 0);
            IEnumerable<string> details = duplicateObjects
                .Take(previewCount)
                .Select(technicalDetailsFormatter);

            string summary = string.Join(", ", details);
            if (duplicateObjects.Count <= previewCount)
            {
                return summary;
            }

            int remainingCount = duplicateObjects.Count - previewCount;
            string moreText = moreTemplate.Replace(Placeholder.COUNT, remainingCount.ToString(CultureInfo.InvariantCulture));
            return string.IsNullOrWhiteSpace(summary)
                ? moreText
                : $"{summary}, {moreText}";
        }

        /// <summary>
        /// Formats the technical details of a network service for duplicate resolution views.
        /// </summary>
        public static string FormatNetworkServiceTechnicalDetails(NetworkService candidate, bool includeTechnicalId = true)
        {
            string details = DisplayBase.DisplayService(candidate, false).ToString();
            if (!includeTechnicalId)
            {
                return string.IsNullOrWhiteSpace(details) ? "-" : details;
            }

            string technicalId = string.IsNullOrWhiteSpace(candidate.Uid)
                ? $"#{candidate.Id}"
                : candidate.Uid;

            return string.IsNullOrWhiteSpace(details)
                ? technicalId
                : $"{details} [{technicalId}]";
        }

        /// <summary>
        /// Formats the technical details of a time object for duplicate resolution views.
        /// </summary>
        public static string FormatTimeObjectTechnicalDetails(TimeObject candidate)
        {
            string details = FormatTimeRangeTechnicalDetails(candidate.StartTime, candidate.EndTime);
            string technicalId = string.IsNullOrWhiteSpace(candidate.Uid)
                ? $"#{candidate.Id}"
                : candidate.Uid;

            return string.IsNullOrWhiteSpace(details)
                ? technicalId
                : $"{details} [{technicalId}]";
        }

        public static void MergeServiceObjectMappingUpdate(NetworkService cachedService, NetworkService updatedService)
        {
            MergeNetworkServiceMappingFields(cachedService, updatedService);
            cachedService.FlowServiceObjectId = updatedService.FlowServiceObjectId;
            cachedService.FlowServiceGroupId = null;
        }

        public static void MergeServiceGroupMappingUpdate(NetworkService cachedService, NetworkService updatedService)
        {
            MergeNetworkServiceMappingFields(cachedService, updatedService);
            cachedService.FlowServiceGroupId = updatedService.FlowServiceGroupId;
            cachedService.FlowServiceObjectId = null;
        }

        public static void MergeNetworkObjectMappingUpdate(NetworkObject cachedObject, NetworkObject updatedObject)
        {
            MergeNetworkObjectMappingFields(cachedObject, updatedObject);
            cachedObject.FlowNetworkObjectId = updatedObject.FlowNetworkObjectId;
            cachedObject.FlowNetworkGroupId = null;
        }

        public static void MergeNetworkGroupMappingUpdate(NetworkObject cachedObject, NetworkObject updatedObject)
        {
            MergeNetworkObjectMappingFields(cachedObject, updatedObject);
            cachedObject.FlowNetworkGroupId = updatedObject.FlowNetworkGroupId;
            cachedObject.FlowNetworkObjectId = null;
        }

        private static void MergeNetworkServiceMappingFields(NetworkService cachedService, NetworkService updatedService)
        {
            cachedService.Name = updatedService.Name;
            cachedService.Uid = updatedService.Uid;
            cachedService.DestinationPort = updatedService.DestinationPort;
            cachedService.DestinationPortEnd = updatedService.DestinationPortEnd;
            cachedService.Active = updatedService.Active;
            cachedService.Removed = updatedService.Removed;
            cachedService.FlowActive = updatedService.FlowActive;
        }

        private static void MergeNetworkObjectMappingFields(NetworkObject cachedObject, NetworkObject updatedObject)
        {
            cachedObject.Name = updatedObject.Name;
            cachedObject.IP = updatedObject.IP;
            cachedObject.IpEnd = updatedObject.IpEnd;
            cachedObject.Uid = updatedObject.Uid;
            cachedObject.Active = updatedObject.Active;
            cachedObject.Removed = updatedObject.Removed;
            cachedObject.FlowActive = updatedObject.FlowActive;
        }
    }

    public class FlowNwObjectDuplicateGroup
    {
        public long FlowNwObjectId { get; set; }
        public string FlowNwObjectName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<NetworkObject> Objects { get; set; } = [];
    }

    public class FlowNwGroupDuplicateGroup
    {
        public long FlowNwGroupId { get; set; }
        public string FlowNwGroupName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<NetworkObject> Objects { get; set; } = [];
    }

    public class FlowSvcObjectDuplicateGroup
    {
        public long FlowSvcObjectId { get; set; }
        public string FlowSvcObjectName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<NetworkService> Services { get; set; } = [];
    }

    public class FlowSvcGroupDuplicateGroup
    {
        public long FlowSvcGroupId { get; set; }
        public string FlowSvcGroupName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<NetworkService> Services { get; set; } = [];
    }

    public class FlowTimeObjectDuplicateGroup
    {
        public long FlowTimeObjectId { get; set; }
        public string FlowTimeObjectName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<TimeObject> TimeObjects { get; set; } = [];
    }
}

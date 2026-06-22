using System.Globalization;
using FWO.Data;

namespace FWO.Data.Flow
{
    public static class FlowAdminHelper
    {
        /// <summary>
        /// Builds the list of unresolved duplicate flow object links.
        /// A group only qualifies if it has multiple linked objects and none of them are active.
        /// </summary>
        public static List<FlowNwObjectDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowNwObject>? flowObjects)
        {
            List<FlowNwObjectDuplicateGroup> duplicateGroups = [];

            foreach (FlowNwObject flowObject in flowObjects ?? [])
            {
                List<NetworkObject> linkedObjects = [.. (flowObject.Objects ?? [])];
                if (linkedObjects.Count <= 1 || linkedObjects.Any(nwObject => nwObject.FlowActive))
                {
                    continue;
                }

                duplicateGroups.Add(new FlowNwObjectDuplicateGroup
                {
                    FlowNwObjectId = flowObject.Id,
                    FlowNwObjectName = flowObject.Name ?? "",
                    Objects = [.. linkedObjects
                        .OrderBy(nwObject => nwObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(nwObject => nwObject.Id)]
                });
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowNwObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowNwObjectId)
                .ThenBy(group => group.Objects.Count)];
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
                        Objects = [.. linkedObjects
                            .OrderBy(nwObject => nwObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                            .ThenBy(nwObject => nwObject.Id)]
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
                        Services = [.. linkedServices
                            .OrderBy(service => service.Name ?? "", StringComparer.OrdinalIgnoreCase)
                            .ThenBy(service => service.Id)]
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
                        Services = [.. linkedServices
                            .OrderBy(service => service.Name ?? "", StringComparer.OrdinalIgnoreCase)
                            .ThenBy(service => service.Id)]
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
                        TimeObjects = [.. linkedTimeObjects
                            .OrderBy(timeObject => timeObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                            .ThenBy(timeObject => timeObject.Id)]
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
        /// Builds a searchable text blob for a custom flow object candidate.
        /// </summary>
        public static string BuildCustomObjectSearchText(NetworkObject candidate)
        {
            return string.Join(' ', [
                candidate.Id.ToString(),
                candidate.Name ?? "",
                candidate.IP ?? "",
                candidate.IpEnd ?? "",
                candidate.Uid ?? "",
                candidate.Active ? "active" : "inactive",
                candidate.Type?.Id.ToString() ?? "",
                candidate.Type?.Name ?? ""
            ]).ToLowerInvariant();
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
        public static string FormatNetworkObjectTechnicalDetails(NetworkObject candidate)
        {
            string details = HasNoTechnicalAddress(candidate)
                ? (candidate.Name ?? "")
                : DisplayBase.DisplayIpWithName(candidate);
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
            return DisplayBase.DisplayIp(candidate.IpStart ?? "", candidate.IpEnd ?? "");
        }

        /// <summary>
        /// Formats the technical details of a flow network group for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowNwGroupTechnicalDetails(FlowNwGroup candidate, string membersLabel)
        {
            return $"{candidate.NwGroupMembers.Count} {membersLabel}";
        }

        /// <summary>
        /// Formats the technical details of a flow service object for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowSvcObjectTechnicalDetails(FlowSvcObject candidate, IEnumerable<IpProtocol>? protocols = null)
        {
            string portRange = DisplayBase.DisplayPort(candidate.PortStart, candidate.PortEnd);
            string protocol = protocols?.FirstOrDefault(protocol => protocol.Id == candidate.ProtoId)?.Name
                ?? (candidate.ProtoId > 0 ? candidate.ProtoId.ToString(CultureInfo.InvariantCulture) : "");
            return string.IsNullOrWhiteSpace(protocol)
                ? portRange
                : string.IsNullOrWhiteSpace(portRange)
                    ? protocol
                    : $"{portRange}/{protocol}";
        }

        /// <summary>
        /// Formats the technical details of a flow service group for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowSvcGroupTechnicalDetails(FlowSvcGroup candidate, string membersLabel)
        {
            return $"{candidate.SvcGroupMembers.Count} {membersLabel}";
        }

        /// <summary>
        /// Formats the technical details of a flow time object for overview and duplicate resolution views.
        /// </summary>
        public static string FormatFlowTimeObjectTechnicalDetails(FlowTimeObject candidate)
        {
            List<string> parts = [];
            if (candidate.StartTime.HasValue)
            {
                parts.Add(candidate.StartTime.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            }

            if (candidate.EndTime.HasValue)
            {
                parts.Add(candidate.EndTime.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            }

            return string.Join(" - ", parts);
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of network objects.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<NetworkObject>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, FormatNetworkObjectTechnicalDetails);
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of network services.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<NetworkService>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, FormatNetworkServiceTechnicalDetails);
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of time objects.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<TimeObject>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, FormatTimeObjectTechnicalDetails);
        }

        /// <summary>
        /// Formats a compact duplicate overview for a list of flow network objects.
        /// </summary>
        public static string FormatDuplicateObjectSummary(IEnumerable<FlowNwObject>? objects, int maxItems, string emptyLabel, string moreTemplate)
        {
            return FormatDuplicateObjectSummary(objects, maxItems, emptyLabel, moreTemplate, FormatFlowNwObjectTechnicalDetails);
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
            string moreText = moreTemplate.Replace("@@COUNT@@", remainingCount.ToString(CultureInfo.InvariantCulture));
            return string.IsNullOrWhiteSpace(summary)
                ? moreText
                : $"{summary}, {moreText}";
        }

        /// <summary>
        /// Filters custom flow object candidates by a case-insensitive search string.
        /// </summary>
        public static List<NetworkObject> FilterCustomObjectCandidates(IEnumerable<NetworkObject>? candidates, string? searchText)
        {
            IEnumerable<NetworkObject> filteredCandidates = candidates ?? [];

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string normalizedSearchText = searchText.Trim();
                filteredCandidates = filteredCandidates.Where(candidate =>
                    BuildCustomObjectSearchText(candidate).Contains(normalizedSearchText, StringComparison.OrdinalIgnoreCase));
            }

            return [.. filteredCandidates
                .OrderBy(candidate => candidate.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Id)];
        }

        /// <summary>
        /// Formats the technical details of a network service for duplicate resolution views.
        /// </summary>
        public static string FormatNetworkServiceTechnicalDetails(NetworkService candidate)
        {
            string details = DisplayBase.DisplayService(candidate, false).ToString();
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
            string details = FormatFlowTimeObjectTechnicalDetails(new FlowTimeObject
            {
                StartTime = candidate.StartTime,
                EndTime = candidate.EndTime
            });
            string technicalId = string.IsNullOrWhiteSpace(candidate.Uid)
                ? $"#{candidate.Id}"
                : candidate.Uid;

            return string.IsNullOrWhiteSpace(details)
                ? technicalId
                : $"{details} [{technicalId}]";
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

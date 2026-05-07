using System.Text.Json;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.Services.Modelling
{
    public class ModellingNotificationRequestBuilder(UserConfig userConfig, List<ModellingHistoryEntry>? historyEntries = null, DateTime? lastRequestStartedAt = null)
    {
        private readonly string stateMarker = ModIntegrationStateConfig.EffectiveMarker(userConfig.ModIntegrationStateMarker);
        private readonly HashSet<string> includedRequestStateNames = ModIntegrationStateConfig.IncludedRequestStateNames(userConfig.ModIntegrationStates);
        private readonly List<ModellingHistoryEntry> history = historyEntries ?? [];

        public List<WfReqTask> BuildRequestTasks(List<ModellingConnection> connections, FwoOwner owner, int stateId)
        {
            int taskNumber = 1;
            List<ModellingConnection> includedConnections = [.. connections
                .Where(IsConnectionIncludedForRequest)
                .OrderByDescending(connection => connection.IsCommonService)
                .ThenBy(connection => connection.Id)];
            return
            [
                .. BuildGroupTasks(includedConnections, owner, stateId, ref taskNumber),
                .. includedConnections.Select(connection => BuildAccessTask(connection, owner, stateId, taskNumber++))
            ];
        }

        private WfReqTask BuildAccessTask(ModellingConnection connection, FwoOwner owner, int stateId, int taskNumber)
        {
            Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.ConnId, connection.Id.ToString() } };
            return new()
            {
                Title = BuildTitle(connection),
                TaskNumber = taskNumber,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                StateId = stateId,
                Owners = [new() { Owner = owner }],
                Elements = BuildElements(connection),
                RuleAction = 1,
                Tracking = 1,
                AdditionalInfo = JsonSerializer.Serialize(addInfo),
                Comments = [new() { Comment = new() { CommentText = connection.Reason ?? "" } }]
            };
        }

        private string BuildTitle(ModellingConnection connection)
        {
            string marker = $" ({userConfig.ModModelledMarker}{connection.Id})";
            string titleKey = connection.IsCommonService ? "new_common_service" : "new_connection";
            return $"{userConfig.GetText(titleKey)}: {connection.Name ?? ""}{marker}";
        }

        private List<WfReqTask> BuildGroupTasks(List<ModellingConnection> connections, FwoOwner owner, int stateId, ref int taskNumber)
        {
            List<WfReqTask> groupTasks = [];
            foreach (ModellingAppRole appRole in IncludedAppRoles(connections))
            {
                groupTasks.Add(BuildAppRoleTask(appRole, owner, stateId, taskNumber++));
            }
            foreach (ModellingServiceGroup serviceGroup in IncludedServiceGroups(connections))
            {
                groupTasks.Add(BuildServiceGroupTask(serviceGroup, owner, stateId, taskNumber++));
            }
            return groupTasks;
        }

        private IEnumerable<ModellingAppRole> IncludedAppRoles(List<ModellingConnection> connections)
        {
            return connections
                .SelectMany(connection => connection.SourceAppRoles.Concat(connection.DestinationAppRoles).Select(wrapper => wrapper.Content))
                .Where(group => group.Id > 0)
                .Where(group => IsGroupIncluded(group.Comment, GroupObjectType(group), group.Id))
                .GroupBy(group => group.Id > 0 ? group.Id.ToString() : group.IdString)
                .Select(group => group.First())
                .OrderBy(group => group.IdString);
        }

        private IEnumerable<ModellingServiceGroup> IncludedServiceGroups(List<ModellingConnection> connections)
        {
            return connections
                .SelectMany(connection => connection.ServiceGroups.Select(wrapper => wrapper.Content))
                .Where(group => group.Id > 0)
                .Where(group => IsGroupIncluded(group.Comment, ModellingTypes.ModObjectType.ServiceGroup, group.Id))
                .GroupBy(group => group.Id > 0 ? group.Id.ToString() : group.Name ?? "")
                .Select(group => group.First())
                .OrderBy(group => group.Name);
        }

        private WfReqTask BuildAppRoleTask(ModellingAppRole appRole, FwoOwner owner, int stateId, int taskNumber)
        {
            Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.GrpName, appRole.IdString }, { AdditionalInfoKeys.AppRoleId, appRole.Id.ToString() } };
            return new()
            {
                Title = userConfig.GetText("new_app_role") + appRole.IdString,
                TaskNumber = taskNumber,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                StateId = stateId,
                Owners = [new() { Owner = owner }],
                Elements = BuildAppRoleMembers(appRole),
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            };
        }

        private WfReqTask BuildServiceGroupTask(ModellingServiceGroup serviceGroup, FwoOwner owner, int stateId, int taskNumber)
        {
            Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.GrpName, serviceGroup.Name ?? "" }, { AdditionalInfoKeys.SvcGrpId, serviceGroup.Id.ToString() } };
            return new()
            {
                Title = userConfig.GetText("new_svc_grp") + serviceGroup.Name,
                TaskNumber = taskNumber,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                StateId = stateId,
                Owners = [new() { Owner = owner }],
                Elements = BuildServiceGroupMembers(serviceGroup),
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            };
        }

        private static List<WfReqElement> BuildAppRoleMembers(ModellingAppRole appRole)
        {
            return [.. ModellingAppServerWrapper.Resolve(appRole.AppServers).Select(appServer => new WfReqElement
            {
                RequestAction = RequestAction.create.ToString(),
                Field = ElemFieldType.source.ToString(),
                Name = appServer.Name,
                IpString = appServer.Ip,
                IpEnd = appServer.IpEnd,
                GroupName = appRole.IdString
            })];
        }

        private static List<WfReqElement> BuildServiceGroupMembers(ModellingServiceGroup serviceGroup)
        {
            return [.. ModellingServiceWrapper.Resolve(serviceGroup.Services).Select(service => new WfReqElement
            {
                RequestAction = RequestAction.create.ToString(),
                Field = ElemFieldType.service.ToString(),
                Name = service.Name,
                Port = service.Port,
                PortEnd = service.PortEnd,
                ProtoId = service.ProtoId,
                GroupName = serviceGroup.Name
            })];
        }

        private bool IsConnectionIncludedForRequest(ModellingConnection connection)
        {
            if (!connection.NotAlreadyRequested())
            {
                return false;
            }

            string stateName = ModIntegrationStateConfig.GetStateName(connection.GetStringProperty(stateMarker));
            if (!ModIntegrationStateConfig.IsIncludedForRequest(stateName, includedRequestStateNames))
            {
                return false;
            }
            return !ModIntegrationStateConfig.RequiresChangeSinceMarker(stateName, includedRequestStateNames) ||
                HasChangedSince(ModellingTypes.ModObjectType.Connection, connection.Id, connection.GetIntegrationStateSetAt(stateMarker));
        }

        private bool IsGroupIncluded(string? comment, ModellingTypes.ModObjectType objectType, long objectId)
        {
            string stateName = ModIntegrationStateConfig.GetMarkedCommentValue(comment, stateMarker);
            if (!ModIntegrationStateConfig.IsIncludedForRequest(stateName, includedRequestStateNames))
            {
                return false;
            }
            return !ModIntegrationStateConfig.RequiresChangeSinceMarker(stateName, includedRequestStateNames) ||
                HasChangedSince(objectType, objectId, ModIntegrationStateConfig.GetMarkedCommentTimestamp(comment, stateMarker));
        }

        private bool HasChangedSince(ModellingTypes.ModObjectType objectType, long objectId, DateTime? stateSetAt)
        {
            DateTime? baseline = lastRequestStartedAt ?? stateSetAt;
            return baseline == null || history.Any(entry =>
                entry.ObjectType == (int)objectType &&
                entry.ObjectId == objectId &&
                entry.ChangeTime != null &&
                entry.ChangeTime.Value.ToUniversalTime() > baseline.Value.ToUniversalTime());
        }

        private static ModellingTypes.ModObjectType GroupObjectType(ModellingAppRole appRole)
        {
            return appRole is ModellingAppZone ? ModellingTypes.ModObjectType.AppZone : ModellingTypes.ModObjectType.AppRole;
        }

        private List<WfReqElement> BuildElements(ModellingConnection connection)
        {
            return
            [
                .. BuildNetworkElements(connection.SourceAreas.Select(wrapper => wrapper.Content.IdString), ElemFieldType.source),
                .. BuildNetworkGroupElements(connection.SourceAppRoles.Select(wrapper => wrapper.Content), ElemFieldType.source),
                .. BuildNetworkElements(connection.SourceOtherGroups.Select(wrapper => wrapper.Content.IdString), ElemFieldType.source),
                .. BuildNetworkElements(connection.SourceAppServers.Select(wrapper => wrapper.Content), ElemFieldType.source),
                .. BuildNetworkElements(connection.DestinationAreas.Select(wrapper => wrapper.Content.IdString), ElemFieldType.destination),
                .. BuildNetworkGroupElements(connection.DestinationAppRoles.Select(wrapper => wrapper.Content), ElemFieldType.destination),
                .. BuildNetworkElements(connection.DestinationOtherGroups.Select(wrapper => wrapper.Content.IdString), ElemFieldType.destination),
                .. BuildNetworkElements(connection.DestinationAppServers.Select(wrapper => wrapper.Content), ElemFieldType.destination),
                .. BuildServiceGroupElements(connection.ServiceGroups.Select(wrapper => wrapper.Content)),
                .. BuildServiceElements(connection.Services.Select(wrapper => wrapper.Content))
            ];
        }

        private static IEnumerable<WfReqElement> BuildNetworkElements(IEnumerable<string> groupNames, ElemFieldType field)
        {
            return groupNames
                .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
                .Select(groupName => new WfReqElement
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = field.ToString(),
                    GroupName = groupName
                });
        }

        private IEnumerable<WfReqElement> BuildNetworkGroupElements(IEnumerable<ModellingAppRole> groups, ElemFieldType field)
        {
            return groups
                .Where(group => IsGroupIncluded(group.Comment, GroupObjectType(group), group.Id))
                .Select(group => group.IdString)
                .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
                .Select(groupName => new WfReqElement
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = field.ToString(),
                    GroupName = groupName
                });
        }

        private static IEnumerable<WfReqElement> BuildNetworkElements(IEnumerable<ModellingAppServer> appServers, ElemFieldType field)
        {
            return appServers.Select(appServer => new WfReqElement
            {
                RequestAction = RequestAction.create.ToString(),
                Field = field.ToString(),
                Name = appServer.Name,
                IpString = appServer.Ip,
                IpEnd = appServer.IpEnd
            });
        }

        private static IEnumerable<WfReqElement> BuildServiceElements(IEnumerable<string> serviceGroupNames)
        {
            return serviceGroupNames
                .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
                .Select(groupName => new WfReqElement
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.service.ToString(),
                    GroupName = groupName
                });
        }

        private IEnumerable<WfReqElement> BuildServiceGroupElements(IEnumerable<ModellingServiceGroup> serviceGroups)
        {
            return serviceGroups
                .Where(group => IsGroupIncluded(group.Comment, ModellingTypes.ModObjectType.ServiceGroup, group.Id))
                .Select(group => group.Name ?? "")
                .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
                .Select(groupName => new WfReqElement
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.service.ToString(),
                    GroupName = groupName
                });
        }

        private static IEnumerable<WfReqElement> BuildServiceElements(IEnumerable<ModellingService> services)
        {
            return services.Select(service => new WfReqElement
            {
                RequestAction = RequestAction.create.ToString(),
                Field = ElemFieldType.service.ToString(),
                Name = service.Name,
                Port = service.Port,
                PortEnd = service.PortEnd,
                ProtoId = service.ProtoId
            });
        }
    }
}

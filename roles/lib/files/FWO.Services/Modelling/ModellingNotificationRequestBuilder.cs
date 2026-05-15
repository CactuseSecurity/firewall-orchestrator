using System.Text.Json;
using FWO.Basics;
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
            List<ModellingConnection> groupConnections = [.. connections
                .Where(connection => connection.NotAlreadyRequested() || includedConnections.Contains(connection))];
            return
            [
                .. BuildGroupTasks(groupConnections, owner, stateId, ref taskNumber),
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
                .Where(IsAppRoleIncluded)
                .GroupBy(group => group.Id > 0 ? group.Id.ToString() : group.IdString)
                .Select(group => group.First())
                .OrderBy(group => group.IdString);
        }

        private IEnumerable<ModellingServiceGroup> IncludedServiceGroups(List<ModellingConnection> connections)
        {
            return connections
                .SelectMany(connection => connection.ServiceGroups.Select(wrapper => wrapper.Content))
                .Where(group => group.Id > 0)
                .Where(IsServiceGroupIncluded)
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
                Cidr = new Cidr(appServer.Ip ?? ""),
                CidrEnd = new Cidr(appServer.IpEnd ?? appServer.Ip ?? ""),
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
            string stateName = ModIntegrationStateConfig.GetStateName(connection.GetStringProperty(stateMarker));
            if (!ModIntegrationStateConfig.IsIncludedForRequest(stateName, includedRequestStateNames))
            {
                return false;
            }
            bool requiresChangeSinceMarker = ModIntegrationStateConfig.RequiresChangeSinceMarker(stateName, includedRequestStateNames);
            if (connection.NotAlreadyRequested() && !requiresChangeSinceMarker)
            {
                return true;
            }
            return requiresChangeSinceMarker && HasConnectionChangedSince(connection, connection.GetIntegrationStateSetAt(stateMarker));
        }

        private bool IsAppRoleIncluded(ModellingAppRole appRole)
        {
            return IsMarkedObjectIncluded(appRole.Comment, GroupObjectType(appRole), appRole.Id, AppRoleMemberKeys(appRole));
        }

        private bool IsServiceGroupIncluded(ModellingServiceGroup serviceGroup)
        {
            return IsMarkedObjectIncluded(serviceGroup.Comment, ModellingTypes.ModObjectType.ServiceGroup, serviceGroup.Id, ServiceGroupMemberKeys(serviceGroup));
        }

        private bool IsMarkedObjectIncluded(string? comment, ModellingTypes.ModObjectType objectType, long objectId, IEnumerable<(int ObjectType, long ObjectId)> referencedObjectKeys)
        {
            string stateName = ModIntegrationStateConfig.GetMarkedCommentValue(comment, stateMarker);
            if (!ModIntegrationStateConfig.IsIncludedForRequest(stateName, includedRequestStateNames))
            {
                return false;
            }
            return !ModIntegrationStateConfig.RequiresChangeSinceMarker(stateName, includedRequestStateNames) ||
                HasAnyChangedSince([((int)objectType, objectId), .. referencedObjectKeys], ModIntegrationStateConfig.GetMarkedCommentTimestamp(comment, stateMarker));
        }

        private bool HasConnectionChangedSince(ModellingConnection connection, DateTime? stateSetAt)
        {
            return HasAnyChangedSince([((int)ModellingTypes.ModObjectType.Connection, connection.Id), .. ReferencedObjectKeys(connection)], stateSetAt);
        }

        private bool HasAnyChangedSince(IEnumerable<(int ObjectType, long ObjectId)> objectKeys, DateTime? stateSetAt)
        {
            DateTime? baseline = lastRequestStartedAt ?? stateSetAt;
            if (baseline == null)
            {
                return true;
            }

            HashSet<(int ObjectType, long ObjectId)> changedObjects = [.. history
                .Where(entry => entry.ChangeTime != null && entry.ChangeTime.Value.ToUniversalTime() > baseline.Value.ToUniversalTime())
                .Select(entry => (entry.ObjectType, entry.ObjectId))];
            return objectKeys.Where(key => key.ObjectId > 0).Any(changedObjects.Contains);
        }

        private static IEnumerable<(int ObjectType, long ObjectId)> ReferencedObjectKeys(ModellingConnection connection)
        {
            foreach (ModellingAppServer appServer in connection.SourceAppServers.Concat(connection.DestinationAppServers).Select(wrapper => wrapper.Content))
            {
                yield return ((int)ModellingTypes.ModObjectType.AppServer, appServer.Id);
            }
            foreach (ModellingAppRole appRole in connection.SourceAppRoles.Concat(connection.DestinationAppRoles).Select(wrapper => wrapper.Content))
            {
                yield return ((int)GroupObjectType(appRole), appRole.Id);
                foreach ((int ObjectType, long ObjectId) appRoleMemberKey in AppRoleMemberKeys(appRole))
                {
                    yield return appRoleMemberKey;
                }
            }
            foreach (ModellingNetworkArea area in connection.SourceAreas.Concat(connection.DestinationAreas).Select(wrapper => wrapper.Content))
            {
                yield return ((int)ModellingTypes.ModObjectType.NetworkArea, area.Id);
                foreach (NetworkSubnet subnet in area.IpData.Select(wrapper => wrapper.Content))
                {
                    yield return ((int)ModellingTypes.ModObjectType.Network, subnet.Id);
                }
            }
            foreach (ModellingNwGroup group in connection.SourceOtherGroups.Concat(connection.DestinationOtherGroups).Select(wrapper => wrapper.Content))
            {
                yield return ((int)ObjectTypeFromGroup(group), group.Id);
            }
            foreach (ModellingService service in connection.Services.Select(wrapper => wrapper.Content))
            {
                yield return ((int)ModellingTypes.ModObjectType.Service, service.Id);
            }
            foreach (ModellingServiceGroup serviceGroup in connection.ServiceGroups.Select(wrapper => wrapper.Content))
            {
                yield return ((int)ModellingTypes.ModObjectType.ServiceGroup, serviceGroup.Id);
                foreach ((int ObjectType, long ObjectId) serviceGroupMemberKey in ServiceGroupMemberKeys(serviceGroup))
                {
                    yield return serviceGroupMemberKey;
                }
            }
        }

        private static IEnumerable<(int ObjectType, long ObjectId)> AppRoleMemberKeys(ModellingAppRole appRole)
        {
            return appRole.AppServers.Select(wrapper => ((int)ModellingTypes.ModObjectType.AppServer, wrapper.Content.Id));
        }

        private static IEnumerable<(int ObjectType, long ObjectId)> ServiceGroupMemberKeys(ModellingServiceGroup serviceGroup)
        {
            return serviceGroup.Services.Select(wrapper => ((int)ModellingTypes.ModObjectType.Service, (long)wrapper.Content.Id));
        }

        private static ModellingTypes.ModObjectType GroupObjectType(ModellingAppRole appRole)
        {
            return appRole is ModellingAppZone ? ModellingTypes.ModObjectType.AppZone : ModellingTypes.ModObjectType.AppRole;
        }

        private static ModellingTypes.ModObjectType ObjectTypeFromGroup(ModellingNwGroup group)
        {
            return Enum.IsDefined(typeof(ModellingTypes.ModObjectType), group.GroupType)
                ? (ModellingTypes.ModObjectType)group.GroupType
                : ModellingTypes.ModObjectType.AppRole;
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

        private static IEnumerable<WfReqElement> BuildNetworkGroupElements(IEnumerable<ModellingAppRole> groups, ElemFieldType field)
        {
            return groups
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
                IpEnd = appServer.IpEnd,
                Cidr = new Cidr(appServer.Ip ?? ""),
                CidrEnd = new Cidr(appServer.IpEnd ?? appServer.Ip ?? "")
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

        private static IEnumerable<WfReqElement> BuildServiceGroupElements(IEnumerable<ModellingServiceGroup> serviceGroups)
        {
            return serviceGroups
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

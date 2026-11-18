using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;

namespace FWO.Test
{
    /// <summary>
    /// Production state for the variance views (report, connection status, middleware job, request).
    /// Groups carry the flat entries the importer writes: the group itself first, then its members.
    /// <list type="bullet">
    /// <item>Connection 9: two rules with its marker, one fitting and one with a different app role set.</item>
    /// <item>Connection 11: one rule using group 003, which has a host the model does not have.</item>
    /// <item>Connection 12: one rule using group 004 and a service group, both with exactly the modelled members.</item>
    /// </list>
    /// </summary>
    internal sealed class SimulatedVarianceApiConnection : JobTestApiConnectionBase
    {
        public const int kMgtId = 1;
        public const string kFittingRuleUid = "uid-fitting-rule";
        public const string kDifferingRuleUid = "uid-second-rule";
        public const string kSurplusMemberRuleUid = "uid-resolve-rule";
        public const string kEqualMembersRuleUid = "uid-equal-rule";
        public const string kServiceGroupName = "SvcGrp1";

        private const int kGroupTypeId = 2;
        private const int kServiceGroupId = 20;
        private const string kConnectionReportQueryName = "getConnectionsResolved";

        /// <summary>
        /// Owners returned to the middleware job.
        /// </summary>
        public List<FwoOwner> Owners { get; set; } = [];

        /// <summary>
        /// Connections returned by the connection report query, i.e. the connections of the owner.
        /// </summary>
        public List<ModellingConnection> OwnerConnections { get; set; } = [];

        /// <summary>
        /// Id of the dummy app role returned by the dummy app role query.
        /// </summary>
        public long DummyAppRoleId { get; set; }

        /// <summary>
        /// Connection properties written back to the database, by connection id.
        /// </summary>
        public Dictionary<int, string> WrittenConnectionProperties { get; } = [];

        protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables, string? operationName, QueryChunkingOptions? chunkingOptions)
        {
            if (query == ModellingQueries.updateConnectionProperties)
            {
                WrittenConnectionProperties[GetVariable<int>(variables, "id")] = GetVariable<string>(variables, "connProp") ?? "";
                return ReturnEmptyOrDefault<QueryResponseType>();
            }
            object? data = typeof(QueryResponseType) switch
            {
                Type t when t == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners => Owners,
                Type t when t == typeof(List<ModellingConnection>) && query.Contains(kConnectionReportQueryName) => OwnerConnections,
                Type t when t == typeof(List<ModellingAppRole>) && query == ModellingQueries.getDummyAppRole => DummyAppRoles(),
                Type t when t == typeof(List<ManagementReport>) && query == ReportQueries.getRelevantImportIdsAtTime => RelevantImports(),
                Type t when t == typeof(List<Management>) => Managements(),
                Type t when t == typeof(List<NetworkObject>) => ObjectsFor(variables),
                Type t when t == typeof(List<Rule>) => RulesFor(variables),
                _ => null
            };
            return data == null ? ReturnEmptyOrDefault<QueryResponseType>() : Task.FromResult((QueryResponseType)data);
        }

        private List<ModellingAppRole> DummyAppRoles()
        {
            return [new() { Id = DummyAppRoleId }];
        }

        private static List<ManagementReport> RelevantImports()
        {
            return [new() { Id = kMgtId, Import = new() { ImportAggregate = new() { ImportAggregateMax = new() { RelevantImportId = 1 } } } }];
        }

        private static List<Management> Managements()
        {
            return [new() { Id = kMgtId, Name = "Mgt1", ExtMgtData = "{\"id\":\"1\",\"name\":\"Ext\"}", Devices = [new() { Id = 1 }] }];
        }

        private static NetworkObject Host(long id, string name, string ip) => new() { Id = id, Name = name, IP = ip, IpEnd = ip, Type = new() { Name = ObjectType.Host } };
        private static NetworkObject HostA() => Host(10, "HostA", "10.0.0.1/32");
        private static NetworkObject HostB() => Host(11, "HostB", "10.0.0.2/32");

        private static NetworkObject Group(long id, string name, params NetworkObject[] members)
        {
            NetworkObject ownFlatEntry = new() { Id = id, Name = name, Type = new() { Name = ObjectType.Group } };
            List<NetworkObject> flatEntries = [ownFlatEntry, .. members];
            return new()
            {
                Id = id,
                Name = name,
                Type = new() { Name = ObjectType.Group },
                ObjectGroupFlats = [.. flatEntries.Select(entry => new GroupFlat<NetworkObject>() { Object = entry })]
            };
        }

        private static NetworkObject Group1() => Group(1, "AR504711-001", HostA(), HostB());
        private static NetworkObject Group2() => Group(2, "AR504711-002", HostA());
        private static NetworkObject Group3() => Group(3, "AR504711-003", HostA(), HostB());
        private static NetworkObject Group4() => Group(4, "AR504711-004", HostA());
        private static NetworkObject Group9() => Group(9, "AR504711-009", HostA());

        private static NetworkService Dns() => new() { Id = 1, Name = "dns", DestinationPort = 53, DestinationPortEnd = 53, ProtoId = 17 };

        private static NetworkService DnsGroup()
        {
            NetworkService ownFlatEntry = new() { Id = kServiceGroupId, Name = kServiceGroupName, Type = new() { Name = ServiceType.Group } };
            List<NetworkService> flatEntries = [ownFlatEntry, Dns()];
            return new()
            {
                Id = kServiceGroupId,
                Name = kServiceGroupName,
                Type = new() { Name = ServiceType.Group },
                ServiceGroupFlats = [.. flatEntries.Select(entry => new GroupFlat<NetworkService>() { Object = entry })]
            };
        }

        private static Rule CreateRule(string uid, string name, List<NetworkObject> froms, List<NetworkObject> tos, NetworkService service) => new()
        {
            Uid = uid,
            Name = name,
            MgmtId = kMgtId,
            Froms = [.. froms.Select(obj => new NetworkLocation(new(), obj))],
            Tos = [.. tos.Select(obj => new NetworkLocation(new(), obj))],
            Services = [new() { Content = service }],
            EnforcingGateways = [new() { Content = new() { Id = 1, Name = "Gateway1" } }]
        };

        private static List<Rule> CreateRules()
        {
            // fresh objects per query: the analysis marks surplus objects on them
            List<NetworkObject> fittingFroms = [Group1()];
            List<NetworkObject> differingFroms = [Group1(), Group9()];
            List<NetworkObject> surplusMemberFroms = [Group3()];
            List<NetworkObject> equalMembersFroms = [Group4()];
            List<NetworkObject> fittingTos = [Group2()];
            List<NetworkObject> differingTos = [Group2()];
            List<NetworkObject> surplusMemberTos = [Group2()];
            List<NetworkObject> equalMembersTos = [Group2()];
            return
            [
                CreateRule(kFittingRuleUid, "FWOC9", fittingFroms, fittingTos, Dns()),
                CreateRule(kDifferingRuleUid, "FWOC9", differingFroms, differingTos, Dns()),
                CreateRule(kSurplusMemberRuleUid, "FWOC11", surplusMemberFroms, surplusMemberTos, Dns()),
                CreateRule(kEqualMembersRuleUid, "FWOC12", equalMembersFroms, equalMembersTos, DnsGroup())
            ];
        }

        private static List<NetworkObject> ObjectsFor(object? variables)
        {
            int[]? objTypeIds = GetVariable<int[]>(variables, "objTypeIds");
            if (objTypeIds == null || objTypeIds.Length == 0)
            {
                return [];
            }
            return objTypeIds[0] == kGroupTypeId ? [Group1(), Group2(), Group3(), Group4(), Group9()] : [HostA(), HostB()];
        }

        private static List<Rule> RulesFor(object? variables)
        {
            // the rule query is paged; all rules fit on the first page
            return GetVariable<int>(variables, "offset") == 0 ? CreateRules() : [];
        }

        private static T? GetVariable<T>(object? variables, string name)
        {
            object? value;
            if (variables is IDictionary<string, object?> dict)
            {
                dict.TryGetValue(name, out value);
            }
            else
            {
                value = variables?.GetType().GetProperty(name)?.GetValue(variables);
            }
            return value is T typedValue ? typedValue : default;
        }
    }

    /// <summary>
    /// Modelled connections matching the production state of <see cref="SimulatedVarianceApiConnection"/>.
    /// Each call creates a fresh connection, as the analysis writes state flags into it.
    /// </summary>
    internal static class SimulatedVarianceConnections
    {
        public const long kDummyAppRoleId = 99;
        public const int kDeletedAppServerConnId = 9;
        public const int kSurplusGroupMemberConnId = 11;
        public const int kEqualGroupMembersConnId = 12;
        public const int kDummyAppRoleConnId = 13;

        public static readonly FwoOwner Owner = new() { Id = 1, Name = "App1" };

        private static ModellingAppServer AppServerA() => new() { Id = 1, Name = "HostA", Ip = "10.0.0.1/32", IpEnd = "10.0.0.1/32" };
        private static ModellingAppServer DeletedAppServerB() => new() { Id = 2, Name = "HostB", Ip = "10.0.0.2/32", IpEnd = "10.0.0.2/32", IsDeleted = true };

        private static ModellingAppRole AppRole(long id, params ModellingAppServer[] appServers) => new()
        {
            Id = id,
            AppId = Owner.Id,
            Name = $"AppRole{id}",
            IdString = $"AR504711-00{id}",
            AppServers = [.. appServers.Select(appServer => new ModellingAppServerWrapper() { Content = appServer })]
        };

        private static ModellingService Dns() => new() { Id = 1, Name = "dns", Port = 53, ProtoId = 17 };

        private static ModellingConnection Connection(int id, ModellingAppRole source, ModellingAppRole destination) => new()
        {
            Id = id,
            AppId = Owner.Id,
            Name = $"Conn{id}",
            SourceAppRoles = [new() { Content = source }],
            DestinationAppRoles = [new() { Content = destination }],
            Services = [new() { Content = Dns() }]
        };

        /// <summary>
        /// The case from issue #5715: an app role with a deleted app server, and two production rules
        /// carrying the marker of the connection, one of them differing.
        /// </summary>
        public static ModellingConnection WithDeletedAppServer()
        {
            return Connection(kDeletedAppServerConnId, AppRole(1, AppServerA(), DeletedAppServerB()), AppRole(2, AppServerA()));
        }

        /// <summary>
        /// Connection whose source app role misses a host the production group has (HostB).
        /// </summary>
        public static ModellingConnection WithSurplusGroupMember()
        {
            return Connection(kSurplusGroupMemberConnId, AppRole(3, AppServerA()), AppRole(2, AppServerA()));
        }

        /// <summary>
        /// Connection whose app roles and service group have exactly the members of the production groups.
        /// </summary>
        public static ModellingConnection WithEqualGroupMembers()
        {
            ModellingServiceGroup serviceGroup = new()
            {
                Id = 5,
                AppId = Owner.Id,
                Name = SimulatedVarianceApiConnection.kServiceGroupName,
                Services = [new() { Content = Dns() }]
            };
            ModellingConnection connection = Connection(kEqualGroupMembersConnId, AppRole(4, AppServerA()), AppRole(2, AppServerA()));
            connection.Services = [];
            connection.ServiceGroups = [new() { Content = serviceGroup }];
            return connection;
        }

        /// <summary>
        /// Connection using the dummy app role, which has no app servers but never counts as empty.
        /// There is no production rule for it.
        /// </summary>
        public static ModellingConnection WithEmptyDummyAppRole()
        {
            return Connection(kDummyAppRoleConnId, AppRole(kDummyAppRoleId), AppRole(2, AppServerA()));
        }
    }
}

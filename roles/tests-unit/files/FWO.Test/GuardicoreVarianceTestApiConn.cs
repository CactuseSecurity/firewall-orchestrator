using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;

namespace FWO.Test
{
    /// <summary>
    /// One Guardicore management whose import shows the labels as groups named "key: value" with one member per CIDR
    /// block; device specific settings come from an in-memory hierarchy.
    /// </summary>
    internal sealed class GuardicoreVarianceTestApiConn : SimulatedApiConnection
    {
        public const int kManagementId = 1;

        private static readonly NetworkObject Host1 = Member(10, "1.2.3.4/32", ObjectType.Host);
        private static readonly NetworkObject Block1 = Member(11, "10.0.0.0/28", ObjectType.Network, "10.0.0.15/32");
        private static readonly NetworkObject Block2 = Member(12, "10.0.0.16/31", ObjectType.Network, "10.0.0.17/32");
        private static readonly NetworkObject Host2 = Member(13, "2.2.2.2/32", ObjectType.Host);
        private static readonly NetworkObject Surplus = Member(14, "9.9.9.9/32", ObjectType.Host);
        private static readonly NetworkObject AreaBlock = Member(15, "10.50.0.0/24", ObjectType.Network, "10.50.0.255/32");

        private static readonly List<NetworkObject> Labels =
        [
            Label(1, "AppRole: AR504711-001", Host1, Block1, Block2),
            Label(2, "AppRole: AR504711-003", Host1, Surplus),
            Label(3, "AppRole: AR504711-009", Host2),
            Label(4, "AppZone: AZ4711", Host1, Block1, Block2, Host2),
            Label(5, "NetworkArea: NA50", AreaBlock),
            Label(6, "Stage: Prod", Host1),
            Label(7, "AppZone: AZ0815", Host2),
            Label(8, "NetworkArea: NA60", AreaBlock)
        ];
        private static readonly List<NetworkObject> Hosts = [Host1, Block1, Block2, Host2, Surplus, AreaBlock];

        public static readonly ModellingAppServer AppServer1 = new() { Id = 1, Name = "AppServer1", Ip = "1.2.3.4", IpEnd = "1.2.3.4" };
        public static readonly ModellingAppServer AppServerRange = new() { Id = 2, Name = "AppServerRange", Ip = "10.0.0.0", IpEnd = "10.0.0.17" };
        public static readonly ModellingAppServer AppServer3 = new() { Id = 3, Name = "AppServer3", Ip = "2.2.2.2", IpEnd = "2.2.2.2" };

        private static readonly ModellingAppZone AppZone = new()
        {
            Id = 30,
            Name = "AZ4711",
            IdString = "AZ4711",
            AppServers = [new() { Content = AppServer1 }, new() { Content = AppServerRange }, new() { Content = AppServer3 }]
        };

        public InMemoryDeviceSpecificSettingsApiConnection Settings { get; } = new();

        /// <summary>Imported rules of the management; none unless a test sets them.</summary>
        public List<Rule> Rules { get; set; } = [];

        /// <summary>Imported labels (groups "key: value") replacing the standard ones, if set.</summary>
        public List<NetworkObject>? ImportedLabels { get; set; }

        /// <summary>Device type of the management; Guardicore unless a test needs another one.</summary>
        public int ManagementDeviceTypeId { get; set; } = FirewallVendors.kGuardicoreManagement;

        /// <summary>External management data of the management; none, as a Guardicore management needs none.</summary>
        public string? ManagementExtMgtData { get; set; }

        /// <summary>App roles that exist (not deleted), of all owners.</summary>
        public List<string> CurrentAppRoles { get; } = ["AR504711-001", "AR504711-002", "AR504711-003"];

        /// <summary>App zones that exist (not deleted), of all owners.</summary>
        public List<string> CurrentAppZones { get; } = ["AZ4711"];

        /// <summary>Network areas that exist (not deleted).</summary>
        public List<string> CurrentNetworkAreas { get; } = ["NA50"];

        /// <summary>
        /// Group names requested for the owner by earlier group_create tasks, with the object type the tasks name (GrpType);
        /// a group requested without a type is not a label FWO deletes.
        /// </summary>
        public List<(string Name, string? Type)> RequestedGroups { get; } =
        [
            ("AR504711-001", "AppRole"), ("AR504711-009", "AppRole"), ("AZ4711", "AppZone"), ("AZ0815", "AppZone"),
            ("NA50", "NetworkArea"), ("NA60", "NetworkArea"), ("Servers", null)
        ];

        public GuardicoreVarianceTestApiConn()
        {
            Settings.AddNode(1, DeviceSpecificScopeType.Global, "global");
            Settings.AddNode(2, DeviceSpecificScopeType.DeviceType, FirewallVendors.kGuardicoreManagement.ToString(), 1);
            Settings.AddNode(3, DeviceSpecificScopeType.Management, kManagementId.ToString(), 2);
        }

        private static NetworkObject Member(long id, string ip, string type, string? ipEnd = null) =>
            new() { Id = id, Name = ip, IP = ip, IpEnd = ipEnd ?? ip, Type = new() { Name = type } };

        private static NetworkObject Label(long id, string name, params NetworkObject[] members) =>
            new() { Id = id, Name = name, Type = new() { Name = ObjectType.Group }, ObjectGroupFlats = [.. members.Select(member => new GroupFlat<NetworkObject> { Object = member })] };

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == DeviceSpecificSettingsQueries.getNodesByNaturalKeys)
            {
                return await Settings.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }
            object result = typeof(QueryResponseType) switch
            {
                Type type when type == typeof(List<ManagementReport>) => RelevantImport(),
                Type type when type == typeof(List<Management>) => Managements(),
                Type type when type == typeof(List<NetworkObject>) => ObjectsOfTypes(variables),
                Type type when type == typeof(List<ModellingAppZone>) => new List<ModellingAppZone> { new(AppZone) },
                Type type when type == typeof(List<ModellingAppServer>) => new List<ModellingAppServer> { AppServer1, AppServerRange, AppServer3 },
                Type type when type == typeof(List<ModellingAppRole>) => CurrentGroups(variables).Select(idString => new ModellingAppRole { IdString = idString }).ToList(),
                Type type when type == typeof(List<WfReqTask>) && query == RequestQueries.getRequestedGroupsOfOwner =>
                    RequestedGroups.Select(RequestedGroupTask).ToList(),
                Type type when type == typeof(List<Rule>) => Rules.ConvertAll(rule => new Rule(rule)),
                Type type when type == typeof(List<ModellingConnection>) => new List<ModellingConnection>(),
                Type type when type == typeof(List<ModellingNetworkArea>) => new List<ModellingNetworkArea>(),
                Type type when type == typeof(List<DeviceReport>) => new List<DeviceReport>(),
                _ => throw new NotImplementedException($"{typeof(QueryResponseType)}: {query}")
            };
            return (QueryResponseType)result;
        }

        private List<string> CurrentGroups(object? variables)
        {
            object? groupType = variables?.GetType().GetProperty("grpType")?.GetValue(variables);
            return groupType switch
            {
                (int)ModellingTypes.ModObjectType.AppZone => CurrentAppZones,
                (int)ModellingTypes.ModObjectType.NetworkArea => CurrentNetworkAreas,
                _ => CurrentAppRoles
            };
        }

        private static List<ManagementReport> RelevantImport() =>
        [
            new() { Import = new() { ImportAggregate = new() { ImportAggregateMax = new() { RelevantImportId = 1 } } } }
        ];

        private List<Management> Managements() =>
        [
            new()
            {
                Id = kManagementId, Name = "Guardicore", ExtMgtData = ManagementExtMgtData,
                DeviceType = new() { Id = ManagementDeviceTypeId, Name = "Guardicore Management" }, Devices = [new() { Id = 1 }]
            }
        ];

        private static WfReqTask RequestedGroupTask((string Name, string? Type) group)
        {
            Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.GrpName, group.Name } };
            if (group.Type != null)
            {
                addInfo[AdditionalInfoKeys.GrpType] = group.Type;
            }
            return new WfReqTask { AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo) };
        }

        private List<NetworkObject> ObjectsOfTypes(object? variables)
        {
            int[]? objectTypeIds = variables?.GetType().GetProperty("objTypeIds")?.GetValue(variables) as int[];
            return objectTypeIds?.FirstOrDefault() == 2 ? ImportedLabels ?? Labels : Hosts;
        }
    }
}

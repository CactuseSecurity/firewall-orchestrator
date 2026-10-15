using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;

namespace FWO.Test
{
    internal class ModellingHandlerTestApiConn : SimulatedApiConnection
    {
        private static ReturnIdWrapper CreateNewAppRoleWrapper()
        {
            return new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { NewIdLong = 123 }]
            };
        }

        const string AppRoleId1 = "AR5000001";
        const string AppRoleId2 = "AR9101234-002";
        const string AppRoleId3 = "AR9901234-999";
        readonly ModellingAppRole AppRole1 = new() { Id = 1, IdString = AppRoleId1 };
        readonly ModellingAppRole AppRole2 = new() { Id = 2, IdString = AppRoleId2 };
        readonly ModellingAppRole AppRole3 = new() { Id = 3, IdString = AppRoleId3 };

        public List<ModellingAppRole> DummyAppRoles { get; set; } = [new() { Id = 999, Name = "dummy" }];
        public int NewAppRoleCalls { get; private set; }
        public int UpdateAppRoleCalls { get; private set; }
        public int AddNwObjectCalls { get; private set; }
        public int RemoveNwObjectCalls { get; private set; }
        public bool FailUpdateAppRole { get; set; }
        public bool FailNewAppRole { get; set; }

        /// <summary>
        /// Number of the call adding an app server to an app role which fails (counting from 1), 0 if none fails.
        /// </summary>
        public int FailingAddNwObjectCall { get; set; }

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<ModellingAppRole>))
            {
                return Respond<QueryResponseType>(GetAppRoles(query, variables));
            }
            if (responseType == typeof(ReturnIdWrapper) && query == ModellingQueries.newAppRole)
            {
                NewAppRoleCalls++;
                if (FailNewAppRole)
                {
                    throw new InvalidOperationException("simulated insert failure");
                }
                return Respond<QueryResponseType>(CreateNewAppRoleWrapper());
            }
            if (responseType == typeof(ReturnIdWrapper) && query == ModellingQueries.addHistoryEntry)
            {
                return Respond<QueryResponseType>(new ReturnIdWrapper());
            }
            if (responseType == typeof(ReturnId))
            {
                return Respond<QueryResponseType>(ChangeAppRole(query));
            }
            if (responseType == typeof(List<ModellingConnection>))
            {
                return Respond<QueryResponseType>(GetInterfaces(GetVariable(variables, "id")));
            }
            throw new NotImplementedException();
        }

        private static QueryResponseType Respond<QueryResponseType>(object data)
        {
            GraphQLResponse<dynamic> response = new() { Data = data };
            return response.Data;
        }

        private static string GetVariable(object? variables, string name)
        {
            return variables?.GetType().GetProperties().First(o => o.Name == name).GetValue(variables, null)?.ToString() ?? "";
        }

        private List<ModellingAppRole> GetAppRoles(string query, object? variables)
        {
            if (query == ModellingQueries.getNewestAppRoles)
            {
                return GetNewestAppRoles(GetVariable(variables, "pattern"));
            }
            if (query == ModellingQueries.getDummyAppRole)
            {
                return DummyAppRoles;
            }
            return [AppRole1];
        }

        private List<ModellingAppRole> GetNewestAppRoles(string pattern)
        {
            if (pattern == AppRoleId1 || pattern == "AR50%")
            {
                return [AppRole1];
            }
            if (pattern == AppRoleId2 || pattern == "AR9101234%")
            {
                return [AppRole2];
            }
            if (pattern == AppRoleId3 || pattern == "AR9901234%")
            {
                return [AppRole3];
            }
            return [];
        }

        private ReturnId ChangeAppRole(string query)
        {
            if (query == ModellingQueries.updateAppRole)
            {
                UpdateAppRoleCalls++;
                if (FailUpdateAppRole)
                {
                    throw new InvalidOperationException("simulated update failure");
                }
            }
            else if (query == ModellingQueries.addNwObjectToNwGroup)
            {
                AddNwObjectCalls++;
                if (AddNwObjectCalls == FailingAddNwObjectCall)
                {
                    throw new InvalidOperationException("simulated assignment failure");
                }
            }
            else if (query == ModellingQueries.removeNwObjectFromNwGroup)
            {
                RemoveNwObjectCalls++;
            }
            else
            {
                throw new NotImplementedException();
            }
            return new ReturnId();
        }

        private static List<ModellingConnection> GetInterfaces(string intId)
        {
            ModellingConnection? interf = intId switch
            {
                "1" => new()
                {
                    Name = "Interf1",
                    InterfacePermission = InterfacePermissions.Public.ToString(),
                    SourceAppRoles = [new() { Content = new() { Name = "AppRole1" } }],
                    SourceOtherGroups = [new() { Content = new() { Name = "NwGroup1" } }],
                    ServiceGroups = [new() { Content = new() { Name = "ServiceGrp1" } }]
                },
                "2" => new()
                {
                    Name = "Interf2",
                    InterfacePermission = InterfacePermissions.Public.ToString(),
                    DestinationAppServers = [new() { Content = new() { Name = "AppServer2" } }],
                    DestinationAppRoles = [new() { Content = new() { Name = "AppRole2" } }],
                    Services = [new() { Content = new() { Name = "Service2" } }]
                },
                "3" => CreateInterface("Interf3", InterfacePermissions.Restricted.ToString(), null, 1),
                "4" => CreateInterface("Interf4", InterfacePermissions.Restricted.ToString(), null, 2),
                "5" => CreateInterface("Interf5", InterfacePermissions.Private.ToString(), 1, 1),
                "6" => CreateInterface("Interf6", InterfacePermissions.Public.ToString(), null, null),
                "7" => CreateInterface("Interf7", InterfacePermissions.Private.ToString(), 2, 1),
                "8" => CreateInterface("Interf8", "Unknown", 2, null),
                _ => null
            };
            return interf != null ? [interf] : [];
        }

        private static ModellingConnection CreateInterface(string name, string permission, int? appId, int? permittedOwnerId)
        {
            return new()
            {
                Name = name,
                InterfacePermission = permission,
                AppId = appId,
                PermittedOwnerWrappers = permittedOwnerId != null ? [new() { Owner = new() { Id = permittedOwnerId.Value } }] : [],
                SourceAppRoles = [new() { Content = new() { Name = "AppRole1" } }]
            };
        }
    }
}

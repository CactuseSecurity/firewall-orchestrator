using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Data.Modelling;
using FWO.Services;

namespace FWO.Test
{
    internal class ModellingHandlerTestApiConn : SimulatedApiConnection
    {
        const string AppRoleId1 = "AR5000001";
        const string AppRoleId2 = "AR9101234-002";
        const string AppRoleId3 = "AR9901234-999";
        readonly ModellingAppRole AppRole1 = new() { Id = 1, IdString = AppRoleId1 };
        readonly ModellingAppRole AppRole2 = new() { Id = 2, IdString = AppRoleId2 };
        readonly ModellingAppRole AppRole3 = new() { Id = 3, IdString = AppRoleId3 };


        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<ModellingAppRole>))
            {
                List<ModellingAppRole>? appRoles = [];
                if (query == ModellingQueries.getNewestAppRoles)
                {
                    if (variables != null)
                    {
                        string pattern = variables?.GetType().GetProperties().First(o => o.Name == "pattern").GetValue(variables, null)?.ToString() ?? "";
                        if (pattern == AppRoleId1 || pattern == "AR50%")
                        {
                            appRoles = [AppRole1];
                        }
                        else if (pattern == AppRoleId2 || pattern == "AR9101234%")
                        {
                            appRoles = [AppRole2];
                        }
                        else if (pattern == AppRoleId3 || pattern == "AR9901234%")
                        {
                            appRoles = [AppRole3];
                        }
                    }
                }
                else
                {
                    appRoles = [AppRole1];
                }

                GraphQLResponse<dynamic> response = new() { Data = appRoles };
                return response.Data;
            }
            else if (responseType == typeof(List<ModellingConnection>))
            {
                List<ModellingConnection>? interfaces = [];
                string intId = variables?.GetType().GetProperties().First(o => o.Name == "id").GetValue(variables, null)?.ToString() ?? "";
                if (intId == "1")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf1",
                        InterfacePermission = InterfacePermissions.Public.ToString(),
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }],
                        SourceOtherGroups = [new(){ Content = new(){ Name = "NwGroup1" } }],
                        ServiceGroups = [new(){ Content = new(){ Name = "ServiceGrp1" } }]
                    }];
                }
                else if (intId == "2")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf2",
                        InterfacePermission = InterfacePermissions.Public.ToString(),
                        DestinationAppServers = [new(){ Content = new(){ Name = "AppServer2" } }],
                        DestinationAppRoles = [new(){ Content = new(){ Name = "AppRole2" } }],
                        Services = [new(){ Content = new(){ Name = "Service2" } }]
                    }];
                }
                else if (intId == "3")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf3",
                        InterfacePermission = InterfacePermissions.Restricted.ToString(),
                        PermittedOwnerWrappers = [new() { Owner = new() { Id = 1 } }],
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }]
                    }];
                }
                else if (intId == "4")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf4",
                        InterfacePermission = InterfacePermissions.Restricted.ToString(),
                        PermittedOwnerWrappers = [new() { Owner = new() { Id = 2 } }],
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }]
                    }];
                }
                else if (intId == "5")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf5",
                        InterfacePermission = InterfacePermissions.Private.ToString(),
                        AppId = 1,
                        PermittedOwnerWrappers = [new() { Owner = new() { Id = 1 } }],
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }]
                    }];
                }
                else if (intId == "6")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf6",
                        InterfacePermission = InterfacePermissions.Public.ToString(),
                        PermittedOwnerWrappers = [],
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }]
                    }];
                }
                else if (intId == "7")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf7",
                        InterfacePermission = InterfacePermissions.Private.ToString(),
                        AppId = 2,
                        PermittedOwnerWrappers = [new() { Owner = new() { Id = 1 } }],
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }]
                    }];
                }
                else if (intId == "8")
                {
                    interfaces = [ new()
                    {
                        Name = "Interf8",
                        InterfacePermission = "Unknown",
                        AppId = 2,
                        SourceAppRoles = [new(){ Content = new(){ Name = "AppRole1" } }]
                    }];
                }

                GraphQLResponse<dynamic> response = new() { Data = interfaces };
                return response.Data;
            }
            throw new NotImplementedException();
        }
    }
}

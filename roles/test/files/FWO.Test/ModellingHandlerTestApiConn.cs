using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Api.Data;

namespace FWO.Test
{
internal class ModellingHandlerTestApiConn : SimulatedApiConnection
    {
        const string AppRoleId1 = "AR5000001";
        ModellingAppRole AppRole1 = new(){ Id = 1, IdString = AppRoleId1 };


        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<ModellingAppRole>))
            {
                List<ModellingAppRole>? appRoles = new();
                if(query == ModellingQueries.getNewestAppRoles)
                {
                    string pattern = variables.GetType().GetProperties().First(o => o.Name == "pattern").GetValue(variables, null).ToString();
                    if(variables != null && (pattern == AppRoleId1 || pattern == "AR50%"))
                    {
                        appRoles = new(){ AppRole1 };
                    }
                }
                else
                {
                    appRoles = new(){ AppRole1 };
                }
                
                GraphQLResponse<dynamic> response = new(){ Data = appRoles };
                return response.Data;
            }
            else if(responseType == typeof(List<ModellingConnection>))
            {
                List<ModellingConnection>? interfaces = new();
                string intId = variables.GetType().GetProperties().First(o => o.Name == "intId").GetValue(variables, null).ToString();
                if(intId == "1")
                {
                    interfaces = new(){ new()
                    {
                        Name = "Interf1",
                        SourceAppRoles = new(){ new(){ Content = new(){ Name = "AppRole1" } } },
                        SourceNwGroups = new(){ new(){ Content = new(){ Name = "NwGroup1" } } },
                        ServiceGroups = new() { new(){ Content = new(){ Name = "ServiceGrp1" } } }
                    }};
                }
                else if(intId == "2")
                {
                    interfaces = new(){ new()
                    {
                        Name = "Interf2",
                        DestinationAppServers = new(){ new(){ Content = new(){ Name = "AppServer2" } } },
                        DestinationAppRoles = new(){ new(){ Content = new(){ Name = "AppRole2" } } },
                        Services = new() { new(){ Content = new(){ Name = "Service2" } } }
                    }};
                }

                GraphQLResponse<dynamic> response = new(){ Data = interfaces };
                return response.Data;
            }
            throw new NotImplementedException();
        }
    }
}

using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Api.Data;
using FWO.GlobalConstants;

namespace FWO.Test
{
    internal class ProdAnalysisTestApiConn : SimulatedApiConnection
    {
        static readonly NetworkObject NwObj1 = new(){ Id = 10, Name = "AppServer1", IP = "1.2.3.4", Type = new(){ Name = ObjectType.Host } };
        static readonly NetworkObject NwObj2 = new(){ Id = 11, Name = "AppServer2", IP = "1.2.3.5", IpEnd = "1.2.3.10", Type = new(){ Name = ObjectType.IPRange } };
        static readonly NetworkObject NwObj3 = new(){ Id = 12, Name = "AppServer3", IP = "1.2.4.0/24", Type = new(){ Name = ObjectType.Network } };
        static readonly NetworkObject Nwgroup1 = new(){ Id = 1, Name = "AR504711-001", Type = new(){ Name = ObjectType.Group }, ObjectGroupFlats = [ new(){ Object = NwObj1}, new(){ Object = NwObj3} ] };


        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<Management>))
            {
                List<Management>? managements =
                [
                    new(){ Id = 1, Name = "Checkpoint1", ExtMgtData = "{\"id\":\"1\",\"name\":\"CheckpointExt\"}" }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = managements };
                return response.Data;
            }
            else if(responseType == typeof(List<NetworkObject>))
            {
                List<NetworkObject>? nwObjects = [];
                if(query == ObjectQueries.getNetworkObjectsForManagement)
                {
                    if(variables != null)
                    {
                        var objTypeIds = variables.GetType().GetProperties().First(o => o.Name == "objTypeIds").GetValue(variables, null);
                        if(((int[])objTypeIds)[0] == 2)
                        {
                            nwObjects = 
                            [
                                Nwgroup1
                            ];
                        }
                        else
                        {
                            nwObjects = 
                            [
                                NwObj1, NwObj2, NwObj3
                            ];
                        }
                    }
                }
                GraphQLResponse<dynamic> response = new(){ Data = nwObjects };
                return response.Data;
            }
            throw new NotImplementedException();
        }
    }
}

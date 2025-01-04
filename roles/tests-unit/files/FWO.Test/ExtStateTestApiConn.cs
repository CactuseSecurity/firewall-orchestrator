using GraphQL;
using FWO.Api.Data;

namespace FWO.Test
{
    internal class ExtStateTestApiConn : SimulatedApiConnection
    {
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<WfExtState>))
            {
                List<WfExtState>? extStates = 
                [
                    new(){ Id = 1, Name = "ExtReqInitialized", StateId = 1 },
                    new(){ Id = 2, Name = "ExtReqRequested", StateId = 2 }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = extStates };
                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}

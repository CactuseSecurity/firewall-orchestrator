﻿using GraphQL;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;

namespace FWO.Test
{
    internal class ExtStateTestApiConn : SimulatedApiConnection
    {
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
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

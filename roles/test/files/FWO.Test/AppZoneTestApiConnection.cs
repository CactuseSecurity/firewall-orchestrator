using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Api.Data;
using FWO.Api.Client;

namespace FWO.Test
{
    internal class AppZoneTestApiConnection : SimulatedApiConnection
    {
        const string NameCom = "COM1";
        const string NameAPP = "APP0";

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (variables is null)
                throw new NotImplementedException("No variables!");

            if (typeof(QueryResponseType) != typeof(NewReturning))
                throw new NotImplementedException("Wrong type!");


            string appName = variables.GetType().GetProperties().First(o => o.Name == "name").GetValue(variables, null)?.ToString();
            string appId = variables.GetType().GetProperties().First(_ => _.Name == "appId").GetValue(variables, null)?.ToString();

            if (string.IsNullOrEmpty(appName))
                throw new NotImplementedException("No name found!");

            if (string.IsNullOrEmpty(appId))
                throw new NotImplementedException("No app ID found!");

            if (!appName.ToLower().StartsWith(NameCom.ToLower()) &&
                !appName.ToLower().StartsWith(NameAPP.ToLower()))
            {
                throw new NotImplementedException("App name pattern wrong!");
            }

            NewReturning returnIds = new();
            returnIds.ReturnIds = new ReturnId[1];
            returnIds.ReturnIds[0] = new ReturnId();
            returnIds.ReturnIds[0].NewId = 1;

            GraphQLResponse<dynamic> response = new() { Data = returnIds };
            return response.Data;
        }
    }
}

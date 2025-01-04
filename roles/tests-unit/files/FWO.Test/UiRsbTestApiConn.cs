
using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Api.Data;
using FWO.Report;

namespace FWO.Test
{
    internal class UiRsbTestApiConn : SimulatedApiConnection
    {
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<ManagementReport>))
            {
                List<ManagementReport> reports = SimulatedReport.DetailedReport().ReportData.ManagementData;
                GraphQLResponse<dynamic> response = new(){ Data = reports };
                return response.Data;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
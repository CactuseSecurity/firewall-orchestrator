
using GraphQL;
using FWO.Data.Report;
using FWO.Services;

namespace FWO.Test
{
    internal class UiRsbTestApiConn : SimulatedApiConnection
    {
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
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
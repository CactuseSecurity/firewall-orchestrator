using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Config.Api;
using FWO.Logging;

namespace FWO.Report
{
    public class ReportConnections : ReportConnectionsBase
    {
        public ReportConnections(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override async Task GenerateCon(int connectionsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback, CancellationToken ct)
        {
            // Query.QueryVariables["limit"] = connectionsPerFetch;
            // Query.QueryVariables["offset"] = 0;
            // bool gotNewObjects = true;

            Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);

            // while (gotNewObjects)
            // {
            //     if (ct.IsCancellationRequested)
            //     {
            //         Log.WriteDebug("Generate Changes Report", "Task cancelled");
            //         ct.ThrowIfCancellationRequested();
            //     }
            //     Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + connectionsPerFetch;
            //     List<ModellingConnection> newConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);
            //     gotNewObjects = newConnections.Count > 0;
            //     Connections.AddRange(newConnections);
                await callback(Connections);
            // }
        }

        public override async Task<bool> GetConObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback)
        {
            await callback (Connections);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public override string ExportToJson()
        {
            return "";
        }

        public override string ExportToCsv()
        {
            throw new NotImplementedException();
        }

        public override string ExportToHtml()
        {
            return "";
        }
    }
}

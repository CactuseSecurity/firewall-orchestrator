using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Config.Api;
using FWO.Logging;

namespace FWO.Report
{
    public class ReportConnections : ReportOwnersBase
    {
        public ReportConnections(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override async Task GenerateCon(int connectionsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback, CancellationToken ct)
        {
            List<ModellingConnection> conns = new();
            // Query.QueryVariables["limit"] = connectionsPerFetch;
            // Query.QueryVariables["offset"] = 0;
            // bool gotNewObjects = true;

            conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);

            // while (gotNewObjects)
            // {
            //     if (ct.IsCancellationRequested)
            //     {
            //         Log.WriteDebug("Generate Connections Report", "Task cancelled");
            //         ct.ThrowIfCancellationRequested();
            //     }
            //     Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + connectionsPerFetch;
            //     List<ModellingConnection> newConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);
            //     gotNewObjects = newConnections.Count > 0;
            //     OwnersReport.Connections.AddRange(newConnections);

            await callback(conns);

            // }
            OwnersReport.Add(new(){ Connections = conns });
        }

        public override async Task<bool> GetConObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback)
        {
            await callback (OwnersReport[0].Connections);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public override string SetDescription()
        {
            int counter = 0;
            foreach(var owner in OwnersReport)
            {
                counter += owner.Connections.Count;
            }
            return $"{counter} {userConfig.GetText("connections")}";
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

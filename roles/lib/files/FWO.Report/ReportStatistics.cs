using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.Report.Filter;
using FWO.ApiClient.Queries;
namespace FWO.Report
{
    public class ReportStatistics : Report
    {
        public Management[] Managements { get; set; }

        public override async Task Generate(int _, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (query.ReportTime != null && query.ReportTime != "" && query.ReportTime != "now")
                TimeFilter = query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
            List<Management> resultList = new List<Management>();
            int i;

            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                else    // managment was not yet imported at that time
                    query.QueryVariables["relevantImportId"] = -1;
                resultList.Add((await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0]);
            }
            result = resultList.ToArray();
            await callback(result);
        }

        public override string ToCsv()
        {
            StringBuilder csvBuilder = new StringBuilder();

            foreach (Management management in Managements)
            {
                //foreach (var item in collection)
                //{

                //}
            }

            throw new NotImplementedException();
        }

        public override string ToHtml()
        {
            throw new NotImplementedException();
        }

        public override string ToPdf()
        {
            throw new NotImplementedException();
        }
    }
}

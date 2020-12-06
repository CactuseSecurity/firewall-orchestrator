using FWO.ApiClient;
using FWO.Api.Data;
using FWO.Report.Filter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FWO.ApiClient.Queries;

namespace FWO.Report
{
    public abstract class Report
    {
        Management[] result = null;

        public async void Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            // get the filter line
            DynGraphqlQuery query = Compiler.Compile(filterInput);
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (query.ReportTime != "")
                TimeFilter = query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

            result = new Management[managementsWithRelevantImportId.Length];
            query.QueryVariables["limit"] = rulesPerFetch;
            query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            for (int i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                result[i] = (await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0];
            }
            while (gotNewObjects)
            {
                query.QueryVariables["offset"] = (int)query.QueryVariables["offset"] + rulesPerFetch;
                for (int j = 0; j < managementsWithRelevantImportId.Length; j++)
                {
                    query.QueryVariables["mgmId"] = managementsWithRelevantImportId[j].Id;
                    gotNewObjects = result[j].Merge((await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0]);
                }
                await callback(result);
            }
        }
         
        public abstract string ToCsv();

        public abstract string ToHtml();

        public abstract string ToPdf();
    }
}

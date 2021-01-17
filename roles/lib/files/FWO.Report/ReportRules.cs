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
    public class ReportRules: ReportBase
    {
        public Management[] Managements { get; set; }

        public override async Task Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);
            query.QueryVariables["limit"] = rulesPerFetch;
            query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            // get the filter line
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (query.ReportTime != "")
                TimeFilter = query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

            result = new Management[managementsWithRelevantImportId.Length];
            int i;

            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                else    // managment was not yet imported at that time
                    query.QueryVariables["relevantImportId"] = -1;
                result[i] = (await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0];
            }
            while (gotNewObjects)
            {
                query.QueryVariables["offset"] = (int)query.QueryVariables["offset"] + rulesPerFetch;
                for (i = 0; i < managementsWithRelevantImportId.Length; i++)
                {
                    if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                        query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                    else
                        query.QueryVariables["relevantImportId"] = -1; // managment was not yet imported at that time
                    query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                    gotNewObjects = result[i].Merge((await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0]);
                }
                await callback(result);
            }
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

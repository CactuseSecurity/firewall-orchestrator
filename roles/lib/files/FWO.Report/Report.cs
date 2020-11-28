using FWO.ApiClient;
using FWO.Ui.Data.API;
using FWO.Report.Filter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Report
{
    public abstract class Report
    {
        Management[] result = null;

        public async void Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);

            query.QueryVariables["limit"] = rulesPerFetch;
            query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            result = await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables);
            await callback(result);

            while (gotNewObjects)
            {
                query.QueryVariables["offset"] = (int)query.QueryVariables["offset"] + rulesPerFetch;
                gotNewObjects = result.Merge(await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables));
                await callback(result);
            }
        }
         
        public abstract string ToCsv();

        public abstract string ToHtml();

        public abstract string ToPdf();
    }
}

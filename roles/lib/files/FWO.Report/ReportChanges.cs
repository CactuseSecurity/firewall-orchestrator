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
    public class ReportChanges : ReportBase
    {
        public override async Task Generate(int changesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);
            query.QueryVariables["limit"] = changesPerFetch;
            query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;
            Managements = Array.Empty<Management>();

            Managements = await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables);
            while (gotNewObjects)
            {
                query.QueryVariables["offset"] = (int)query.QueryVariables["offset"] + changesPerFetch;
                gotNewObjects = Managements.Merge(await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables));
                await callback(Managements);
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

        public override byte[] ToPdf()
        {
            throw new NotImplementedException();
        }
    }
}

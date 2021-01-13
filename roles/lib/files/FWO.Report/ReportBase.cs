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
    public abstract class ReportBase
    {
        protected Management[] result = null;

        public abstract Task Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback);
        
        public abstract string ToCsv();

        public abstract string ToHtml();

        public abstract string ToPdf();

        public static ReportBase ConstructReport(string filterInput)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);

            return query.ReportType switch
            {
                "statistics" => new ReportStatistics(),
                "rules" => new ReportRules(),
                "changes" => new ReportChanges(),
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }
    }
}

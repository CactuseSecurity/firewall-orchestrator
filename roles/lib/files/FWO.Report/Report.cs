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

        public abstract void Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback);
        
        public abstract string ToCsv();

        public abstract string ToHtml();

        public abstract string ToPdf();
    }
}

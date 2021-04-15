using FWO.ApiClient;
using FWO.Api.Data;
using FWO.Report.Filter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FWO.ApiClient.Queries;
using System.Text.Json;
using VetCV.HtmlRendererCore.PdfSharpCore;
using PdfSharpCore;
using PdfSharpCore.Pdf;

namespace FWO.Report
{
    public abstract class ReportBase
    {
        protected string HtmlTemplate = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8""/>
      <title> ##Title##</title>   
         <style>
             table {
                font-family: arial, sans-serif;
                font-size: 10px;
                border-collapse: collapse;
                width: 100 %;
              }

              td {
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
              }

              th {
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
                background-color: #dddddd;
              }
         </style>
    </head>
    <body>
        ##Body##
    </body>
</html>";

        public Management[] Managements = null;

        public abstract Task Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback);
        
        public abstract string ExportToCsv();

        public virtual string ExportToJson()
        {
            return JsonSerializer.Serialize(Managements, new JsonSerializerOptions { WriteIndented = true });
        }

        public abstract string ExportToHtml();

        public virtual byte[] ToPdf()
        {
            // HTML
            string html = ExportToHtml();

            // CONFIG
            PdfGenerateConfig config = new PdfGenerateConfig();
            config.PageOrientation = PageOrientation.Landscape;
            config.SetMargins(20);
            config.PageSize = PageSize.A4;

            PdfDocument document = PdfGenerator.GeneratePdf(html, config);

            using (MemoryStream stream = new MemoryStream())
            {
                document.Save(stream, false);
                return stream.ToArray();
            }           
        }

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

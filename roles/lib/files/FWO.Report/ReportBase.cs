using FWO.ApiClient;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text.Json;
using System.Text;
using WkHtmlToPdfDotNet;

namespace FWO.Report
{
    public abstract class ReportBase
    {
        protected StringBuilder HtmlTemplate = new StringBuilder($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8""/>
      <title>##Title##</title>
         <style>  
             table {{
                font-family: arial, sans-serif;
                font-size: 10px;
                border-collapse: collapse; 
                width: 100 %;
              }}

              td {{
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
              }}

              th {{
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
                background-color: #dddddd;
              }}
         </style>
    </head>
    <body>
        <h2>##Title##</h2>
        <p>Filter: ##Filter## - ##GeneratedOn##: ##Date##</p>
        <hr>
        ##Body##
    </body>
</html>");

        public Management[] Managements = new Management[]{};
        
        // public Management[] ReportObjects = null;
        

        public readonly DynGraphqlQuery Query;
        protected UserConfig userConfig;

        private string htmlExport = "";

        // Pdf converter
        protected static readonly SynchronizedConverter converter = new SynchronizedConverter(new PdfTools());

        public ReportBase(DynGraphqlQuery query, UserConfig UserConfig)
        {
            Query = query;
            userConfig = UserConfig;
        }

        public abstract Task Generate(int rulesPerFetch, APIConnection apiConnection, Func<Management[], Task> callback, CancellationToken ct);

        public bool GotObjectsInReport { get; protected set; } = false;

        public abstract Task GetObjectsInReport(int objectsPerFetch, APIConnection apiConnection, Func<Management[], Task> callback); // to be called when exporting

        public abstract Task GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, byte objects, int maxFetchCycles, APIConnection apiConnection, Func<Management[], Task> callback);

        public abstract string ExportToCsv();

        public virtual string ExportToJson()
        {
            return JsonSerializer.Serialize(Managements.Where(mgt => !mgt.Ignore), new JsonSerializerOptions { WriteIndented = true });
        }

        public abstract string ExportToHtml();

        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport)
        {
            if (string.IsNullOrEmpty(htmlExport))
            {
                HtmlTemplate = HtmlTemplate.Replace("##Body##", htmlReport.ToString());
                HtmlTemplate = HtmlTemplate.Replace("##Title##", title);
                HtmlTemplate = HtmlTemplate.Replace("##Filter##", filter);
                HtmlTemplate = HtmlTemplate.Replace("##Date##", date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedOn##", userConfig.GetText("generated_on"));
                htmlExport = HtmlTemplate.ToString();
            }
            return htmlExport;
        }

        public virtual byte[] ToPdf()
        {
            // HTML
            if (string.IsNullOrEmpty(htmlExport))
                htmlExport = ExportToHtml();

            GlobalSettings globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Landscape,
                PaperSize = PaperKind.A4
            };

            HtmlToPdfDocument doc = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects =
                {
                    new ObjectSettings()
                    {
                        PagesCount = true,
                        HtmlContent = htmlExport,
                        WebSettings = { DefaultEncoding = "utf-8" },
                        HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true, Spacing = 2.812 }
                    }
                }
            };

            return converter.Convert(doc);

            //// CONFIG
            //PdfGenerateConfig config = new PdfGenerateConfig();
            //config.PageOrientation = PageOrientation.Landscape;
            //config.SetMargins(20);
            //config.PageSize = PageSize.A4;

            //PdfDocument document = PdfGenerator.GeneratePdf(html, config);

            //using (MemoryStream stream = new MemoryStream())
            //{
            //    document.Save(stream, false);
            //    return stream.ToArray();
            //}
        }

        public static ReportBase ConstructReport(string filterInput, DeviceFilter deviceFilter, TimeFilter timeFilter, ReportType reportType, UserConfig userConfig)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput, reportType, deviceFilter, timeFilter);

            return reportType switch
            {
                ReportType.Statistics => new ReportStatistics(query, userConfig),
                ReportType.Rules => new ReportRules(query, userConfig),
                ReportType.Changes => new ReportChanges(query, userConfig),
                ReportType.NatRules => new ReportNatRules(query, userConfig),
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }
    }
}

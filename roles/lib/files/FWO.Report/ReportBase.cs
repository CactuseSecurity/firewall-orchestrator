using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text.Json;
using System.Text;
using WkHtmlToPdfDotNet;

namespace FWO.Report
{
    public enum RsbTab
    {
        all = 10, 
        report = 20, 
        rule = 30
    }

    public enum ObjCategory
    {
        all = 0,
        nobj = 1, 
        nsrv = 2, 
        user = 3
    }

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
        <p>Filter: ##Filter##</p>
        <p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>
        <p>##GeneratedOn##: ##Date## (UTC)</p>
        <p>Devices: ##DeviceFilter##</p>
        <hr>
        ##Body##
    </body>
</html>");

        public Management[] Managements = new Management[] { };

        public readonly DynGraphqlQuery Query;
        protected UserConfig userConfig;
        public ReportType ReportType;

        private string htmlExport = "";

        // Pdf converter
        protected static readonly SynchronizedConverter converter = new SynchronizedConverter(new PdfTools());

        public ReportBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType)
        {
            Query = query;
            userConfig = UserConfig;
            ReportType = reportType;
        }

        public abstract Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<Management[], Task> callback, CancellationToken ct);

        public bool GotObjectsInReport { get; protected set; } = false;

        public abstract Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<Management[], Task> callback); // to be called when exporting

        public abstract Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<Management[], Task> callback);

        public abstract string ExportToCsv();

        public virtual string ExportToJson()
        {
            return JsonSerializer.Serialize(Managements.Where(mgt => !mgt.Ignore), new JsonSerializerOptions { WriteIndented = true });
        }

        public abstract string ExportToHtml();

        public virtual string SetDescription()
        {
            int managementCounter = 0;
            foreach (var management in Managements.Where(mgt => !mgt.Ignore))
            {
                managementCounter++;
            }
            return $"{managementCounter} {userConfig.GetText("managements")}";
        }

        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport)
        {
            if (string.IsNullOrEmpty(htmlExport))
            {
                HtmlTemplate = HtmlTemplate.Replace("##Title##", title);
                HtmlTemplate = HtmlTemplate.Replace("##Filter##", filter);
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedOn##", userConfig.GetText("generated_on"));
                HtmlTemplate = HtmlTemplate.Replace("##Date##", date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                if(ReportType.IsChangeReport())
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>", "");
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##", userConfig.GetText("date_of_config"));
                    HtmlTemplate = HtmlTemplate.Replace("##GeneratedFor##", DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                }
                HtmlTemplate = HtmlTemplate.Replace("##DeviceFilter##", string.Join("; ", Array.ConvertAll(Managements.Where(mgt => !mgt.Ignore).ToArray(), management => management.NameAndDeviceNames())));
                HtmlTemplate = HtmlTemplate.Replace("##Body##", htmlReport.ToString());
                htmlExport = HtmlTemplate.ToString();
            }
            return htmlExport;
        }

        public string DisplayReportHeaderCsv()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            if(!ReportType.IsChangeReport())
            {
                report.AppendLine($"# date of configuration shown: {DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            }
            report.AppendLine($"# device filter: {string.Join(" ", Array.ConvertAll(Managements.Where(mgt => !mgt.Ignore).ToArray(), management => management.NameAndDeviceNames(" ")))}");
            report.AppendLine($"# other filters: {Query.RawFilter}");
            report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
            report.AppendLine($"# data protection level: For internal use only");
            report.AppendLine($"#");
            return $"{report.ToString()}";
        }

        public virtual byte[] ToPdf(PaperKind paperKind, int width = -1, int height = -1)
        {
            // HTML
            if (string.IsNullOrEmpty(htmlExport))
                htmlExport = ExportToHtml();

            GlobalSettings globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Landscape,
            };

            if (paperKind == PaperKind.Custom)
            {
                if (width > 0 && height > 0)
                {
                    globalSettings.PaperSize = new PechkinPaperSize(width + "mm", height + "mm");
                }
                else
                {
                    throw new Exception("Custom paper size: width or height <= 0");
                }
            }
            else
            {
                globalSettings.PaperSize = paperKind;
            }

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
        }

        public static ReportBase ConstructReport(ReportTemplate reportFilter, UserConfig userConfig)
        {
            DynGraphqlQuery query = Compiler.Compile(reportFilter);
            ReportType repType = (ReportType) (reportFilter.ReportParams.ReportType ?? throw new NotSupportedException("Report Type is not set."));
            return repType switch
            {
                ReportType.Statistics => new ReportStatistics(query, userConfig, repType),
                ReportType.Rules => new ReportRules(query, userConfig, repType),
                ReportType.ResolvedRules => new ReportRules(query, userConfig, repType),
                ReportType.ResolvedRulesTech => new ReportRules(query, userConfig, repType),
                ReportType.Changes => new ReportChanges(query, userConfig, repType),
                ReportType.ResolvedChanges => new ReportChanges(query, userConfig, repType),
                ReportType.ResolvedChangesTech => new ReportChanges(query, userConfig, repType),
                ReportType.NatRules => new ReportNatRules(query, userConfig, repType),
                ReportType.Recertification => new ReportRules(query, userConfig, repType),
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }

        public async Task<Management[]> getRelevantImportIds(ApiConnection apiConnection)
        {
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            ImpIdQueryVariables["time"] = (Query.ReportTimeString != "" ? Query.ReportTimeString : DateTime.Now.ToString(DynGraphqlQuery.fullTimeFormat));
            ImpIdQueryVariables["mgmIds"] = Query.RelevantManagementIds;
            return await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
        }

        public static string GetIconClass(ObjCategory objCategory, string objType)
        {
            switch (objType)
            {
                case "group" when objCategory == ObjCategory.user:
                    return "oi oi-people";
                case "group":
                    return "oi oi-list-rich";
                case "host":
                    return "oi oi-laptop";
                case "network":
                    return "oi oi-rss";
                case "ip_range":
                    return "oi oi-resize-width";
                default:
                    switch (objCategory)
                    {
                        case ObjCategory.nobj:
                            return "oi oi-laptop";
                        case ObjCategory.nsrv:
                            return "oi oi-wrench";
                        case ObjCategory.user:
                            return "oi oi-person";
                    }
                    return "";
            }
        }
    }
}

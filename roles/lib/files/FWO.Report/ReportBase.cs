using FWO.Basics;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text;
using WkHtmlToPdfDotNet;

namespace FWO.Report
{
    public enum RsbTab
    {
        all = 10, 
        report = 20, 
        rule = 30,

        usedObj = 40,
        unusedObj = 50
    }

    public enum ObjCategory
    {
        all = 0,
        nobj = 1, 
        nsrv = 2, 
        user = 3
    }

    public struct ObjCatString
    {
        public const string NwObj = "nwobj";
        public const string Svc = "svc";
        public const string User = "user";
    }

    public enum OutputLocation
    {
        export,
        report,
        certification
    }

    public abstract class ReportBase
    {
        protected StringBuilder HtmlTemplate = new ($@"
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
        <p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>
        <p>##GeneratedOn##: ##Date## (UTC)</p>
        <p>##OwnerFilters##</p>
        <p>##OtherFilters##</p>
        <p>##Filter##</p>
        <hr>
        ##Body##
    </body>
</html>");

        public readonly DynGraphqlQuery Query;
        protected UserConfig userConfig;
        public ReportType ReportType;
        public ReportData ReportData = new();

        protected string htmlExport = "";

        // Pdf converter
        protected static readonly SynchronizedConverter converter = new (new PdfTools());
        public bool GotObjectsInReport { get; protected set; } = false;


        public ReportBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType)
        {
            Query = query;
            userConfig = UserConfig;
            ReportType = reportType;
        }

        public abstract Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct);

        public abstract Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback); // to be called when exporting

        public virtual Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            throw new NotImplementedException();
        }

        public virtual bool NoRuleFound()
        {
            return true;
        }

        public abstract string ExportToCsv();

        public abstract string ExportToJson();

        public abstract string ExportToHtml();

        public abstract string SetDescription();

        public static ReportBase ConstructReport(ReportTemplate reportFilter, UserConfig userConfig)
        {
            DynGraphqlQuery query = Compiler.Compile(reportFilter);
            ReportType repType = (ReportType)reportFilter.ReportParams.ReportType;
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
                ReportType.UnusedRules => new ReportRules(query, userConfig, repType),
                ReportType.Connections => new ReportConnections(query, userConfig, repType),
                ReportType.AppRules => new ReportAppRules(query, userConfig, repType, reportFilter.ReportParams.ModellingFilter),
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }

        public static string ConstructLink(string type, string symbol, long id, string name, OutputLocation location, string reportId, string style)
        {
            string page = location == OutputLocation.report ? PageName.ReportGeneration : PageName.Certification;
            string link = location == OutputLocation.export ? $"#" : $"{page}#goto-report-{reportId}-";
            return $"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{link}{type}{id}\" target=\"_top\" style=\"{style}\">{name}</a>";
        }

        protected string GenerateHtmlFrameBase(string title, string filter, DateTime date, StringBuilder htmlReport, string? deviceFilter = null, string? ownerFilter = null)
        {
            if (string.IsNullOrEmpty(htmlExport))
            {
                HtmlTemplate = HtmlTemplate.Replace("##Title##", title);
                HtmlTemplate = HtmlTemplate.Replace("##Filter##", userConfig.GetText("filter") + ": " + filter);
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedOn##", userConfig.GetText("generated_on"));
                HtmlTemplate = HtmlTemplate.Replace("##Date##", date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                if(ReportType.IsChangeReport())
                {
                    string timeRange = $"{userConfig.GetText("change_time")}: " +
                        $"{userConfig.GetText("from")}: {ToUtcString(Query.QueryVariables["start"]?.ToString())}, " +
                        $"{userConfig.GetText("until")}: {ToUtcString(Query.QueryVariables["stop"]?.ToString())}";
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##: ##GeneratedFor##", timeRange);
                }
                else if(ReportType.IsRuleReport() || ReportType == ReportType.Statistics)
                {
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##", userConfig.GetText("date_of_config"));
                    HtmlTemplate = HtmlTemplate.Replace("##GeneratedFor##", ToUtcString(Query.ReportTimeString));
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>", "");
                }

                if (ownerFilter != null)
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OwnerFilters##", userConfig.GetText("owners") + ": " + ownerFilter);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##OwnerFilters##</p>", "");
                }

                if(deviceFilter != null)
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OtherFilters##", userConfig.GetText("devices") + ": " + deviceFilter);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##OtherFilters##</p>", "");
                }
                HtmlTemplate = HtmlTemplate.Replace("##Body##", htmlReport.ToString());
                htmlExport = HtmlTemplate.ToString();
            }
            return htmlExport;
        }

        public static string ToUtcString(string? timestring)
        {
            try
            {
                return timestring != null ? DateTime.Parse(timestring).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK") : "";
            }
            catch(Exception)
            {
                return timestring ?? "";
            }
        }

        public virtual byte[] ToPdf(PaperKind paperKind, int width = -1, int height = -1)
        {
            // HTML
            if (string.IsNullOrEmpty(htmlExport))
            {
                htmlExport = ExportToHtml();
            }

            GlobalSettings globalSettings = new ()
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

            HtmlToPdfDocument doc = new ()
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

        public static string GetIconClass(ObjCategory? objCategory, string? objType)
        {
            return objType switch
            {
                ObjectType.Group when objCategory == ObjCategory.user => Icons.UserGroup,
                ObjectType.Group => Icons.ObjGroup,
                ObjectType.Host => Icons.Host,
                ObjectType.Network => Icons.Network,
                ObjectType.IPRange => Icons.Range,
                _ => objCategory switch
                {
                    ObjCategory.nobj => Icons.NwObject,
                    ObjCategory.nsrv => Icons.Service,
                    ObjCategory.user => Icons.User,
                    _ => "",
                },
            };
        }
    }
}

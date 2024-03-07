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
        user = 3 //,
        // appSvc = 4
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

        public readonly DynGraphqlQuery Query;
        protected UserConfig userConfig;
        public ReportType ReportType;

        protected string htmlExport = "";

        // Pdf converter
        protected static readonly SynchronizedConverter converter = new SynchronizedConverter(new PdfTools());
        public bool GotObjectsInReport { get; protected set; } = false;


        public ReportBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType)
        {
            Query = query;
            userConfig = UserConfig;
            ReportType = reportType;
        }

        public virtual Task GenerateMgt(int rulesPerFetch, ApiConnection apiConnection, Func<List<ManagementReport>, Task> callback, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public virtual Task GenerateCon(int _, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> GetMgtObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<List<ManagementReport>, Task> callback) // to be called when exporting
        {
            throw new NotImplementedException();
        }
        public virtual Task<bool> GetConObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback) // to be called when exporting
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<List<ManagementReport>, Task> callback)
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
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }

        public string ToUtcString(string? timestring)
        {
            try
            {
                return timestring != null ? DateTime.Parse(timestring).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK") : "";
            }
            catch(Exception)
            {
                return timestring != null ? timestring : "";
            }
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

        public static string GetIconClass(ObjCategory? objCategory, string? objType)
        {
            switch (objType)
            {
                case ObjectType.Group when objCategory == ObjCategory.user:
                    return Icons.UserGroup;
                case ObjectType.Group:
                    return Icons.ObjGroup;
                case ObjectType.Host:
                    return Icons.Host;
                case ObjectType.Network:
                    return Icons.Network;
                case ObjectType.IPRange:
                    return Icons.Range;
                default:
                    switch (objCategory)
                    {
                        case ObjCategory.nobj:
                            return Icons.NwObject;
                        case ObjCategory.nsrv:
                            return Icons.Service;
                        case ObjCategory.user:
                            return Icons.User;
                    }
                    return "";
            }
        }
    }
}

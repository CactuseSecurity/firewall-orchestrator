﻿using FWO.Api.Client;
using FWO.Api.Client.Queries;
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

        public abstract Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, byte objects, int maxFetchCycles, ApiConnection apiConnection, Func<Management[], Task> callback);

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
                HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##", userConfig.GetText("date_of_config"));
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedFor##", DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                HtmlTemplate = HtmlTemplate.Replace("##DeviceFilter##", string.Join("; ", Array.ConvertAll(Managements, management => management.NameAndDeviceNames())));
                HtmlTemplate = HtmlTemplate.Replace("##Body##", htmlReport.ToString());
                htmlExport = HtmlTemplate.ToString();
            }
            return htmlExport;
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

        public static ReportBase ConstructReport(string filterInput, DeviceFilter deviceFilter, TimeFilter timeFilter, ReportType reportType, UserConfig userConfig)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput, reportType, deviceFilter, timeFilter);

            return reportType switch
            {
                ReportType.Statistics => new ReportStatistics(query, userConfig, reportType),
                ReportType.Rules => new ReportRules(query, userConfig, reportType),
                ReportType.ResolvedRules => new ReportRules(query, userConfig, reportType),
                ReportType.Changes => new ReportChanges(query, userConfig, reportType),
                ReportType.NatRules => new ReportNatRules(query, userConfig, reportType),
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
    }
}

using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Data;
using FWO.Report.Filter;
using FWO.Services;
using FWO.Services.RuleTreeBuilder;
using System.Text;
using System.Reflection;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using PuppeteerSharp.BrowserData;
using HtmlAgilityPack;
using System.Runtime.InteropServices;
using System.Net;

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
        // kept as the pristine source so that the rendered report can be discarded again: the builder
        // below is substituted in place, so after an export it holds a full copy of the report body
        private static readonly string HtmlTemplateSource = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8""/>
      <title>##Title##</title>
         {NotificationTableBodyBuilder.HtmlTableStyleBlock}
    </head>
    <body>
        <h2>##Title##</h2>
        <p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>
        <p>##GeneratedOn##: ##Date## (UTC)</p>
        <p>##OwnerFilters##</p>
        <p>##OtherFilters##</p>
        <p>##Filter##</p>
        <hr>
        ##ToC##
        <hr>
        ##Body##
    </body>
</html>";

        protected StringBuilder HtmlTemplate = new(HtmlTemplateSource);

        public readonly DynGraphqlQuery Query;
        public UserConfig userConfig;
        public ReportType ReportType { get; set; }
        public ReportData ReportData { get; set; } = new();
        public int CustomWidth { get; set; } = 0;
        public int CustomHeight { get; set; } = 0;
        protected int Levelshift = 0;

        protected string htmlExport = "";
        protected string htmlBodyExport = "";
        protected bool htmlBodyExportValid = false;

        private string TocHTMLTemplate = "<div id=\"toc_container\"><h2>##ToCHeader##</h2><ul class=\"toc_list\">##ToCList##</ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style>";

        public bool GotObjectsInReport { get; protected set; } = false;

        // every pdf export starts its own headless browser, which costs a few hundred megabytes while it
        // runs. nothing else limits how many exports happen at once, so without this gate a handful of
        // simultaneous exports is enough to drive the whole service into the out of memory killer.
        protected const int kMaxConcurrentPdfRenders = 2;
        private static readonly SemaphoreSlim PdfRenderGate = new(kMaxConcurrentPdfRenders, kMaxConcurrentPdfRenders);

        // the gate is process wide, so every timeout below has to be finite: a render that hangs while
        // holding a slot would otherwise block every later export in this process for good. the browser
        // gets its own deadlines so that a stuck render gives its slot back instead of keeping it, and
        // waiting for a slot is bounded as well so that a queued export fails visibly rather than hanging.
        protected const int kPdfRenderGateWaitSeconds = 120;
        private const int kBrowserLaunchTimeoutMs = 60_000;
        private const int kBrowserProtocolTimeoutMs = 180_000;
        private const int kPageOperationTimeoutMs = 120_000;
        private const int kBrowserCloseTimeoutMs = 30_000;


        protected ReportBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType)
        {
            Query = query;
            userConfig = UserConfig;
            ReportType = reportType;
        }

        /// <summary>
        /// Drops the cached html rendering of this report. It is only needed while an export is being
        /// prepared - keeping it afterwards pins a multi megabyte string to the report for as long as
        /// the page holds it, on top of the report data itself. The template is reset as well, because
        /// the substitutions happen in place and leave a second copy of the body inside the builder.
        /// </summary>
        public void ReleaseExportCache()
        {
            htmlExport = "";
            htmlBodyExport = "";
            htmlBodyExportValid = false;
            HtmlTemplate = new(HtmlTemplateSource);
        }

        public abstract Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct);

        public virtual async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            await callback(ReportData);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public virtual Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            throw new NotImplementedException();
        }

        public virtual bool NoRuleFound()
        {
            return true;
        }

        public virtual bool NoChangesFound()
        {
            return true;
        }

        public abstract string ExportToCsv();

        public abstract string ExportToJson();

        public abstract string ExportToHtml();

        public virtual string ExportToHtmlBody()
        {
            if (!htmlBodyExportValid)
            {
                ExportToHtml();
                if (!htmlBodyExportValid && !string.IsNullOrWhiteSpace(htmlExport))
                {
                    htmlBodyExport = htmlExport;
                    htmlBodyExportValid = true;
                }
            }
            return htmlBodyExportValid ? htmlBodyExport : htmlExport;
        }

        public abstract string SetDescription();

        public static ReportBase ConstructReport(ReportTemplate reportFilter, UserConfig userConfig, IRuleTreeBuilder? ruleTreeBuilder = null)
        {
            DynGraphqlQuery query = Compiler.Compile(reportFilter);
            ReportType repType = (ReportType)reportFilter.ReportParams.ReportType;
            WorkflowFilter workflowFilter = BuildEffectiveWorkflowFilter(query, reportFilter.ReportParams.WorkflowFilter);
            return repType switch
            {
                ReportType.Statistics => new ReportStatistics(query, userConfig, repType),
                ReportType.Rules => new ReportRules(query, userConfig, repType, ruleTreeBuilder),
                ReportType.ResolvedRules => new ReportRules(query, userConfig, repType, ruleTreeBuilder),
                ReportType.ResolvedRulesTech => new ReportRules(query, userConfig, repType, ruleTreeBuilder),
                ReportType.Changes => new ReportChanges(query, userConfig, repType, reportFilter.ReportParams.TimeFilter, reportFilter.ReportParams.IncludeObjects),
                ReportType.ResolvedChanges => new ReportChanges(query, userConfig, repType, reportFilter.ReportParams.TimeFilter, reportFilter.ReportParams.IncludeObjects),
                ReportType.ResolvedChangesTech => new ReportChanges(query, userConfig, repType, reportFilter.ReportParams.TimeFilter, reportFilter.ReportParams.IncludeObjects),
                ReportType.NatRules => new ReportNatRules(query, userConfig, repType),
                ReportType.Recertification => new ReportRules(query, userConfig, repType, ruleTreeBuilder),
                ReportType.UnusedRules => new ReportRules(query, userConfig, repType, ruleTreeBuilder),
                ReportType.Connections => new ReportConnections(query, userConfig, repType),
                ReportType.AppRules => new ReportAppRules(query, userConfig, repType, reportFilter.ReportParams.ModellingFilter),
                ReportType.VarianceAnalysis => new ReportVariances(query, userConfig, repType),
                ReportType.ComplianceReport => new ReportCompliance(query, userConfig, repType, reportFilter.ReportParams),
                ReportType.ComplianceDiffReport => new ReportComplianceDiff(query, userConfig, repType, reportFilter.ReportParams),
                ReportType.OwnerRecertification => new ReportOwnerRecerts(query, userConfig, repType),
                ReportType.RecertificationEvent => new RecertificateOwner(query, userConfig, repType),
                ReportType.RecertEventReport => new ReportRecertEvent(query, userConfig, repType, ruleTreeBuilder),
                ReportType.TicketReport => new ReportTickets(query, userConfig, repType, workflowFilter),
                ReportType.TicketChangeReport => new ReportTicketChanges(query, userConfig, repType, workflowFilter),
                ReportType.Owners => new ReportOwners(query, userConfig, repType),
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }

        private static WorkflowFilter BuildEffectiveWorkflowFilter(DynGraphqlQuery query, WorkflowFilter workflowFilter)
        {
            WorkflowFilter effectiveWorkflowFilter = new(workflowFilter);

            if (query.WorkflowTaskTypes.Count > 0)
            {
                effectiveWorkflowFilter.TaskTypes = [.. query.WorkflowTaskTypes];
            }

            if (query.WorkflowStateIds.Count > 0)
            {
                effectiveWorkflowFilter.StateIds = [.. query.WorkflowStateIds];
            }

            if (!string.IsNullOrWhiteSpace(query.WorkflowPhase))
            {
                effectiveWorkflowFilter.Phase = query.WorkflowPhase;
            }

            if (query.WorkflowReferenceDateFilter.HasValue)
            {
                effectiveWorkflowFilter.ReferenceDate = query.WorkflowReferenceDateFilter.Value;
            }

            return effectiveWorkflowFilter;
        }

        public static string GetLinkAddress(OutputLocation location, string reportId, string type, int chapterNumber, long id, ReportType reportType)
        {
            string page = location == OutputLocation.report ? PageName.ReportGeneration : PageName.Certification;
            string link;
            if (reportType.IsChangeReport())
            {
                link = location == OutputLocation.export ? $"#" : $"{page}#goto-all-{reportId}-";
            }
            else
            {
                link = location == OutputLocation.export ? $"#" : $"{page}#goto-report-{reportId}-";
            }
            return $"{link}{type}{chapterNumber}x{id}";
        }

        public static string ConstructLink(string symbol, string name, string style, string linkAddress)
        {
            return $"<span class=\"{symbol}\">&nbsp;</span><a onclick=\"event.stopPropagation();\" href=\"{linkAddress}\" target=\"_top\" style=\"{style}\">{name}</a>";
        }

        protected static string OutputCsv(string? input)
        {
            return $"\"{input?.Replace("\"", "\"\"") ?? ""}\",";
        }

        protected static string EncodeHtml(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        protected static string FormatHtmlCell(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : EncodeHtml(value)
                    .Replace("\r\n", "<br>")
                    .Replace("\n", "<br>")
                    .Replace("\r", "<br>");
        }

        protected string GenerateHtmlFrameBase(string title, string filter, DateTime date, StringBuilder htmlReport, string? otherFilter = null, string? ownerFilter = null, TimeFilter? timeFilter = null)
        {
            if (string.IsNullOrEmpty(htmlExport))
            {
                string body = htmlReport.ToString();
                HtmlTemplate = HtmlTemplate.Replace("##Title##", title);
                ReplaceFilter(filter);
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedOn##", userConfig.GetText("generated_on"));
                HtmlTemplate = HtmlTemplate.Replace("##Date##", date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                ReplaceDateOfConfig(timeFilter);
                ReplaceOwnerFilter(ownerFilter);
                ReplaceOtherFilter(otherFilter);

                string htmlToC = BuildHTMLToC(body);
                HtmlTemplate = HtmlTemplate.Replace("##ToC##", htmlToC);
                HtmlTemplate = HtmlTemplate.Replace("##Body##", body);
                htmlExport = HtmlTemplate.ToString();
                htmlBodyExport = NotificationTableBodyBuilder.HtmlTableStyleBlock + body;
                htmlBodyExportValid = true;
            }
            return htmlExport;
        }

        private void ReplaceFilter(string filter)
        {
            if (filter != "")
            {
                HtmlTemplate = HtmlTemplate.Replace("##Filter##", userConfig.GetText("filter") + ": " + filter);
            }
            else
            {
                HtmlTemplate = HtmlTemplate.Replace("<p>##Filter##</p>", "");
            }
        }

        private void ReplaceDateOfConfig(TimeFilter? timeFilter)
        {
            if (ReportType.IsChangeReport() || ReportType == ReportType.TicketChangeReport)
            {
                (string startTime, string stopTime) = ReportType == ReportType.TicketChangeReport && timeFilter == null
                    ? ((string)Query.QueryVariables["ticket_time_start"], (string)Query.QueryVariables["ticket_time_end"])
                    : DynGraphqlQuery.ResolveTimeRange(timeFilter ?? new());
                string timeRange = $"{userConfig.GetText("change_time")}: " +
                    $"{userConfig.GetText("from")}: {ToUtcString(startTime)}, " +
                    $"{userConfig.GetText("until")}: {ToUtcString(stopTime)}";
                HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##: ##GeneratedFor##", timeRange);
            }
            else if (ReportType.HasTimeFilter())
            {
                HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##", userConfig.GetText("date_of_config"));
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedFor##", ToUtcString(Query.ReportTimeString));
            }
            else
            {
                HtmlTemplate = HtmlTemplate.Replace("<p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>", "");
            }
        }

        private void ReplaceOwnerFilter(string? ownerFilter)
        {
            if (ownerFilter != null && ownerFilter != "")
            {
                HtmlTemplate = HtmlTemplate.Replace("##OwnerFilters##", userConfig.GetText("owners") + ": " + ownerFilter);
            }
            else
            {
                HtmlTemplate = HtmlTemplate.Replace("<p>##OwnerFilters##</p>", "");
            }
        }

        private void ReplaceOtherFilter(string? otherFilter)
        {
            if (otherFilter != null && ReportType != ReportType.RecertEventReport)
            {
                if (ReportType.IsWorkflowReport())
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OtherFilters##", userConfig.GetText("workflow_filters") + ": " + otherFilter);
                }
                else if (ReportType.IsRulebaseReport())
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OtherFilters##", userConfig.GetText("managements") + ": " + otherFilter);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OtherFilters##", userConfig.GetText("devices") + ": " + otherFilter);
                }
            }
            else
            {
                HtmlTemplate = HtmlTemplate.Replace("<p>##OtherFilters##</p>", "");
            }
        }

        protected static string ToUtcString(string? timestring)
        {
            try
            {
                return timestring != null ? DateTime.Parse(timestring).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK") : "";
            }
            catch (Exception)
            {
                return timestring ?? "";
            }
        }

        private async Task<string?> CreatePDFViaPuppeteer(string html, PaperFormat format)
        {
            OperatingSystem? os = Environment.OSVersion;

            string path = "";
            Platform platform = Platform.Unknown;
            const SupportedBrowser wantedBrowser = SupportedBrowser.Chrome;

            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                    platform = Platform.Win32;
                    break;
                case PlatformID.Unix:
                    path = GlobalConst.ChromeBinPathLinux;
                    platform = Platform.Linux;
                    break;
                default:
                    break;
            }

            BrowserFetcher browserFetcher = new(new BrowserFetcherOptions() { Platform = platform, Browser = wantedBrowser, Path = path });

            IEnumerable<InstalledBrowser>? allInstalledBrowsers = browserFetcher.GetInstalledBrowsers().Where(_ => _.Browser == wantedBrowser);

            string? executablePath = null;

            if (!allInstalledBrowsers.Any())
            {
                if (os.Platform == PlatformID.Win32NT)
                {
                    Log.WriteInfo("Browser", $"Browser not found for Windows! Trying to download...");
                    await browserFetcher.DownloadAsync();
                    allInstalledBrowsers = browserFetcher.GetInstalledBrowsers().Where(_ => _.Browser == wantedBrowser);
                }
                else
                {
                    executablePath = SystemChromium.GetPath() ??
                        throw new EnvironmentException($"Found no installed {wantedBrowser} instances and no system chromium!");
                    Log.WriteInfo("Browser", $"No installed {wantedBrowser} found, falling back to system chromium at: {executablePath}");
                }
            }

            if (executablePath == null)
            {
                string? newestBuildId = allInstalledBrowsers.Max(_ => _.BuildId);

                if (string.IsNullOrWhiteSpace(newestBuildId))
                {
                    throw new EnvironmentException($"Invalid build ID!");
                }

                InstalledBrowser? latestInstalledBrowser = allInstalledBrowsers.Single(_ => _.BuildId == newestBuildId) ??
                    throw new EnvironmentException($"Found no installed {wantedBrowser} instances with a valid build ID!");

                Log.WriteInfo("Test Log", $"Selecting latest installed {wantedBrowser}({latestInstalledBrowser.BuildId}) at: {latestInstalledBrowser.GetExecutablePath()}");

                executablePath = latestInstalledBrowser.GetExecutablePath();
            }

            return await RunGatedPdfRender(() => RenderPdfInBrowser(html, format, executablePath, wantedBrowser));
        }

        /// <summary>
        /// Runs a pdf render behind the concurrency gate. The gate is held for the whole render rather
        /// than just the browser launch, so that at most <see cref="kMaxConcurrentPdfRenders"/> headless
        /// browsers are resident at any one time. The slot is returned even if the render throws.
        /// Waiting for a slot is bounded: the gate is shared by the whole process, so an export that
        /// cannot get one has to fail with a message rather than block the user's circuit forever.
        /// </summary>
        protected static async Task<string?> RunGatedPdfRender(Func<Task<string?>> render)
        {
            if (!await PdfRenderGate.WaitAsync(TimeSpan.FromSeconds(kPdfRenderGateWaitSeconds)))
            {
                Log.WriteAlert("Report Export", $"No pdf render slot became available within {kPdfRenderGateWaitSeconds} seconds.");
                throw new TimeoutException($"Too many report exports are running at the moment. Please try again in a few minutes.");
            }
            try
            {
                return await render();
            }
            finally
            {
                PdfRenderGate.Release();
            }
        }

        /// <summary>
        /// Renders the given html to a base64 encoded pdf in a dedicated headless browser instance.
        /// Callers must hold <see cref="PdfRenderGate"/> for the duration of this call.
        /// </summary>
        private async Task<string?> RenderPdfInBrowser(string html, PaperFormat format, string executablePath, SupportedBrowser wantedBrowser)
        {
            IBrowser? browser;

            try
            {
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    Timeout = kBrowserLaunchTimeoutMs,
                    ProtocolTimeout = kBrowserProtocolTimeoutMs,
                });
            }
            catch (Exception)
            {
                Log.WriteAlert("Test Log", $"Couldn't start {wantedBrowser} instance!");
                throw new EnvironmentException($"Couldn't start {wantedBrowser} instance!");
            }

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(html, new SetContentOptions { Timeout = kPageOperationTimeoutMs });

                PuppeteerSharp.Media.PaperFormat? pupformat = GetPuppeteerPaperFormat(format) ?? throw new KeyNotFoundException();

                PdfOptions pdfOptions = new() { Outline = true, DisplayHeaderFooter = false, Landscape = true, PrintBackground = true, Format = pupformat, Timeout = kPageOperationTimeoutMs, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                byte[]? pdfData = await page.PdfDataAsync(pdfOptions);

                return Convert.ToBase64String(pdfData);
            }
            catch (KeyNotFoundException)
            {
                throw new NotSupportedException("This paper kind is currently not supported. Please choose another one or \"Custom\" for a custom size.");
            }
            catch (Exception exception)
            {
                // reporting a timed out or crashed render as an unsupported paper format sends whoever
                // has to diagnose it looking in the wrong place - the render holds a process wide slot
                Log.WriteError("Report Export", "Rendering the report to pdf failed.", exception);
                throw;
            }
            finally
            {
                await CloseBrowserSafely(browser, wantedBrowser);
            }
        }

        /// <summary>
        /// Shuts the headless browser down without letting a stuck shutdown hold on to the render slot.
        /// A graceful close can hang on a wedged renderer, so it is given a deadline after which the
        /// browser is disposed - which kills the process - and the slot is released either way.
        /// </summary>
        private static async Task CloseBrowserSafely(IBrowser browser, SupportedBrowser wantedBrowser)
        {
            try
            {
                await browser.CloseAsync().WaitAsync(TimeSpan.FromMilliseconds(kBrowserCloseTimeoutMs));
            }
            catch (Exception exception)
            {
                Log.WriteError("Report Export", $"Closing the {wantedBrowser} instance failed, killing it instead.", exception);
            }
            finally
            {
                browser.Dispose();
            }
        }

        public static List<ToCHeader> CreateTOCContent(string html)
        {
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            List<HtmlNode>? headings = [.. doc.DocumentNode.Descendants().Where(n => n.Name.StartsWith('h') && n.Name.Length == 2 && n.Name != "hr")];
            List<ToCHeader> tocs = [];

            int i = 0;

            foreach (HtmlNode heading in headings)
            {
                string headText = heading.InnerText.Trim();

                if (heading.Name == "h4" && tocs.Count > 0)
                {
                    tocs[i - 1].Items.Add(new ToCItem(headText, heading.Id));
                }
                else if (heading.Name == "h5" && tocs.Count > 0 && tocs[i - 1].Items.Count > 0)
                {
                    tocs[i - 1].Items[^1].SubItems.Add(new ToCItem(headText, heading.Id));
                }
                else if (heading.Name == "h6" && tocs.Count > 0 && tocs[i - 1].Items.Count > 0 && tocs[i - 1].Items[^1].SubItems.Count > 0)
                {
                    tocs[i - 1].Items[^1].SubItems[^1].SubItems.Add(new ToCItem(headText, heading.Id));
                }
                else
                {
                    tocs.Add(new(headText, heading.Id));
                    i++;
                }
            }
            return tocs;
        }

        public string BuildHTMLToC(string html)
        {
            bool tocTemplateValid = IsValidHTML(TocHTMLTemplate);

            if (!tocTemplateValid)
            {
                throw new ArgumentException(userConfig.GetText("E9302"));
            }

            List<ToCHeader>? tocHeaders = CreateTOCContent(html);
            if (tocHeaders.Count == 0)
            {
                return "";
            }

            TocHTMLTemplate = TocHTMLTemplate.Replace("##ToCHeader##", userConfig.GetText("tableofcontent"));

            StringBuilder sb = new();
            foreach (ToCHeader toCHeader in tocHeaders)
            {
                AppendHeader(sb, toCHeader);
            }

            TocHTMLTemplate = TocHTMLTemplate.Replace("##ToCList##", sb.ToString());
            bool tocValidHTML = IsValidHTML(TocHTMLTemplate);
            if (!tocValidHTML)
            {
                throw new ArgumentException(userConfig.GetText("E9302"));
            }

            return TocHTMLTemplate;
        }

        private static void AppendHeader(StringBuilder sb, ToCHeader toCHeader)
        {
            sb.AppendLine($"<li><a href=\"#{toCHeader.Id}\">{toCHeader.Title}</a></li>");

            if (toCHeader.Items.Count > 0)
            {
                sb.AppendLine("<ul>");

                foreach (ToCItem tocItem in toCHeader.Items)
                {
                    AppendItem(sb, tocItem);
                }
                sb.AppendLine("</ul>");
            }
        }

        private static void AppendItem(StringBuilder sb, ToCItem tocItem)
        {
            sb.AppendLine($"<li class=\"subli\"><a href=\"#{tocItem.Id}\">{tocItem.Title}</a></li>");
            if (tocItem.SubItems.Count > 0)
            {
                sb.AppendLine("<ul>");
                foreach (ToCItem subItem in tocItem.SubItems)
                {
                    AppendSubItem(sb, subItem);
                }
                sb.AppendLine("</ul>");
            }
        }

        private static void AppendSubItem(StringBuilder sb, ToCItem subItem)
        {
            sb.AppendLine($"<li class=\"subli\"><a href=\"#{subItem.Id}\">{subItem.Title}</a></li>");
            if (subItem.SubItems.Count > 0)
            {
                sb.AppendLine("<ul>");
                foreach (ToCItem subsubItem in subItem.SubItems)
                {
                    sb.AppendLine($"<li class=\"subli\"><a href=\"#{subsubItem.Id}\">{subsubItem.Title}</a></li>");
                }
                sb.AppendLine("</ul>");
            }
        }

        protected string Headline(string? title, int level)
        {
            return $"<h{level + Levelshift} id=\"{Guid.NewGuid()}\">{title}</h{level + Levelshift}>";
        }

        public static bool IsValidHTML(string html)
        {
            try
            {
                HtmlDocument? doc = new();
                doc.LoadHtml(html);
                return !doc.ParseErrors.Any();
            }
            catch (Exception)
            {
                return false;
            }

        }

        public PuppeteerSharp.Media.PaperFormat? GetPuppeteerPaperFormat(PaperFormat format)
        {
            if (format == PaperFormat.Custom)
                return new PuppeteerSharp.Media.PaperFormat(CustomWidth, CustomHeight);

            PropertyInfo[] propertyInfos = typeof(PuppeteerSharp.Media.PaperFormat).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);

            PropertyInfo? prop = propertyInfos.SingleOrDefault(_ => _.Name == format.ToString());

            if (prop == null)
                return default;

            PuppeteerSharp.Media.PaperFormat? propFormat = (PuppeteerSharp.Media.PaperFormat?)prop.GetValue(null);

            if (propFormat is null)
                return default;

            return propFormat;
        }

        public virtual async Task<string?> ToPdf(string html, PaperFormat format)
        {
            return await CreatePDFViaPuppeteer(html, format);
        }

        public virtual async Task<string?> ToPdf(string html)
        {
            return await CreatePDFViaPuppeteer(html, PaperFormat.A4);
        }

        public virtual async Task<string?> ToPdf(PaperFormat format)
        {
            return await CreatePDFViaPuppeteer(htmlExport, format);
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
                ObjectType.AccessRole => Icons.User,
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

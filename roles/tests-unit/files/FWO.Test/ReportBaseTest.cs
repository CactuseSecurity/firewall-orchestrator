using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report.Data;
using FWO.Report;
using FWO.Report.Filter;
using NSubstitute;
using NUnit.Framework;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class ReportBaseTest
    {
        private const int kFailedRenderAttempts = 3;
        private const int kConcurrentRenderProbes = 6;
        private const int kShortGateWaitMilliseconds = 50;
        private const int kShortCloseWaitMilliseconds = 50;
        private const int kUnknownPaperFormat = 999;
        private const string kRenderedHtml = "<html><body><p>report</p></body></html>";
        private static readonly byte[] kPdfData = [1, 2, 3, 4];

        private sealed class TestReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public static string OutputCsvPublic(string? input)
            {
                return OutputCsv(input);
            }

            public static string ToUtcStringPublic(string? input)
            {
                return ToUtcString(input);
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                return string.Empty;
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class GatedRenderReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public static Task<string?> RunGated(Func<Task<string?>> render)
            {
                return RunGatedPdfRender(render);
            }

            public static Task<string?> RunGated(Func<Task<string?>> render, TimeSpan gateWaitTimeout)
            {
                return RunGatedPdfRender(render, gateWaitTimeout);
            }

            public static int MaxConcurrentRenders => kMaxConcurrentPdfRenders;

            public static int GateWaitSeconds => kPdfRenderGateWaitSeconds;

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                return string.Empty;
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class BrowserRenderReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public Task<string?> RenderIn(IBrowser browser, string html, FWO.Report.PaperFormat format)
            {
                return RenderPdfInLaunchedBrowser(browser, html, format, SupportedBrowser.Chrome);
            }

            public static Task CloseSafely(IBrowser browser, TimeSpan? closeTimeout = null)
            {
                return CloseBrowserSafely(browser, SupportedBrowser.Chrome, closeTimeout);
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                return string.Empty;
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class FrameReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public string Body { get; set; } = "<p>frame body</p>";

            public string BuildHtmlFrame()
            {
                return GenerateHtmlFrameBase("Frame Title", "", DateTime.Parse("2026-01-01T00:00:00Z"), new StringBuilder(Body));
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                return BuildHtmlFrame();
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class LazyBodyReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public int ExportCalls { get; private set; }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                ExportCalls++;
                htmlBodyExport = "<p>lazy frame body</p>";
                htmlBodyExportValid = true;
                return "<html><body><p>lazy frame body</p></body></html>";
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class HtmlOnlyReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public int ExportCalls { get; private set; }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                ExportCalls++;
                htmlExport = "<html><body><p>html only</p></body></html>";
                return htmlExport;
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        [Test]
        public void OutputCsvEscapesQuotesAndNull()
        {
            Assert.That(TestReportBase.OutputCsvPublic("a\"b"), Is.EqualTo("\"a\"\"b\","));
            Assert.That(TestReportBase.OutputCsvPublic(null), Is.EqualTo("\"\","));
        }

        [Test]
        public void GetLinkAddressUsesReportPrefixForNonChangeReports()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.report, "rep42", "rule", 3, 99, ReportType.Rules);

            Assert.That(link, Is.EqualTo($"{PageName.ReportGeneration}#goto-report-rep42-rule3x99"));
        }

        [Test]
        public void GetLinkAddressUsesAllPrefixForChangeReports()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.report, "rep42", "rule", 3, 99, ReportType.Changes);

            Assert.That(link, Is.EqualTo($"{PageName.ReportGeneration}#goto-all-rep42-rule3x99"));
        }

        [Test]
        public void GetLinkAddressUsesCertificationPageOutsideReportLocation()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.certification, "rep42", "svc", 1, 5, ReportType.Rules);

            Assert.That(link, Is.EqualTo($"{PageName.Certification}#goto-report-rep42-svc1x5"));
        }

        [Test]
        public void GetLinkAddressUsesHashOnlyForExportLocation()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.export, "rep42", "nwobj", 2, 7, ReportType.Changes);

            Assert.That(link, Is.EqualTo("#nwobj2x7"));
        }

        [Test]
        public void ConstructLinkBuildsExpectedAnchorHtml()
        {
            string link = ReportBase.ConstructLink("icon-test", "Test Name", "color:red;", "#dest");

            Assert.That(link, Is.EqualTo("<span class=\"icon-test\">&nbsp;</span><a onclick=\"event.stopPropagation();\" href=\"#dest\" target=\"_top\" style=\"color:red;\">Test Name</a>"));
        }

        [Test]
        public void ExportToHtmlBodyReturnsBodyFragmentForFramedReports()
        {
            FrameReportBase report = new();

            string html = report.ExportToHtml();
            string body = report.ExportToHtmlBody();

            Assert.That(html, Does.Contain("<html>"));
            Assert.That(body, Does.Contain("<style>"));
            Assert.That(body, Does.Contain("border-collapse"));
            Assert.That(body, Does.Not.Contain("<html>"));
            Assert.That(body, Does.Not.Contain("<body>"));
            Assert.That(body, Does.Contain("<p>frame body</p>"));
        }

        [Test]
        public void ExportToHtmlBodyTriggersExportWhenBodyIsNotYetCached()
        {
            LazyBodyReportBase report = new();

            string body = report.ExportToHtmlBody();

            Assert.That(report.ExportCalls, Is.EqualTo(1));
            Assert.That(body, Is.EqualTo("<p>lazy frame body</p>"));
        }

        [Test]
        public void ExportToHtmlBodyFallsBackToFullHtmlWhenBodyIsNeverPopulated()
        {
            HtmlOnlyReportBase report = new();

            string firstBody = report.ExportToHtmlBody();
            string secondBody = report.ExportToHtmlBody();

            Assert.That(report.ExportCalls, Is.EqualTo(1));
            Assert.That(firstBody, Is.EqualTo("<html><body><p>html only</p></body></html>"));
            Assert.That(secondBody, Is.EqualTo(firstBody));
        }

        [Test]
        public void GetPuppeteerPaperFormatReturnsCustomFormatFromDimensions()
        {
            TestReportBase report = new()
            {
                CustomWidth = 210,
                CustomHeight = 297
            };

            var format = report.GetPuppeteerPaperFormat(FWO.Report.PaperFormat.Custom);

            Assert.That(format, Is.Not.Null);
            Assert.That(format!.Width, Is.EqualTo(210));
            Assert.That(format.Height, Is.EqualTo(297));
        }

        [Test]
        public void GetPuppeteerPaperFormatReturnsNullForUnknownFormat()
        {
            TestReportBase report = new();

            var format = report.GetPuppeteerPaperFormat((FWO.Report.PaperFormat)999);

            Assert.That(format, Is.Null);
        }

        [Test]
        public void ToUtcStringConvertsValidDatesAndKeepsInvalidText()
        {
            Assert.That(TestReportBase.ToUtcStringPublic("2026-07-08T10:30:00+02:00"), Does.Contain("2026-07-08T08:30:00"));
            Assert.That(TestReportBase.ToUtcStringPublic("not-a-date"), Is.EqualTo("not-a-date"));
            Assert.That(TestReportBase.ToUtcStringPublic(null), Is.EqualTo(""));
        }

        [Test]
        public void CreateTOCContentBuildsNestedEntries()
        {
            List<ToCHeader> toc = ReportBase.CreateTOCContent("<h2>Top</h2><h4>Child</h4><h5>Grandchild</h5><h6>GreatGrandchild</h6>");

            Assert.That(toc, Has.Count.EqualTo(1));
            Assert.That(toc[0].Title, Is.EqualTo("Top"));
            Assert.That(toc[0].Items, Has.Count.EqualTo(1));
            Assert.That(toc[0].Items[0].Title, Is.EqualTo("Child"));
            Assert.That(toc[0].Items[0].SubItems, Has.Count.EqualTo(1));
            Assert.That(toc[0].Items[0].SubItems[0].Title, Is.EqualTo("Grandchild"));
            Assert.That(toc[0].Items[0].SubItems[0].SubItems, Has.Count.EqualTo(1));
            Assert.That(toc[0].Items[0].SubItems[0].SubItems[0].Title, Is.EqualTo("GreatGrandchild"));
        }

        [Test]
        public void BuildHTMLToCReturnsEmptyWithoutHeadings()
        {
            FrameReportBase report = new();

            string toc = report.BuildHTMLToC("<p>No headings here</p>");

            Assert.That(toc, Is.EqualTo(""));
        }

        [Test]
        public void BuildHTMLToCRendersTableOfContentsWithHeadings()
        {
            FrameReportBase report = new();

            string toc = report.BuildHTMLToC("<h2>Top</h2><h4>Child</h4>");

            Assert.That(toc, Does.Contain("Table of content"));
            Assert.That(toc, Does.Contain("Top"));
            Assert.That(toc, Does.Contain("Child"));
            Assert.That(toc, Does.Contain("<ul class=\"toc_list\">"));
        }

        [Test]
        public void GetIconClassMapsKnownObjectTypes()
        {
            Assert.That(ReportBase.GetIconClass(ObjCategory.user, ObjectType.Group), Is.EqualTo(Icons.UserGroup));
            Assert.That(ReportBase.GetIconClass(ObjCategory.nobj, ObjectType.Host), Is.EqualTo(Icons.Host));
            Assert.That(ReportBase.GetIconClass(ObjCategory.nsrv, ObjectType.AccessRole), Is.EqualTo(Icons.User));
            Assert.That(ReportBase.GetIconClass(null, null), Is.EqualTo(""));
        }

        [Test]
        public void ExportToHtmlKeepsTheRenderedHtmlCachedUntilItIsReleased()
        {
            FrameReportBase report = new();

            string firstExport = report.ExportToHtml();

            Assert.That(firstExport, Does.Contain("frame body"));
            // the cache is what makes a repeated export cheap, and also what keeps the string alive
            Assert.That(report.ExportToHtml(), Is.SameAs(firstExport));
        }

        [Test]
        public void ReleaseExportCacheDropsTheCachedHtmlSoTheNextExportRebuildsIt()
        {
            FrameReportBase report = new();
            string firstExport = report.ExportToHtml();

            report.ReleaseExportCache();
            string secondExport = report.ExportToHtml();

            Assert.That(secondExport, Does.Contain("frame body"));
            Assert.That(secondExport, Is.Not.SameAs(firstExport));
        }

        [Test]
        public void ReleaseExportCacheAlsoDropsTheCachedHtmlBody()
        {
            FrameReportBase report = new();
            string firstBody = report.ExportToHtmlBody();
            Assert.That(firstBody, Does.Contain("frame body"));

            report.ReleaseExportCache();

            Assert.That(report.ExportToHtmlBody(), Is.Not.SameAs(firstBody));
        }

        [Test]
        public void ReleaseExportCacheIsSafeToCallWithoutAPreviousExport()
        {
            FrameReportBase report = new();

            Assert.DoesNotThrow(report.ReleaseExportCache);
            Assert.That(report.ExportToHtml(), Does.Contain("frame body"));
        }

        [Test]
        public async Task GatedPdfRenderReturnsTheRenderedResult()
        {
            string? result = await GatedRenderReportBase.RunGated(() => Task.FromResult<string?>("pdf"));

            Assert.That(result, Is.EqualTo("pdf"));
        }

        [Test]
        public void GatedPdfRenderPropagatesRenderFailures()
        {
            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await GatedRenderReportBase.RunGated(() => throw new NotSupportedException("bad paper format")));
        }

        [Test]
        public async Task GatedPdfRenderReturnsTheSlotAfterAFailedRender()
        {
            // a slot leaked on the failure path would deadlock every later export
            for (int attempt = 0; attempt < kFailedRenderAttempts; attempt++)
            {
                Assert.ThrowsAsync<NotSupportedException>(async () =>
                    await GatedRenderReportBase.RunGated(() => throw new NotSupportedException("bad paper format")));
            }

            Task<string?> afterFailures = GatedRenderReportBase.RunGated(() => Task.FromResult<string?>("still works"));

            Assert.That(await afterFailures, Is.EqualTo("still works"));
        }

        /// <summary>
        /// Runs more renders than the gate allows at once and reports how many of them were ever
        /// inside the gate at the same time.
        /// </summary>
        private static async Task<int> MeasurePeakConcurrentRenders()
        {
            int running = 0;
            int peak = 0;
            object peakLock = new();
            using SemaphoreSlim release = new(0, kConcurrentRenderProbes);

            async Task<string?> Render()
            {
                lock (peakLock)
                {
                    running++;
                    peak = Math.Max(peak, running);
                }
                await release.WaitAsync();
                lock (peakLock)
                {
                    running--;
                }
                return "pdf";
            }

            List<Task<string?>> renders = [];
            for (int probe = 0; probe < kConcurrentRenderProbes; probe++)
            {
                renders.Add(GatedRenderReportBase.RunGated(Render));
            }

            // let them all finish, then check how many were ever inside the gate at the same time
            release.Release(kConcurrentRenderProbes);
            await Task.WhenAll(renders);
            return peak;
        }

        /// <summary>
        /// Holds every render slot until <see cref="GateOccupation.ReleaseAll"/> is called, so that a
        /// further render has to wait for the gate. Callers must release in a finally block, or the
        /// gate stays saturated for every later test in this process.
        /// </summary>
        private sealed class GateOccupation(SemaphoreSlim release, List<Task<string?>> occupants)
        {
            public async Task ReleaseAll()
            {
                release.Release(occupants.Count);
                await Task.WhenAll(occupants);
                release.Dispose();
            }
        }

        private static async Task<GateOccupation> SaturateTheGate()
        {
            int slots = GatedRenderReportBase.MaxConcurrentRenders;
            SemaphoreSlim release = new(0, slots);
            using SemaphoreSlim entered = new(0, slots);
            List<Task<string?>> occupants = [];

            for (int slot = 0; slot < slots; slot++)
            {
                occupants.Add(GatedRenderReportBase.RunGated(async () =>
                {
                    entered.Release();
                    await release.WaitAsync();
                    return "pdf";
                }));
            }
            // only once every occupant is inside the gate is the next render guaranteed to have to wait
            for (int slot = 0; slot < slots; slot++)
            {
                await entered.WaitAsync();
            }
            return new GateOccupation(release, occupants);
        }

        [Test]
        public async Task GatedPdfRenderNeverRunsMoreBrowsersThanTheLimit()
        {
            // this is the whole point of the gate: each render is a headless browser worth hundreds
            // of megabytes, so more of them at once than the limit is what triggers the oom killer
            int peak = await MeasurePeakConcurrentRenders();

            Assert.That(peak, Is.LessThanOrEqualTo(GatedRenderReportBase.MaxConcurrentRenders));
            Assert.That(peak, Is.GreaterThan(0));
        }

        [Test]
        public async Task GatedPdfRenderFailsInsteadOfWaitingForeverForABusyGate()
        {
            // the gate is process wide, so an unbounded wait would leave the user's circuit hanging
            // on a slot that a wedged render might never give back
            GateOccupation occupation = await SaturateTheGate();
            try
            {
                Assert.ThrowsAsync<TimeoutException>(async () => await GatedRenderReportBase.RunGated(
                    () => Task.FromResult<string?>("pdf"), TimeSpan.FromMilliseconds(kShortGateWaitMilliseconds)));
            }
            finally
            {
                await occupation.ReleaseAll();
            }
        }

        [Test]
        public async Task GatedPdfRenderDoesNotHandBackASlotItNeverGotWhenTheWaitTimedOut()
        {
            GateOccupation occupation = await SaturateTheGate();
            try
            {
                Assert.ThrowsAsync<TimeoutException>(async () => await GatedRenderReportBase.RunGated(
                    () => Task.FromResult<string?>("pdf"), TimeSpan.FromMilliseconds(kShortGateWaitMilliseconds)));
            }
            finally
            {
                await occupation.ReleaseAll();
            }

            // releasing a slot that was never taken would raise the gate's limit for the whole process
            int peak = await MeasurePeakConcurrentRenders();

            Assert.That(peak, Is.LessThanOrEqualTo(GatedRenderReportBase.MaxConcurrentRenders));
        }

        [Test]
        public async Task GatedPdfRenderWaitsForAFreeSlotWithinTheTimeout()
        {
            // a render that only has to queue must still go through rather than fail early
            GateOccupation occupation = await SaturateTheGate();
            Task<string?> queued = GatedRenderReportBase.RunGated(
                () => Task.FromResult<string?>("queued pdf"), TimeSpan.FromSeconds(GatedRenderReportBase.GateWaitSeconds));

            await occupation.ReleaseAll();

            Assert.That(await queued, Is.EqualTo("queued pdf"));
        }

        [Test]
        public void TheDefaultGateWaitIsFinite()
        {
            Assert.That(GatedRenderReportBase.GateWaitSeconds, Is.GreaterThan(0));
        }

        /// <summary>
        /// Builds a browser whose single page renders the given pdf bytes, so that the render path can
        /// be exercised without the hundreds of megabytes a real headless browser would cost.
        /// </summary>
        private static IBrowser SubstituteBrowser(out IPage page, byte[]? pdfData = null)
        {
            page = Substitute.For<IPage>();
            page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns(pdfData ?? kPdfData);
            IBrowser browser = Substitute.For<IBrowser>();
            browser.NewPageAsync().Returns(page);
            browser.CloseAsync().Returns(Task.CompletedTask);
            return browser;
        }

        [Test]
        public async Task RenderInLaunchedBrowserReturnsTheBase64EncodedPdf()
        {
            IBrowser browser = SubstituteBrowser(out IPage page);
            BrowserRenderReportBase report = new();

            string? pdf = await report.RenderIn(browser, kRenderedHtml, FWO.Report.PaperFormat.A4);

            Assert.That(pdf, Is.EqualTo(Convert.ToBase64String(kPdfData)));
            await page.Received(1).SetContentAsync(kRenderedHtml, Arg.Any<SetContentOptions>());
        }

        [Test]
        public async Task RenderInLaunchedBrowserGivesEveryPageOperationAFiniteDeadline()
        {
            // a render without deadlines can wedge while holding a render slot, which is what starves
            // every later export in this process
            IBrowser browser = SubstituteBrowser(out IPage page);
            BrowserRenderReportBase report = new();

            await report.RenderIn(browser, kRenderedHtml, FWO.Report.PaperFormat.A4);

            await page.Received(1).SetContentAsync(kRenderedHtml, Arg.Is<SetContentOptions>(options => options.Timeout > 0));
            await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(options => options.Timeout > 0));
        }

        [Test]
        public async Task RenderInLaunchedBrowserClosesAndDisposesTheBrowser()
        {
            IBrowser browser = SubstituteBrowser(out _);
            BrowserRenderReportBase report = new();

            await report.RenderIn(browser, kRenderedHtml, FWO.Report.PaperFormat.A4);

            await browser.Received(1).CloseAsync();
            browser.Received(1).Dispose();
        }

        [Test]
        public void RenderInLaunchedBrowserReportsAnUnknownPaperFormatAsUnsupported()
        {
            IBrowser browser = SubstituteBrowser(out _);
            BrowserRenderReportBase report = new();

            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await report.RenderIn(browser, kRenderedHtml, (FWO.Report.PaperFormat)kUnknownPaperFormat));
            browser.Received(1).Dispose();
        }

        [Test]
        public void RenderInLaunchedBrowserRethrowsRenderFailuresInsteadOfBlamingThePaperFormat()
        {
            // a timed out or crashed render used to surface as "this paper kind is not supported",
            // which sends whoever has to diagnose it looking in entirely the wrong place
            IBrowser browser = SubstituteBrowser(out IPage page);
            page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns<byte[]>(_ => throw new TimeoutException("render wedged"));
            BrowserRenderReportBase report = new();

            TimeoutException? failure = Assert.ThrowsAsync<TimeoutException>(async () =>
                await report.RenderIn(browser, kRenderedHtml, FWO.Report.PaperFormat.A4));

            Assert.That(failure?.Message, Is.EqualTo("render wedged"));
        }

        [Test]
        public void RenderInLaunchedBrowserStillShutsTheBrowserDownAfterAFailedRender()
        {
            // a browser left behind by a failed render keeps its memory until the service restarts
            IBrowser browser = SubstituteBrowser(out IPage page);
            page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns<byte[]>(_ => throw new TimeoutException("render wedged"));
            BrowserRenderReportBase report = new();

            Assert.ThrowsAsync<TimeoutException>(async () =>
                await report.RenderIn(browser, kRenderedHtml, FWO.Report.PaperFormat.A4));

            browser.Received(1).Dispose();
        }

        [Test]
        public async Task ClosingTheBrowserGracefullyAlsoDisposesIt()
        {
            IBrowser browser = SubstituteBrowser(out _);

            await BrowserRenderReportBase.CloseSafely(browser);

            await browser.Received(1).CloseAsync();
            browser.Received(1).Dispose();
        }

        [Test]
        public async Task ClosingTheBrowserDisposesItEvenWhenTheGracefulCloseFails()
        {
            IBrowser browser = SubstituteBrowser(out _);
            browser.CloseAsync().Returns(Task.FromException(new InvalidOperationException("already gone")));

            await BrowserRenderReportBase.CloseSafely(browser);

            browser.Received(1).Dispose();
        }

        [Test]
        public async Task ClosingTheBrowserGivesUpOnAHangingCloseAndKillsItInstead()
        {
            // a shutdown that never returns would hold on to the render slot for the rest of the
            // process lifetime, so the close has to be bounded and fall back to disposing the browser
            IBrowser browser = SubstituteBrowser(out _);
            using CancellationTokenSource neverCompletes = new();
            browser.CloseAsync().Returns(Task.Delay(Timeout.Infinite, neverCompletes.Token));

            await BrowserRenderReportBase.CloseSafely(browser, TimeSpan.FromMilliseconds(kShortCloseWaitMilliseconds));

            browser.Received(1).Dispose();
            await neverCompletes.CancelAsync();
        }

        [Test]
        public void ReleaseExportCacheResetsTheTemplateSoNoCopyOfTheBodyIsLeftBehind()
        {
            // the template is substituted in place, so a release that only cleared the export string
            // would still leave a full copy of the rendered body inside the builder. rendering a
            // different body after the release is what proves the template really went back to blank.
            FrameReportBase report = new();
            report.ExportToHtml();

            report.ReleaseExportCache();
            report.Body = "<p>second body</p>";
            string rebuilt = report.ExportToHtml();

            Assert.That(rebuilt, Does.Contain("second body"));
            Assert.That(rebuilt, Does.Not.Contain("frame body"));
        }
    }
}

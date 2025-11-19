using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using FWO.Report;
using FWO.Report.Data;
using FWO.Basics;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class HtmlToPdfTest
    {
        [Test]
        public async Task GeneratePdf()
        {
            bool isValidHtml = ReportBase.IsValidHTML(GlobalConst.TestPDFHtmlTemplate);
            ClassicAssert.IsTrue(isValidHtml);

            string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
            bool isGitHubActions = sudoUser is not null && sudoUser.Equals("runner", StringComparison.OrdinalIgnoreCase);

            if (ShouldSkipPdfTest(isGitHubActions, out string skipReason))
            {
                Log.WriteInfo("Test Log", skipReason);
                Assert.Ignore(skipReason);
            }

            if (File.Exists(GlobalConst.TestPDFFilePath))
                File.Delete(GlobalConst.TestPDFFilePath);

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

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

            if (allInstalledBrowsers is null || !allInstalledBrowsers.Any())
            {
                // this should only happen for testing on local systems where no suitable browser is installed
                Log.WriteInfo("Browser", $"Browser not found for current system - trying to download...");
                await browserFetcher.DownloadAsync();
                allInstalledBrowsers = browserFetcher.GetInstalledBrowsers().Where(_ => _.Browser == wantedBrowser);
            }

            foreach (InstalledBrowser instBrowser in allInstalledBrowsers)
            {
                Log.WriteInfo("Test Log", $"Found installed {instBrowser.Browser}({instBrowser.BuildId}) at: {instBrowser.GetExecutablePath()}");
            }

            string? newestBuildId = allInstalledBrowsers.Max(_ => _.BuildId);

            if (string.IsNullOrWhiteSpace(newestBuildId))
            {
                Log.WriteAlert("Test Log", $"Invalid build ID!");
                return;
            }

            InstalledBrowser? latestInstalledBrowser = allInstalledBrowsers.Single(_ => _.BuildId == newestBuildId);

            if (latestInstalledBrowser is null)
            {
                Log.WriteAlert("Test Log", $"Found no installed {wantedBrowser} instances with a valid build ID!");
                return;
            }

            Log.WriteInfo("Test Log", $"Selecting latest installed {wantedBrowser}({latestInstalledBrowser.BuildId}) at: {latestInstalledBrowser.GetExecutablePath()}");

            IBrowser? browser;

            try
            {
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    ExecutablePath = latestInstalledBrowser.GetExecutablePath(),
                    Headless = true,
                    DumpIO = isGitHubActions,
                    Args = isGitHubActions ? ["--database=/tmp", "--no-sandbox"] : []
                });
            }
            catch (Exception)
            {
                Log.WriteAlert("Test Log", $"Couldn't start {wantedBrowser} instance!");
                throw new Exception($"Couldn't start {wantedBrowser} instance!");
            }

            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A0);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A1);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A2);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A3);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A4);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A5);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.A6);

            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.Ledger);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.Legal);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.Letter);
            await TryCreatePDF(browser, PuppeteerSharp.Media.PaperFormat.Tabloid);

            try
            {
                await browser.CloseAsync();
                browser.Dispose();
            }
            catch (Exception)
            {
                throw new Exception("Couldn't close browser instance!");
            }
        }

        [Test]
        public void TryCreateToC()
        {
            bool isValidHtml = ReportBase.IsValidHTML(GlobalConst.TestPDFHtmlTemplate);
            ClassicAssert.IsTrue(isValidHtml);

            List<ToCHeader>? tocContent = ReportBase.CreateTOCContent(GlobalConst.TestPDFHtmlTemplate);

            ClassicAssert.AreEqual(tocContent.Count, 2);
            ClassicAssert.AreEqual(tocContent[0].Title, "test");
            ClassicAssert.AreEqual(tocContent[1].Title, "test mit puppteer");
        }

        private async Task TryCreatePDF(IBrowser browser, PuppeteerSharp.Media.PaperFormat paperFormat)
        {
            if (browser.IsClosed || !browser.IsConnected || browser.Process == null)
            {
                Log.WriteAlert("Test Log", $"Browser: {browser.GetVersionAsync()} is not started or closed due to errors!");
                return;
            }

            Log.WriteInfo("Test Log", $"Test creating PDF {paperFormat}");

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(GlobalConst.TestPDFHtmlTemplate);

                PdfOptions pdfOptions = new() { Outline = true, DisplayHeaderFooter = false, Landscape = true, PrintBackground = true, Format = paperFormat, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                byte[]? pdfData = await page.PdfDataAsync(pdfOptions);
                await File.WriteAllBytesAsync(GlobalConst.TestPDFFilePath, pdfData);

                Assert.That(GlobalConst.TestPDFFilePath, Does.Exist);
                FileAssert.Exists(GlobalConst.TestPDFFilePath);
                ClassicAssert.AreEqual(new FileInfo(GlobalConst.TestPDFFilePath).Length, pdfData.Length);
            }
            catch (Exception ex)
            {
                Log.WriteAlert("Test Log", $"{ex.Message}");
                return;
            }
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            if (File.Exists(GlobalConst.TestPDFFilePath))
            {
                File.Delete(GlobalConst.TestPDFFilePath);
            }
        }

        private static bool ShouldSkipPdfTest(bool isGitHubActions, out string reason)
        {
            if (isGitHubActions)
            {
                reason = "PDF Test skipping: Test is running on Github actions.";
                return true;
            }

            string? skipEnv = Environment.GetEnvironmentVariable("FW_SKIP_PDF_TEST");
            if (!string.IsNullOrWhiteSpace(skipEnv) && IsTruthy(skipEnv))
            {
                reason = "PDF Test skipping: FW_SKIP_PDF_TEST requested skip.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static bool IsTruthy(string? value)
        {
            if (value is null)
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}

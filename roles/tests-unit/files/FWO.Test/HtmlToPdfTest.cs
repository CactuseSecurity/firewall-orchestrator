using AngleSharp.Dom;
using FWO.Basics;
using FWO.Logging;
using FWO.Report;
using FWO.Report.Data;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using PuppeteerSharp.Media;
using System.Diagnostics;

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

            foreach(System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                Console.WriteLine($"::warning::{entry.Key} = {entry.Value}");
                Debug.WriteLine($"::warning::{entry.Key} = {entry.Value}");
                Console.WriteLine($"{entry.Key} = {entry.Value}");
            }

            string? githubActions = Environment.GetEnvironmentVariable("RUNNING_ON_GITHUB_ACTIONS");
            Log.WriteInfo("Test Log", $"{nameof(githubActions)} = {githubActions}");
            Console.WriteLine($"{nameof(githubActions)} = {githubActions}");
            Debug.WriteLine($"{nameof(githubActions)} = {githubActions}");
            Console.WriteLine($"{nameof(githubActions)} = {githubActions}");

            string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
            string? runnerUser = Environment.GetEnvironmentVariable("RUNNER_USER");

            bool isGitHubActions = sudoUser is not null && runnerUser is not null && sudoUser.Equals("runner") && runnerUser.Equals("runner");

            if(isGitHubActions)
            {
                Log.WriteInfo("Test Log", $"PDF Test skipping: Test is running on Github actions.");
                return;
            }

            if(File.Exists(GlobalConst.TestPDFFilePath))
                File.Delete(GlobalConst.TestPDFFilePath);

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            string path = "";
            Platform platform = Platform.Unknown;
            const SupportedBrowser wantedBrowser = SupportedBrowser.Chrome;

            switch(os.Platform)
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

            if(allInstalledBrowsers is null || !allInstalledBrowsers.Any())
            {
                Log.WriteAlert("Test Log", $"Found no installed {wantedBrowser} instances!");
                return;
            }

            foreach(InstalledBrowser instBrowser in allInstalledBrowsers)
            {
                Log.WriteInfo("Test Log", $"Found installed {instBrowser.Browser}({instBrowser.BuildId}) at: {instBrowser.GetExecutablePath()}");
            }

            string? newestBuildId = allInstalledBrowsers.Max(_ => _.BuildId);

            if(string.IsNullOrWhiteSpace(newestBuildId))
            {
                Log.WriteAlert("Test Log", $"Invalid build ID!");
                return;
            }

            InstalledBrowser? latestInstalledBrowser = allInstalledBrowsers.Single(_ => _.BuildId == newestBuildId);

            if(latestInstalledBrowser is null)
            {
                Log.WriteAlert("Test Log", $"Found no installed {wantedBrowser} instances with a valid build ID!");
                return;
            }

            Log.WriteInfo("Test Log", $"Selecting latest installed {wantedBrowser}({latestInstalledBrowser}) at: {latestInstalledBrowser.GetExecutablePath()}");

            IBrowser? browser;

            try
            {
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    ExecutablePath = latestInstalledBrowser.GetExecutablePath(),
                    Headless = true,
                    DumpIO = isGitHubActions, // Enables debug logs
                    Args = new[] { "--database=/tmp", "--no-sandbox" }
                });
            }
            catch(Exception)
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
            }
            catch(Exception)
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
            if(browser.IsClosed || !browser.IsConnected || browser.Process == null)
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
            catch(Exception ex)
            {
                Log.WriteAlert("Test Log", $"{ex.Message}");
                return;
            }
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            if(File.Exists(GlobalConst.TestPDFFilePath))
            {
                File.Delete(GlobalConst.TestPDFFilePath);
            }
        }
    }
}

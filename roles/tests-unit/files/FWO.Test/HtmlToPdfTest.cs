using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using FWO.Report;
using FWO.Report.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class HtmlToPdfTest
    {
        private const string FilePath = "pdffile.pdf";
        private const string Html = "<html><body><h1>test</h1><h2>test mit puppteer</h2></body></html>";
        private const string ChromeBinPathLinux = "/usr/local/bin";


        [Test]
        public async Task GeneratePdf()
        {
            bool isValidHtml = ReportBase.IsValidHTML(Html);
            ClassicAssert.IsTrue(isValidHtml);

            string? isGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
            string? isGitHubActions2 = Environment.GetEnvironmentVariable("RUNNING_ON_GITHUB_ACTIONS");
            string? isGitHubActions3 = Environment.GetEnvironmentVariable("RUNNING_ON_GITHUB");

            Log.WriteInfo("Test Log", $"GITHUB_ACTIONS? {isGitHubActions}");
            Log.WriteInfo("Test Log", $"RUNNING_ON_GITHUB_ACTIONS? {isGitHubActions2}");
            Log.WriteInfo("Test Log", $"RUNNING_ON_GITHUB? {isGitHubActions3}");
            Log.WriteInfo("Test Log", $"GITHUB_ENV {Environment.GetEnvironmentVariable("GITHUB_ENV")}");

            if (!string.IsNullOrEmpty(isGitHubActions))
            {
                return;
            }

            if (File.Exists(FilePath))
                File.Delete(FilePath);

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
                    path = ChromeBinPathLinux;
                    platform = Platform.Linux;
                    break;
                default:
                    break;
            }

            BrowserFetcher browserFetcher = new(new BrowserFetcherOptions() { Platform = platform, Browser = wantedBrowser, Path = path });

            InstalledBrowser? installedBrowser = browserFetcher.GetInstalledBrowsers()
                      .FirstOrDefault(_ => _.Platform == platform && _.Browser == wantedBrowser);

            if (installedBrowser == null)
            {
                Log.WriteWarning("Test Log", $"Browser {wantedBrowser} is not installed! Trying to download latest version...");
                installedBrowser = await browserFetcher.DownloadAsync(BrowserTag.Latest);
            }

            Log.WriteInfo("Test Log", $"Browser Path: {installedBrowser.GetExecutablePath()}");

            using IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = installedBrowser.GetExecutablePath(),
                Headless = true,
                DumpIO = isGitHubActions != null ? true : false, // Enables debug logs
                Args = new[] { "--database=/tmp", "--no-sandbox" }
            });

            try
            {
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
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                await browser.CloseAsync();
            }
        }

        [Test]
        public void TryCreateToC()
        {
            bool isValidHtml = ReportBase.IsValidHTML(Html);
            ClassicAssert.IsTrue(isValidHtml);

            List<ToCHeader>? tocContent = ReportBase.CreateTOCContent(Html);

            ClassicAssert.AreEqual(tocContent.Count, 2);
            ClassicAssert.AreEqual(tocContent[0].Title, "test");
            ClassicAssert.AreEqual(tocContent[1].Title, "test mit puppteer");
        }

        private async Task TryCreatePDF(IBrowser browser, PuppeteerSharp.Media.PaperFormat paperFormat)
        {
            Log.WriteInfo("Test Log", $"Test creating PDF {paperFormat}");

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(Html);

                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = paperFormat, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                using Stream? pdfData = await page.PdfStreamAsync(pdfOptions);

                byte[]? pdfWithToCData = ReportBase.AddToCBookmarksToPDF(pdfData, Html);

                await File.WriteAllBytesAsync(FilePath, pdfWithToCData);

                Assert.That(FilePath, Does.Exist);
                FileAssert.Exists(FilePath);
                ClassicAssert.AreEqual(new FileInfo(FilePath).Length, pdfWithToCData.Length);
            }
            catch (Exception)
            {
                throw new Exception("This paper kind is currently not supported. Please choose another one or \"Custom\" for a custom size.");
            }
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}

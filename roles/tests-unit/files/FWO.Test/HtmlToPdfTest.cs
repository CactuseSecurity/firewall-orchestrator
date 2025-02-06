using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using HtmlAgilityPack;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class HtmlToPdfTest
    {
        private const string FilePath = "pdffile.pdf";
        private const string Html = "<html> <body> <h1>test<h1> test mit puppteer </body> </html>";
        private const string ChromeBinPathLinux = "/usr/local/bin";

        [Test]
        public async Task GeneratePdf()
        {
            Assert.That(IsValidHTML(Html));

            string? isGitHubActions = Environment.GetEnvironmentVariable("RUNNING_ON_GITHUB_ACTIONS");


            if (File.Exists(FilePath))
                File.Delete(FilePath);

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            string path = "";
            BrowserFetcher? browserFetcher = default;

            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                    browserFetcher = new();
                    break;
                case PlatformID.Unix:
                    path = ChromeBinPathLinux;
                    browserFetcher = new(new BrowserFetcherOptions { Path = path, Platform = Platform.Linux, Browser = SupportedBrowser.Chrome });
                    break;
                default:
                    break;
            }

            InstalledBrowser? brw = browserFetcher.GetInstalledBrowsers().FirstOrDefault() ?? await browserFetcher.DownloadAsync(BrowserTag.Latest);

            Log.WriteInfo("Test Log", $"Browser: {brw.GetExecutablePath()}");

            using IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = brw.GetExecutablePath(),
                Headless = false,
                Browser = SupportedBrowser.Chrome
            });

            try
            {
                await TryCreatePDF(browser, PaperFormat.A0);
                await TryCreatePDF(browser, PaperFormat.A1);
                await TryCreatePDF(browser, PaperFormat.A2);
                await TryCreatePDF(browser, PaperFormat.A3);
                await TryCreatePDF(browser, PaperFormat.A4);
                await TryCreatePDF(browser, PaperFormat.A5);
                await TryCreatePDF(browser, PaperFormat.A6);

                await TryCreatePDF(browser, PaperFormat.Ledger);
                await TryCreatePDF(browser, PaperFormat.Legal);
                await TryCreatePDF(browser, PaperFormat.Letter);
                await TryCreatePDF(browser, PaperFormat.Tabloid);
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

        private static bool IsValidHTML(string html)
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

        private async Task TryCreatePDF(IBrowser browser, PaperFormat paperFormat)
        {
            Log.WriteInfo("Test Log", $"Test creating PDF {paperFormat}");

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(Html);

                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = paperFormat, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                byte[] pdfData = await page.PdfDataAsync(pdfOptions);

                await File.WriteAllBytesAsync(FilePath, pdfData);

                Assert.That(FilePath, Does.Exist);
                FileAssert.Exists(FilePath);
                ClassicAssert.AreEqual(new FileInfo(FilePath).Length, pdfData.Length);
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

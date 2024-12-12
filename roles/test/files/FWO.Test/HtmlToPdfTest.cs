using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class HtmlToPdfTest
    {
        private const string FilePath = "pdffile.pdf";

        [Test]
        [Parallelizable]
        public async Task GeneratePdf()
        {
            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            BrowserFetcher? browserFetcher;

            switch (os.Platform)
            {
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.WinCE:
                case PlatformID.Xbox:
                case PlatformID.MacOSX:
                case PlatformID.Other:
                    browserFetcher = new();
                    Log.WriteInfo("Test Log", $"Downloading headless Browser...");
                    break;
                case PlatformID.Unix:
                    browserFetcher = new();
                    Log.WriteInfo("Test Log", $"Downloading headless Browser {nameof(SupportedBrowser.ChromeHeadlessShell)}");
                    break;
                default:
                    browserFetcher = new();
                    Log.WriteInfo("Test Log", $"Downloading headless Browser...");
                    break;
            }

            InstalledBrowser? brw = await browserFetcher.DownloadAsync();

            if (brw.PermissionsFixed == false)
            {
                throw new Exception("Sandbox permissions were not applied. You need to run your application as an administrator.");
            }

            Log.WriteInfo("Test Log", "starting PDF generation");
            // HTML
            string html = "<html> <body> <h1>test<h1> test </body> </html>";

            if (File.Exists(FilePath))
                File.Delete(FilePath);

            IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
               // Browser = browserFetcher.Browser,
                Args = ["--no-sandbox"] //, "--disable-setuid-sandbox"
            });

            try
            {
                IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(html);

                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = PaperFormat.A4, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };

                Log.WriteInfo("Test Log", "Writing data to pdf");
                await page.PdfAsync(FilePath);               
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                await browser.CloseAsync();
            }

            Assert.That(FilePath, Does.Exist);
            ClassicAssert.Greater(new FileInfo(FilePath).Length, 5000);
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            File.Delete(FilePath);
        }
    }
}

using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace FWO.Test
{
    [TestFixture]
    internal class HtmlToPdfTest
    {
        private const string FilePath = "pdffile.pdf";
        private const string Html = "<html> <body> <h1>test<h1> test </body> </html>";
        private const string ChromeBinPathWin = "C:\\chrome";
        private const string ChromeBinPathLinux = "/tmp/fworch";

        [Test]
        public async Task GeneratePdf()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            string path;

            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                    path = ChromeBinPathWin;                    
                    break;
                case PlatformID.Unix:
                    path = ChromeBinPathLinux;
                    break;
                default:
                    return;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            BrowserFetcher? browserFetcher = new(new BrowserFetcherOptions { Path = path });
            Log.WriteInfo("Test Log", $"Downloading chrome to: {path}");

            InstalledBrowser? brw = await browserFetcher.DownloadAsync();

            Log.WriteInfo("Test Log", $"Browser Path: {brw.GetExecutablePath()}");

            if (brw.PermissionsFixed == false)
            {
                throw new Exception("Sandbox permissions were not applied. You need to run your application as an administrator.");
            }

            CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            await TryCreatePDF(brw, cancellationTokenSource.Token);

            Assert.That(FilePath, Does.Exist);
            ClassicAssert.Greater(new FileInfo(FilePath).Length, 5000);
        }

        private async Task TryCreatePDF(InstalledBrowser brw, CancellationToken ct)
        {
            Log.WriteInfo("Test Log", "Starting Browser...");
            IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = brw.GetExecutablePath(),
                Headless = true,
                HeadlessMode = HeadlessMode.Shell,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"] //, "--disable-setuid-sandbox"
            });

            Log.WriteInfo("Test Log", "Browser started...");

            IPage page = await browser.NewPageAsync();
            await page.BringToFrontAsync();
            await page.GoToAsync("https://google.com");
            Log.WriteInfo("Test Log", "Browser navigated...");

            try
            {
                Log.WriteInfo("Test Log", "Browser new page...");
                page = await browser.NewPageAsync();

                Log.WriteInfo("Test Log", "Browser set html content...");
                await page.SetContentAsync(Html);

                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = PaperFormat.A4, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };

                Log.WriteInfo("Test Log", $"Writing data to pdf at: {Path.GetFullPath(FilePath)})");
                await page.PdfAsync(FilePath);
                Log.WriteInfo("Test Log", "PDF created...");
            }
            catch (Exception ex)
            {
                Log.WriteError(ex.ToString());
                Log.WriteError(ex.InnerException!.ToString());
                throw new Exception(ex.Message);
            }
            finally
            {
                await browser.CloseAsync();
            }
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            File.Delete(FilePath);
        }
    }
}

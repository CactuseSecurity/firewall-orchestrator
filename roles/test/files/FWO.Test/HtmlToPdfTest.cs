using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using FWO.Ui.Pages.Reporting;
using FWO.Report;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;

namespace FWO.Test
{
    [TestFixture]
    internal class HtmlToPdfTest
    {
        private const string FilePath = "pdffile.pdf";
        private const string Html = "<html> <body> <h1>test<h1> test mit puppteer </body> </html>";
        private const string ChromeBinPathLinux = "/usr/local/bin";

        [Test]
        public async Task GeneratePdf()
        {
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

            InstalledBrowser? brw = await browserFetcher.DownloadAsync(BrowserTag.Stable);

            using IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = brw.GetExecutablePath(),
                Headless = true
            });

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(Html);

                PuppeteerSharp.Media.PaperFormat? pupformat = PuppeteerSharp.Media.PaperFormat.A4;

                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = pupformat, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                byte[] pdfData = await page.PdfDataAsync(pdfOptions);

                string html = Convert.ToBase64String(pdfData);
                File.WriteAllText(FilePath, html);
            }
            catch (Exception)
            {
                throw new Exception("This paper kind is currently not supported. Please choose another one or \"Custom\" for a custom size.");
            }
            finally
            {
                await browser.CloseAsync();
            }

            Assert.That(FilePath, Does.Exist);
            ClassicAssert.Greater(new FileInfo(FilePath).Length, 5000);
        }

        //private async Task TryCreatePDF(InstalledBrowser brw, CancellationToken ct)
        //{
        //    Log.WriteInfo("Test Log", "Starting Browser...");
        //    IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
        //    {
        //        ExecutablePath = brw.GetExecutablePath(),
        //        Headless = false,
        //        //HeadlessMode = HeadlessMode.True,
        //        Args = ["--no-sandbox", "--disable-setuid-sandbox"] //, "--disable-setuid-sandbox"
        //    });

        //    Log.WriteInfo("Test Log", "Browser started...");

        //    IPage page = await browser.NewPageAsync();
        //    await page.BringToFrontAsync();
        //    await page.GoToAsync("https://google.com");
        //    Log.WriteInfo("Test Log", "Browser navigated...");

        //    try
        //    {
        //        Log.WriteInfo("Test Log", "Browser new page...");
        //        page = await browser.NewPageAsync();

        //        Log.WriteInfo("Test Log", "Browser set html content...");
        //        await page.SetContentAsync(Html);

        //        PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = PaperFormat.A4, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };

        //        Log.WriteInfo("Test Log", $"Writing data to pdf at: {Path.GetFullPath(FilePath)})");
        //        await page.PdfAsync(FilePath);
        //        Log.WriteInfo("Test Log", "PDF created...");
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.WriteError(ex.ToString());
        //        Log.WriteError(ex.InnerException!.ToString());
        //        throw new Exception(ex.Message);
        //    }
        //    finally
        //    {
        //        await browser.CloseAsync();
        //    }
        //}

        [OneTimeTearDown]
        public void OnFinished()
        {
            //File.Delete(FilePath);
        }
    }
}

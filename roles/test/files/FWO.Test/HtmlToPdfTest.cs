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
           // Log.WriteInfo("Test Log", "Removing installed browsers...");
            BrowserFetcher? browserFetcher = new();

            //foreach (PuppeteerSharp.BrowserData.InstalledBrowser installedBrowser in browserFetcher.GetInstalledBrowsers())
            //{
              
            //    try
            //    {
            //        browserFetcher.Uninstall(installedBrowser.BuildId);
            //    }
            //    catch (Exception ex)
            //    {
            //        throw new Exception("Browser couldn't be uninstalled. Try rebooting the system, the browser may be in use. ");
            //    }
                
            //}

            Log.WriteInfo("Test Log", "Downloading headless Browser...");
            InstalledBrowser? brw = await browserFetcher.DownloadAsync();

            if (brw.PermissionsFixed == false)
            {
                throw new Exception("Sandbox permissions were not applied. You need to run your application as an administrator.");
            }

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            Log.WriteInfo("Test Log", "starting PDF generation");
            // HTML
            string html = "<html> <body> <h1>test<h1> test </body> </html>";
            
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                //Headless = true,
                // Browser = SupportedBrowser.ChromeHeadlessShell,
                Args = ["--no-sandbox"] //, "--disable-setuid-sandbox"
            });

            try
            {                
                IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(html);
                
                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = PaperFormat.A4, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                
                await page.PdfAsync(FilePath);

                Log.WriteInfo("Test Log", "Writing data to pdf");
               // File.WriteAllBytes(FilePath, pdfData);
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

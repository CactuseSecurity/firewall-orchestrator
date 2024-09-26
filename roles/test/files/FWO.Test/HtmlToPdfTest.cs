using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class HtmlToPdfTest
    {

        [Test]
        [Parallelizable]
        public async Task GeneratePdf()
        {
            Log.WriteInfo("Test Log", "Downloading headless Browser...");

            BrowserFetcher? browserFetcher = new();
            await browserFetcher.DownloadAsync();

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            Log.WriteInfo("Test Log", "starting PDF generation");
            // HTML
            string html = "<html> <body> <h1>test<h1> test </body> </html>";

            string filePath = "pdffile.pdf";

            if (File.Exists(filePath))
                File.Delete(filePath);

            using IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(html);
                
                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = PaperFormat.A4, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                byte[] pdfData = await page.PdfDataAsync(pdfOptions);

                File.WriteAllBytes(filePath, pdfData);
            }
            catch (Exception)
            {
                throw new Exception("This paper kind is currently not supported. Please choose another one or \"Custom\" for a custom size.");
            }
            finally
            {
                await browser.CloseAsync();
            }

            Assert.That(filePath, Does.Exist);
            ClassicAssert.Greater(new FileInfo(filePath).Length, 5000);
        }


        [OneTimeTearDown]
        public void OnFinished()
        {
            File.Delete("test.pdf");
            File.Delete("chrome-linux64.zip");
            File.Delete("chrome-win32.zip");
            File.Delete("chrome-win64.zip");
        }
    }
}

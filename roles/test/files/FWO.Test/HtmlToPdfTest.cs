using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using System.IO.Compression;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.FileSystemGlobbing;

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

            OperatingSystem? os = Environment.OSVersion;

            Log.WriteInfo("Test Log", $"OS: {os}");

            Log.WriteInfo("Test Log", "starting PDF generation");
            // HTML
            string html = "<html> <body> <h1>test<h1> test </body> </html>";

            string filePath = "pdffile.pdf";
            IBrowser? browser = default;

            try
            {
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {                 
                    Headless = true
                });

                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(html);

                PdfOptions pdfOptions = new() { DisplayHeaderFooter = true, Landscape = true, PrintBackground = true, Format = PuppeteerSharp.Media.PaperFormat.A4, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                await page.PdfAsync(filePath);

            }
            catch (Exception)
            {
                throw new Exception("This paper kind is currently not supported. Please choose another one or \"Custom\" for a custom size.");
            }
            finally
            {
                if (browser is not null)
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

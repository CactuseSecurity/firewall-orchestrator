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
            Log.WriteInfo("Test Log", "starting PDF generation");
            // HTML
            string html = "<html> <body> <h1>test<h1> test </body> </html>";

            using IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });

            string filePath = "pdffile.pdf";
            
            try
            {
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
                await browser.CloseAsync();
            }

            Assert.That(filePath, Does.Exist);
            ClassicAssert.Greater(new FileInfo(filePath).Length, 5000);
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            File.Delete("test.pdf");
        }
    }
}

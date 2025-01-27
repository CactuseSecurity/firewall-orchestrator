using NUnit.Framework;
using FWO.Logging;
using HtmlAgilityPack;
using PeachPDF.PdfSharpCore;
using NUnit.Framework.Legacy;
using PeachPDF;

namespace FWO.Test
{
    [TestFixture]
    internal class HtmlToPdfTest
    {
        private const string FilePath = "pdffile.pdf";
        private const string Html = "<html><body><h1>test<h1>test</body></html>";

        [Test]
        public async Task GeneratePdf()
        {
            Assert.That(IsValidHTML(Html));

            try
            {
                await TryCreatePDF(PageSize.A0);
                await TryCreatePDF(PageSize.A1);
                await TryCreatePDF(PageSize.A2);
                await TryCreatePDF(PageSize.A3);
                await TryCreatePDF(PageSize.A4);
                await TryCreatePDF(PageSize.A5);
                await TryCreatePDF(PageSize.A6);
                await TryCreatePDF(PageSize.B0);
                await TryCreatePDF(PageSize.B1);
                await TryCreatePDF(PageSize.B2);
                await TryCreatePDF(PageSize.B3);
                await TryCreatePDF(PageSize.B4);
                await TryCreatePDF(PageSize.B5);
                await TryCreatePDF(PageSize.Letter);
                await TryCreatePDF(PageSize.Tabloid);
            }
            catch (Exception)
            {
                throw;
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

        private static async Task TryCreatePDF(PageSize pageSize)
        {
            Log.WriteInfo("Test Log", $"Test creating PDF {pageSize}");

            try
            {
                PdfGenerateConfig pdfConfig = new()
                {
                    PageSize = pageSize,
                    PageOrientation = PageOrientation.Landscape
                };

                PdfGenerator generator = new();

                var document = await generator.GeneratePdf(Html, pdfConfig);
                document.Save(FilePath);

                Assert.That(FilePath, Does.Exist);
                FileAssert.Exists(FilePath);

                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception)
            {
                throw;
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

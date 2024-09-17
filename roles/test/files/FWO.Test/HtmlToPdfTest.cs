using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using PuppeteerSharp.Media;
using PuppeteerSharp;
using System.IO.Compression;
using System.Diagnostics;

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

            if (os.Platform == PlatformID.Win32NT)
            {
                await DownloadForWindows();
            }
            else if(os.Platform == PlatformID.Unix)
            {
                await DownloadForUnixTestsystem();
            }

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

        private async Task DownloadForWindows()
        {
            BrowserFetcher? browserFetcher = new();
            await browserFetcher.DownloadAsync();
        }

        private async Task DownloadForUnixTestsystem()
        {
            string uri = "https://storage.googleapis.com/chrome-for-testing-public/128.0.6613.119/linux64/chrome-linux64.zip";
            string outputPath = "chrome-linux64.zip";

            if (!Uri.TryCreate(uri, UriKind.Absolute, out var uriResult))
                throw new InvalidOperationException("URI is invalid.");

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using HttpClient httpClient = new();

            using HttpResponseMessage response = await httpClient.GetAsync(uriResult, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using FileStream? fileStream = File.Create(outputPath);
            using Stream? httpStream = await response.Content.ReadAsStreamAsync();
            await httpStream.CopyToAsync(fileStream);
            fileStream.Close();
            await fileStream.DisposeAsync();

            string path = Path.Combine(GlobalConstants.GlobalConst.FworchUnixBrowserBinPath, outputPath.Replace(".zip", ""));
           
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            if (File.Exists(path))
                File.Delete(path);

            try
            {
                ZipFile.ExtractToDirectory(outputPath,  path);
            }
            catch (Exception ex)
            {
                Log.WriteInfo("Test Log", ex.ToString());               
                throw;
            }
           
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

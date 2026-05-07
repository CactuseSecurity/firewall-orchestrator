using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using Microsoft.AspNetCore.Http;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using PuppeteerSharp.Media;

namespace FWO.Services
{
    public class NotificationEmailLayoutContent
    {
        public string PlainText { get; set; } = "";
        public string Html { get; set; } = "";
        public string Csv { get; set; } = "";
        public string Json { get; set; } = "";

        public string BodyForLayout(NotificationLayout layout)
        {
            return layout == NotificationLayout.HtmlInBody ? Html : PlainText;
        }
    }

    public static class NotificationEmailLayoutHelper
    {
        public static string BuildBody(FwoNotification notification, string? content)
        {
            string notificationBody = notification.EmailBody ?? "";
            string resolvedContent = ResolveContent(notification.Layout, content);
            if (notificationBody.Contains(Placeholder.CONTENT))
            {
                return notificationBody.Replace(Placeholder.CONTENT, resolvedContent);
            }

            return string.IsNullOrEmpty(resolvedContent) ? notificationBody : resolvedContent;
        }

        public static string BuildBody(FwoNotification notification, NotificationEmailLayoutContent? content)
        {
            string notificationBody = notification.EmailBody ?? "";
            if (content == null || !NotificationLayoutGroups.WithoutAttachments().Contains(notification.Layout))
            {
                return notificationBody.Replace(Placeholder.CONTENT, "");
            }

            string resolvedContent = content.BodyForLayout(notification.Layout);
            if (notificationBody.Contains(Placeholder.CONTENT))
            {
                return notificationBody.Replace(Placeholder.CONTENT, resolvedContent);
            }

            return $"{notificationBody}{resolvedContent}";
        }

        public static async Task<FormFile?> BuildAttachment(NotificationLayout layout, NotificationEmailLayoutContent? content, string subject)
        {
            if (content == null)
            {
                return null;
            }

            return layout switch
            {
                NotificationLayout.PdfAsAttachment => EmailHelper.CreateAttachment(await ToPdf(content.Html), GlobalConst.kPdf, subject),
                NotificationLayout.HtmlAsAttachment => EmailHelper.CreateAttachment(content.Html, GlobalConst.kHtml, subject),
                NotificationLayout.JsonAsAttachment => EmailHelper.CreateAttachment(content.Json, GlobalConst.kJson, subject),
                NotificationLayout.CsvAsAttachment => EmailHelper.CreateAttachment(content.Csv, GlobalConst.kCsv, subject),
                _ => null
            };
        }

        public static async Task<FormFile?> BuildAttachment(NotificationLayout layout, string subject, Func<string> html, Func<string> json,
            Func<string> csv, Func<string, Task<string?>> pdf)
        {
            return layout switch
            {
                NotificationLayout.PdfAsAttachment => EmailHelper.CreateAttachment(await pdf(html()), GlobalConst.kPdf, subject),
                NotificationLayout.HtmlAsAttachment => EmailHelper.CreateAttachment(html(), GlobalConst.kHtml, subject),
                NotificationLayout.JsonAsAttachment => EmailHelper.CreateAttachment(json(), GlobalConst.kJson, subject),
                NotificationLayout.CsvAsAttachment => EmailHelper.CreateAttachment(csv(), GlobalConst.kCsv, subject),
                _ => null
            };
        }

        private static string ResolveContent(NotificationLayout layout, string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "";
            }

            return layout == NotificationLayout.HtmlInBody
                ? content.Replace("\r\n", "<br>").Replace("\n", "<br>")
                : content;
        }

        private static async Task<string?> ToPdf(string html)
        {
            OperatingSystem os = Environment.OSVersion;
            string path = os.Platform == PlatformID.Unix ? GlobalConst.ChromeBinPathLinux : "";
            Platform platform = os.Platform == PlatformID.Win32NT ? Platform.Win32 : Platform.Linux;
            BrowserFetcher browserFetcher = new(new BrowserFetcherOptions() { Platform = platform, Browser = SupportedBrowser.Chrome, Path = path });
            IEnumerable<InstalledBrowser> browsers = browserFetcher.GetInstalledBrowsers().Where(browser => browser.Browser == SupportedBrowser.Chrome);
            if (!browsers.Any())
            {
                if (os.Platform != PlatformID.Win32NT)
                {
                    throw new EnvironmentException("Found no installed Chrome instances.");
                }
                await browserFetcher.DownloadAsync();
                browsers = browserFetcher.GetInstalledBrowsers().Where(browser => browser.Browser == SupportedBrowser.Chrome);
            }

            InstalledBrowser browserInfo = browsers.OrderBy(browser => browser.BuildId).Last();
            await using IBrowser browser = await Puppeteer.LaunchAsync(new LaunchOptions { ExecutablePath = browserInfo.GetExecutablePath(), Headless = true });
            using IPage page = await browser.NewPageAsync();
            await page.SetContentAsync(html);
            PdfOptions options = new() { DisplayHeaderFooter = false, Landscape = true, PrintBackground = true, Format = PaperFormat.A4 };
            return Convert.ToBase64String(await page.PdfDataAsync(options));
        }
    }
}

using FWO.Basics;
using FWO.Data;
using FWO.Services.HeadlessBrowser;
using Microsoft.AspNetCore.Http;
using PuppeteerSharp;
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
        private const int kBrowserLaunchTimeoutMs = 60_000;
        private const int kBrowserProtocolTimeoutMs = 180_000;

        public static string BuildBody(FwoNotification notification, string? content)
        {
            string notificationBody = notification.EmailBody ?? "";
            string resolvedContent = ResolveContent(notification.Layout, content);
            if (notificationBody.Contains(Placeholder.CONTENT))
            {
                return notificationBody.Replace(Placeholder.CONTENT, resolvedContent);
            }

            return string.IsNullOrEmpty(resolvedContent) ? notificationBody : $"{notificationBody}{resolvedContent}";
        }

        public static string BuildBody(FwoNotification notification, NotificationEmailLayoutContent? content)
        {
            string notificationBody = notification.EmailBody ?? "";
            if (content == null || !NotificationLayoutGroups.WithoutAttachments().Contains(notification.Layout))
            {
                return notificationBody.Replace(Placeholder.CONTENT, "");
            }

            string resolvedContent = content.BodyForLayout(notification.Layout);
            if (notification.Layout == NotificationLayout.HtmlInBody && !string.IsNullOrWhiteSpace(resolvedContent))
            {
                resolvedContent = NotificationTableBodyBuilder.HtmlTableStyleBlock + resolvedContent;
            }
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
                NotificationLayout.PdfAsAttachment => EmailHelper.CreateAttachment(await ToPdf(NotificationTableBodyBuilder.BuildHtmlDocument(content.Html)), GlobalConst.kPdf, subject),
                NotificationLayout.HtmlAsAttachment => EmailHelper.CreateAttachment(NotificationTableBodyBuilder.BuildHtmlDocument(content.Html), GlobalConst.kHtml, subject),
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
            string executablePath = await HeadlessBrowserLauncher.ResolveExecutablePath();
            await using IBrowser browser = await HeadlessBrowserLauncher.LaunchAsync(executablePath, kBrowserLaunchTimeoutMs, kBrowserProtocolTimeoutMs);
            using IPage page = await browser.NewPageAsync();
            await page.SetContentAsync(html);
            PdfOptions options = new() { DisplayHeaderFooter = false, Landscape = true, PrintBackground = true, Format = PaperFormat.A4 };
            return Convert.ToBase64String(await page.PdfDataAsync(options));
        }
    }
}

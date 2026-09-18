using FWO.Logging;
using PuppeteerSharp;

namespace FWO.Services
{
    /// <summary>
    /// Locks down the headless browser that renders report and notification html to pdf.
    /// The html handed to that browser is built from stored values - object, service, device and owner
    /// names, reasons, labels - so it has to be assumed to contain markup that was never meant to be
    /// there. A page that fetches subresources turns such markup into outbound requests issued by the
    /// server itself, which reach hosts the requesting user cannot reach and can carry data out in the
    /// request line. This class removes that capability at the renderer rather than at each of the many
    /// places that build the html: scripts do not run, and every request except the document the render
    /// starts from is aborted before it leaves the process.
    /// Each render already runs in a browser of its own that is closed afterwards, so no context, cache
    /// or cookie is shared between two renders.
    /// </summary>
    public static class PdfRenderSecurity
    {
        /// <summary>
        /// Schemes a render may load. Both are resolved inside the browser and never reach the network:
        /// "about" is the blank document the content is written into, "data" is content inlined in the
        /// url itself.
        /// </summary>
        private static readonly List<string> kAllowedRenderSchemes = ["about", "data"];

        /// <summary>
        /// Command line of the render browser. It turns off every background channel Chrome opens on its
        /// own, and makes host name resolution fail for every name, so a request that is somehow issued
        /// outside the interception below still cannot reach a host.
        /// </summary>
        private static readonly string[] kHardenedBrowserArgs =
        [
            "--host-resolver-rules=MAP * ~NOTFOUND",
            "--disable-background-networking",
            "--disable-component-update",
            "--disable-default-apps",
            "--disable-extensions",
            "--disable-remote-fonts",
            "--disable-sync",
            "--no-default-browser-check",
            "--no-first-run",
            "--no-pings"
        ];

        /// <summary>
        /// The command line the render browser is launched with.
        /// </summary>
        /// <returns>A copy of the hardened argument list, so a caller cannot change the shared one.</returns>
        public static string[] GetHardenedBrowserArgs()
        {
            return [.. kHardenedBrowserArgs];
        }

        /// <summary>
        /// Whether a render may load the given url. Only the schemes the render itself needs are allowed;
        /// anything naming a host is refused, including a literal address that needs no name resolution.
        /// A url that cannot be read at all is refused as well.
        /// </summary>
        /// <param name="url">The url the page wants to load.</param>
        /// <returns>True when the request may be continued.</returns>
        public static bool IsAllowedRenderUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUrl)
                && kAllowedRenderSchemes.Contains(parsedUrl.Scheme.ToLowerInvariant());
        }

        /// <summary>
        /// Applies the render lockdown to a page before any content is set on it.
        /// Must be called before the content is written, because a request the page issues while it is
        /// still unprotected is already out of reach.
        /// </summary>
        /// <param name="page">The page the report is about to be rendered on.</param>
        public static async Task HardenPageAsync(IPage page)
        {
            await page.SetJavaScriptEnabledAsync(false);
            await page.SetCacheEnabledAsync(false);
            await page.SetRequestInterceptionAsync(true);
            page.Request += OnPageRequest;
        }

        /// <summary>
        /// Decides a single intercepted request. Puppeteer hands this over as an event, so it cannot be
        /// awaited by the caller and must not let an exception escape: an unhandled one here would end
        /// the process rather than the render.
        /// </summary>
        private static async void OnPageRequest(object? sender, RequestEventArgs eventArgs)
        {
            try
            {
                if (IsAllowedRenderUrl(eventArgs.Request.Url))
                {
                    await eventArgs.Request.ContinueAsync();
                    return;
                }

                Log.WriteWarning("Report Export",
                    $"The report renderer refused to load '{eventArgs.Request.Url}'. The exported content contains a reference that does not belong in a report.");
                await eventArgs.Request.AbortAsync();
            }
            catch (Exception exception)
            {
                Log.WriteError("Report Export", "Deciding an intercepted render request failed.", exception);
            }
        }
    }
}

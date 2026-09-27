using FWO.Services;
using NSubstitute;
using NUnit.Framework;
using PuppeteerSharp;

namespace FWO.Test
{
    /// <summary>
    /// Covers the lockdown of the headless browser that renders report html to pdf (SEC-10).
    /// The report html is built from stored values, so the renderer must not turn a reference hidden in
    /// one of them into a request the server issues.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class PdfRenderSecurityTest
    {
        private const string kBackgroundNetworkingArgument = "--disable-background-networking";

        [Test]
        [TestCase("about:blank")]
        [TestCase("data:text/html,<p>x</p>")]
        public void IsAllowedRenderUrl_AllowsWhatTheRenderItselfNeeds(string url)
        {
            Assert.That(PdfRenderSecurity.IsAllowedRenderUrl(url), Is.True);
        }

        [Test]
        [TestCase("http://127.0.0.1:8080/x")]
        [TestCase("http://[::1]/x")]
        [TestCase("https://attacker.example/collect?token=abc")]
        [TestCase("http://169.254.169.254/latest/meta-data/")]
        [TestCase("file:///etc/passwd")]
        [TestCase("ftp://attacker.example/x")]
        [TestCase("ws://attacker.example/x")]
        [TestCase("//attacker.example/x")]
        [TestCase("not a url")]
        [TestCase("")]
        [TestCase(null)]
        public void IsAllowedRenderUrl_RefusesEverythingThatCouldLeaveTheProcess(string? url)
        {
            Assert.That(PdfRenderSecurity.IsAllowedRenderUrl(url), Is.False);
        }

        [Test]
        public void HardenedBrowserArgs_TurnOffTheBackgroundChannelsOfTheBrowser()
        {
            Assert.That(PdfRenderSecurity.GetHardenedBrowserArgs(), Does.Contain(kBackgroundNetworkingArgument));
        }

        /// <summary>
        /// PuppeteerSharp does not hand an argument containing a space to Chrome as one argv entry, so
        /// Chrome refuses to start and every pdf export fails at launch. An argument list that cannot be
        /// passed is worse than no hardening at all, because it takes the whole feature down with it.
        /// </summary>
        [Test]
        public void HardenedBrowserArgs_CarryNothingThatStopsTheBrowserFromStarting()
        {
            Assert.That(PdfRenderSecurity.GetHardenedBrowserArgs(), Has.None.Contains(" "));
        }

        /// <summary>
        /// The argument list is shared, so a caller that edits what it gets back would weaken every later
        /// render in the process.
        /// </summary>
        [Test]
        public void HardenedBrowserArgs_AreHandedOutAsACopy()
        {
            string[] firstCall = PdfRenderSecurity.GetHardenedBrowserArgs();
            firstCall[0] = "--something-else";

            Assert.That(PdfRenderSecurity.GetHardenedBrowserArgs(), Does.Contain(kBackgroundNetworkingArgument));
        }

        [Test]
        public async Task HardenPage_TurnsOffScriptsAndInterceptsEveryRequest()
        {
            IPage page = Substitute.For<IPage>();

            await PdfRenderSecurity.HardenPageAsync(page);

            await Task.WhenAll(
                page.Received(1).SetJavaScriptEnabledAsync(false),
                page.Received(1).SetCacheEnabledAsync(false),
                page.Received(1).SetRequestInterceptionAsync(true));
        }

        /// <summary>
        /// Interception has to be in place before the content is written: a request the page issues while
        /// it is still unprotected has already left by the time the handler is attached.
        /// </summary>
        [Test]
        public async Task HardenPage_EnablesInterceptionBeforeAnyContentCanBeSet()
        {
            IPage page = Substitute.For<IPage>();

            await PdfRenderSecurity.HardenPageAsync(page);
            await page.SetContentAsync("<html></html>");

            Received.InOrder(() =>
            {
                page.SetRequestInterceptionAsync(true);
                page.SetContentAsync("<html></html>");
            });
        }
    }
}

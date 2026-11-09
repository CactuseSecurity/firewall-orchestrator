using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the context aware encoding of values written into generated report html (SEC-10).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HtmlOutputEncoderTest
    {
        [Test]
        [TestCase("<img src=x>", "&lt;img src=x&gt;")]
        [TestCase("a & b", "a &amp; b")]
        [TestCase("\"quoted\"", "&quot;quoted&quot;")]
        [TestCase("plain name", "plain name")]
        public void EncodeText_MakesMarkupInert(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.EncodeText(value), Is.EqualTo(expected));
        }

        [Test]
        public void EncodeText_TreatsNullAsEmpty()
        {
            Assert.That(HtmlOutputEncoder.EncodeText(null), Is.Empty);
        }

        /// <summary>
        /// A value written into a double quoted attribute escapes it through the quote that ends it, so
        /// that is the character the attribute encoding has to remove.
        /// </summary>
        [Test]
        public void EncodeAttribute_ClosesNoAttributeAndOpensNoTag()
        {
            string encoded = HtmlOutputEncoder.EncodeAttribute("\" onload=\"alert(1)");

            Assert.Multiple(() =>
            {
                Assert.That(encoded, Does.Not.Contain("\""));
                Assert.That(encoded, Does.Contain("&quot;"));
            });
        }

        [Test]
        public void EncodeAttribute_TreatsNullAsEmpty()
        {
            Assert.That(HtmlOutputEncoder.EncodeAttribute(null), Is.Empty);
        }

        [Test]
        [TestCase("#nwobj1x2")]
        [TestCase("ReportGeneration#goto-report-1-nwobj0x5")]
        [TestCase("#")]
        public void ValidateLocalUrl_KeepsTargetsThatStayOnTheDocument(string url)
        {
            Assert.That(HtmlOutputEncoder.ValidateLocalUrl(url), Is.EqualTo(url));
        }

        [Test]
        [TestCase("http://attacker.example/x")]
        [TestCase("https://attacker.example/x")]
        [TestCase("//attacker.example/x")]
        [TestCase("\\\\attacker.example\\x")]
        [TestCase("javascript:alert(1)")]
        [TestCase("data:text/html,<script>alert(1)</script>")]
        [TestCase("file:///etc/passwd")]
        [TestCase("mailto:someone@example.org")]
        public void ValidateLocalUrl_RefusesTargetsThatLeaveTheDocument(string url)
        {
            Assert.That(HtmlOutputEncoder.ValidateLocalUrl(url), Is.EqualTo(HtmlOutputEncoder.kBlockedUrlReplacement));
        }

        /// <summary>
        /// A browser drops whitespace and control characters before it reads the scheme, so a check that
        /// reads the value as written would pass a target the browser then treats as script.
        /// </summary>
        [Test]
        [TestCase("java\nscript:alert(1)")]
        [TestCase("java\tscript:alert(1)")]
        [TestCase("  javascript:alert(1)")]
        [TestCase("java\0script:alert(1)")]
        public void ValidateLocalUrl_RefusesASchemeHiddenBehindIgnoredCharacters(string url)
        {
            Assert.That(HtmlOutputEncoder.ValidateLocalUrl(url), Is.EqualTo(HtmlOutputEncoder.kBlockedUrlReplacement));
        }

        [Test]
        public void ValidateLocalUrl_TreatsNullAndEmptyAsBlocked()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HtmlOutputEncoder.ValidateLocalUrl(null), Is.EqualTo(HtmlOutputEncoder.kBlockedUrlReplacement));
                Assert.That(HtmlOutputEncoder.ValidateLocalUrl("   "), Is.EqualTo(HtmlOutputEncoder.kBlockedUrlReplacement));
            });
        }

        [Test]
        public void EncodeLocalUrl_RefusesAndEncodesInOneStep()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HtmlOutputEncoder.EncodeLocalUrl("http://attacker.example/\"x"), Is.EqualTo("#"));
                Assert.That(HtmlOutputEncoder.EncodeLocalUrl("#a\"b"), Is.EqualTo("#a&quot;b"));
            });
        }
    }
}

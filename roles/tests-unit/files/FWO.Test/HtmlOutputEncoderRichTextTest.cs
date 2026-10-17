using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the allow-list rendering of user formatted free text such as connection reasons (GHSA-mm29-m6vq-gwgg).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HtmlOutputEncoderRichTextTest
    {
        [Test]
        [TestCase("<b>bold</b> <i>italic</i> <u>under</u>", "<b>bold</b> <i>italic</i> <u>under</u>")]
        [TestCase("<STRONG>x</STRONG>", "<strong>x</strong>")]
        [TestCase("<ul><li>one</li><li>two</li></ul>", "<ul><li>one</li><li>two</li></ul>")]
        [TestCase("a<br>b<br/>c<BR />d", "a<br>b<br>c<br>d")]
        [TestCase("a<hr>b", "a<hr>b")]
        [TestCase("plain text", "plain text")]
        public void SanitizeRichText_KeepsFormattingTags(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.SanitizeRichText(value), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("<span style=\"color: red\">x</span>", "<span style=\"color: red\">x</span>")]
        [TestCase("<span style=color:#ff0000>x</span>", "<span style=\"color: #ff0000\">x</span>")]
        [TestCase("<span style='background-color: rgb(1, 2, 3)'>x</span>", "<span style=\"background-color: rgb(1, 2, 3)\">x</span>")]
        [TestCase("<font color=green>x</font>", "<font color=\"green\">x</font>")]
        [TestCase("<span class=\"text-danger bg-warning\">x</span>", "<span class=\"text-danger bg-warning\">x</span>")]
        public void SanitizeRichText_KeepsColors(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.SanitizeRichText(value), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("<span style=\"color: red; position: fixed; top: 0\">x</span>", "<span style=\"color: red\">x</span>")]
        [TestCase("<span style=\"background-color: url(http://evil)\">x</span>", "<span>x</span>")]
        [TestCase("<span style=\"color: expression(alert(1))\">x</span>", "<span>x</span>")]
        [TestCase("<span style=\"color: re\\64\">x</span>", "<span>x</span>")]
        [TestCase("<span class=\"position-fixed text-danger\">x</span>", "<span class=\"text-danger\">x</span>")]
        [TestCase("<b color=red id=x>x</b>", "<b>x</b>")]
        [TestCase("<span style=\"&#99;olor: red\">x</span>", "<span style=\"color: red\">x</span>")]
        public void SanitizeRichText_DropsUnsafeAttributes(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.SanitizeRichText(value), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("<img src=x onerror=console.log(1)>", "&lt;img src=x onerror=console.log(1)&gt;")]
        [TestCase("<b onmouseover=alert(1)>x</b>", "<b>x</b>")]
        [TestCase("<span onclick=\"alert(1)\" style=color:red>x</span>", "<span style=\"color: red\">x</span>")]
        [TestCase("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
        [TestCase("<a href=javascript:alert(1)>x</a>", "&lt;a href=javascript:alert(1)&gt;x&lt;/a&gt;")]
        [TestCase("<svg/onload=alert(1)>", "&lt;svg/onload=alert(1)&gt;")]
        [TestCase("<b class=sweep>marker</b>", "<b>marker</b>")]
        [TestCase("1 < 2 and 3 > 2", "1 &lt; 2 and 3 &gt; 2")]
        [TestCase("<img src=x onerror=alert(1)", "&lt;img src=x onerror=alert(1)")]
        [TestCase("&lt;img src=x onerror=alert(1)&gt;", "&lt;img src=x onerror=alert(1)&gt;")]
        [TestCase("a &amp; b", "a &amp; b")]
        [TestCase("\"quoted\"", "&quot;quoted&quot;")]
        public void SanitizeRichText_EncodesEverythingElse(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.SanitizeRichText(value), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("<b>open", "<b>open</b>")]
        [TestCase("<b><i>x</b>y", "<b><i>x</i></b>y")]
        [TestCase("x</div></b>", "x")]
        [TestCase("<div><span>x</div>", "<div><span>x</span></div>")]
        [TestCase("<br></br>", "<br>")]
        public void SanitizeRichText_BalancesTags(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.SanitizeRichText(value), Is.EqualTo(expected));
        }

        [Test]
        public void SanitizeRichText_LimitsNesting()
        {
            string deepValue = string.Concat(Enumerable.Repeat("<b>", 50)) + "x";
            string result = HtmlOutputEncoder.SanitizeRichText(deepValue);
            int openedTags = result.Split("<b>").Length - 1;
            int closedTags = result.Split("</b>").Length - 1;
            Assert.Multiple(() =>
            {
                Assert.That(openedTags, Is.EqualTo(20));
                Assert.That(closedTags, Is.EqualTo(20));
            });
        }

        [Test]
        [TestCase("line1\nline2", "line1<br>line2")]
        [TestCase("line1\r\nline2\rline3", "line1<br>line2<br>line3")]
        public void SanitizeRichText_KeepsLineBreaks(string value, string expected)
        {
            Assert.That(HtmlOutputEncoder.SanitizeRichText(value), Is.EqualTo(expected));
        }

        /// <summary>
        /// The login page renders the welcome message with this method. Tags the old filter removed
        /// (headings, links, images) are now shown as text, as the help text H5466a describes.
        /// </summary>
        [Test]
        public void SanitizeRichText_RendersWelcomeMessage()
        {
            string welcomeMessage = "<h4>Notice</h4>\r\n<b onclick=alert(1)>Maintenance</b> <a href=\"https://example.com\">info</a>\n<img src=x onerror=alert(1)>";
            string expected = "&lt;h4&gt;Notice&lt;/h4&gt;<br><b>Maintenance</b> &lt;a href=&quot;https://example.com&quot;&gt;info&lt;/a&gt;<br>&lt;img src=x onerror=alert(1)&gt;";
            Assert.That(HtmlOutputEncoder.SanitizeRichText(welcomeMessage), Is.EqualTo(expected));
        }

        [Test]
        public void SanitizeRichText_TreatsNullAsEmpty()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HtmlOutputEncoder.SanitizeRichText(null), Is.Empty);
                Assert.That(HtmlOutputEncoder.SanitizeRichText(""), Is.Empty);
            });
        }
    }
}

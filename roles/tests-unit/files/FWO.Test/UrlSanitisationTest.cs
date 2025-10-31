using NUnit.Framework;
using FWO.Ui.Services;
using Moq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using MimeKit.IO.Filters;

namespace FWO.Test
{
    [TestFixture]
    public class UrlSanitizerTests
    {
        public required UrlSanitizer _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = new UrlSanitizer();
        }

        [TestCase("https://example.com", "https://example.com/")]
        [TestCase("http://example.com", "http://example.com/")]
        [TestCase("https://example.com/path", "https://example.com/path")]
        [TestCase("http://example.com/path?x=1", "http://example.com/path?x=1")]
        public void Clean_AllowsHttpAndHttps_AndReturnsNormalizedUrl(string input, string expectedPrefix)
        {
            var result = _sut.Clean(input);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StartsWith(expectedPrefix), $"Expected URL to start with '{expectedPrefix}'");
        }

        [Test]
        public void Clean_StripsFragment()
        {
            var input = "http://example.com/path#frag";
            var result = _sut.Clean(input);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Contains('#'), Is.False, "Fragment should be stripped.");
            // Optionally double-check using Uri
            var uri = new Uri(result);

            Assert.That(uri.Fragment, Is.EqualTo(string.Empty));
        }


        // source for attack patterns: https://cheatsheetseries.owasp.org/cheatsheets/XSS_Filter_Evasion_Cheat_Sheet.html
        // best way to test this: 
        // - start UI locally in debug mode 
        // - in browser call http://localhost:5000/help/ + attack string

        [TestCase("javascript:alert(1)")]
        [TestCase("https://example.com/help/API/?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("/help/API/?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("help/API/?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("help/API/?lang=<SCRIPT SRC=https://cdn.jsdelivr.net/gh/Moksh45/host-xss.rocks/index.js></SCRIPT>")]
        [TestCase("images/API/?lang=<SCRIPT SRC=https://cdn.jsdelivr.net/gh/Moksh45/host-xss.rocks/index.js></SCRIPT>")]
        [TestCase("js/API/?lang=<SCRIPT SRC=https://cdn.jsdelivr.net/gh/Moksh45/host-xss.rocks/index.js></SCRIPT>")]
        [TestCase("javascript:/*--></title></style></textarea></script></xmp> <svg/onload='+/\"`/+/onmouseover=1/+/[*/[]/+alert(42);//' > ")]
        [TestCase("<a href=\"javascript: alert(String.fromCharCode(88, 83, 83))\">Click Me!</a>")]
        [TestCase("<img src=x onerror=\"&#0000106&#0000097&#0000118&#0000097&#0000115&#0000099&#0000114&#0000105&#0000112&#0000116&#0000058&#0000097&#0000108&#0000101&#0000114&#0000116&#0000040&#0000039&#0000088&#0000083&#0000083&#0000039&#0000041\">")]
        [TestCase(" <a href=\"&#106;&#97;&#118;&#97;&#115;&#99;&#114;&#105;&#112;&#116;&#58;&#97;&#108;&#101;&#114;&#116;&#40;&#39;&#88;&#83;&#83;&#39;&#41;\">Click Me!</a>")]
        [TestCase("<a href=\"&#0000106&#0000097&#0000118&#0000097&#0000115&#0000099&#0000114&#0000105&#0000112&#0000116&#0000058&#0000097&#0000108&#0000101&#0000114&#0000116&#0000040&#0000039&#0000088&#0000083&#0000083&#0000039&#0000041\">Click Me</a>")]
        [TestCase("<a href=\"&#x6A&#x61&#x76&#x61&#x73&#x63&#x72&#x69&#x70&#x74&#x3A&#x61&#x6C&#x65&#x72&#x74&#x28&#x27&#x58&#x53&#x53&#x27&#x29\">Click Me</a>")]
        [TestCase(" <a href=\"jav\tascript: alert('XSS');\">Click Me</a>")]
        [TestCase(" <a href=\"jav &#x09;ascript:alert('XSS');\">Click Me</a>")]
        [TestCase("<a href=\"jav &#x0A;ascript:alert('XSS');\">Click Me</a>")]
        [TestCase("<a href=\"jav &#x0D;ascript:alert('XSS');\">Click Me</a>")]
        [TestCase("<a href=\" &#14;  javascript:alert('XSS');\">Click Me</a>")]
        [TestCase("<SCRIPT/XSS SRC=\"http://xss.rocks/xss.js\"></SCRIPT>")]
        [TestCase("<SCRIPT/SRC=\"http://xss.rocks/xss.js\"></SCRIPT>")]
        [TestCase("<SCRIPT SRC=http://xss.rocks/xss.js?< B >")]
        [TestCase("<SCRIPT SRC=//xss.rocks/.j>")]
        [TestCase("<IMG SRC=\"`< javascript:alert >`('XSS')\"")]
        [TestCase("</TITLE><SCRIPT>alert(\"XSS\");</SCRIPT>")]
        // [TestCase("<<SCRIPT>alert(\"XSS\");//\<</SCRIPT>")]
        // [TestCase("<IMG \"\"\"><SCRIPT>alert(\"XSS\")</SCRIPT>\"\>")]

        public void Clean_RejectsScripts(string input)
        {
            var result = _sut.Clean(input);
            if (result == null)
                return; // accepted as null
            Assert.That(result, Does.Not.Contain("javascript:"));
            Assert.That(result, Does.Not.Contain("<script>"));
        }

        [TestCase("file:///etc/passwd")]
        [TestCase("ftp://example.com/resource")]
        [TestCase("<IMG SRC =# onmouseover=\"alert('xxs')\">")]
        [TestCase("<IMG SRC= onmouseover=\"alert('xxs')\">")]
        [TestCase("<IMG onmouseover=\"alert('xxs')\">")]
        [TestCase("<IMG SRC=/ onerror=\"alert(String.fromCharCode(88, 83, 83))\"></img>")]
        [TestCase("\";alert('XSS');//")]
        [TestCase("<svg/onload=alert('XSS')>")]
        [TestCase("Set.constructor`alert\x28document.domain\x29")]
        // [TestCase("\<a onmouseover=\"alert(document.cookie)\"\>xxs link\</a\>")]
        // [TestCase("\<a onmouseover=alert(document.cookie)\>xxs link\</a\>")]

        public void Clean_RejectsOtherDangerousOrUnsupportedSchemes(string input)
        {
            var result = _sut.Clean(input);
            if (result == null)
                return; // accepted as null
            Assert.That(result, Does.Not.Contain("ftp:"));
            Assert.That(result, Does.Not.Contain("file"));
            Assert.That(result, Does.Not.Contain("alert"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not a url")]
        [TestCase("://missing-scheme.com")]
        public void Clean_InvalidInput_ReturnsNull(string input)
        {
            var result = _sut.Clean(input);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Clean_TrimsWhitespace()
        {
            var input = "   https://example.com/path   ";
            var result = _sut.Clean(input);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StartsWith("https://example.com/path"), "Whitespace should be trimmed.");
        }

        [Test]
        public void Clean_NormalizesUnicode_EquivalentResults()
        {
            // path with decomposed 'e' + combining acute
            var decomposed = "https://example.com/e\u0301";
            // path with precomposed 'é'
            var composed = "https://example.com/é";

            var r1 = _sut.Clean(decomposed);
            var r2 = _sut.Clean(composed);

            Assert.That(r1, Is.Not.Null);
            Assert.That(r2, Is.Not.Null);
            Assert.That(r1, Is.EqualTo(r2), "Sanitizer should normalize to the same canonical form.");
        }

        [Test]
        public void Clean_Rejects_OverlyLongUrls()
        {
            var longPath = new string('a', 2050);
            var input = $"https://example.com/{longPath}";
            var result = _sut.Clean(input);

            Assert.That(result, Is.Null, "URLs longer than 2048 chars should be rejected.");
        }

        [Test]
        public void Filter_InvokesSanitizer_ForUrlParameter()
        {
            var sanitizerMock = new Mock<IUrlSanitizer>();
            sanitizerMock.Setup(s => s.Clean(It.IsAny<string>())).Returns("https://safe.test/");

            var filter = new SanitizeUrlFilter(sanitizerMock.Object);

            // prepare ActionExecutingContext with an action argument containing the URL to sanitize
            var httpContext = new DefaultHttpContext();


            var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor());
            var actionArguments = new Dictionary<string, object> { { "url", "http://evil.com" } };
            var mockController = new Mock<Controller>();
            var ctx = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), (IDictionary<string, object?>)actionArguments, mockController.Object);

            filter.OnActionExecuting(ctx);

            sanitizerMock.Verify(s => s.Clean("http://evil.com"), Times.Once);
        }

    }
}

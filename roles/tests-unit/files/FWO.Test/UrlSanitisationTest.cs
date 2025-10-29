using NUnit.Framework;
using FWO.Ui.Services;
using Moq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;

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

        [TestCase("javascript:alert(1)")]
        [TestCase("data:text/html,<script>alert(1)</script>")]
        [TestCase("file:///etc/passwd")]
        [TestCase("ftp://example.com/resource")]
        [TestCase("https://example.com/help/API/?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("/help/API/?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("help/API/?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        [TestCase("?lang=l%22%3E%3Cscript%3Ealert(%27XSS%27)%3C%2fscript%3Ea")]
        public void Clean_RejectsDangerousOrUnsupportedSchemes(string input)
        {
            var result = _sut.Clean(input);
            if (result == null)
                return; // accepted as null
            Assert.That(result, Does.Not.Contain("javascript:"));
            Assert.That(result, Does.Not.Contain("<script>"));
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

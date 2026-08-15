using FWO.Ui.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    public class UrlSanitizerMiddlewareTest
    {
        [Test]
        public async Task InvokeAsync_WhenSanitizerAcceptsUrl_CallsNext()
        {
            Moq.Mock<IUrlSanitizer> sanitizer = new();
            sanitizer.Setup(s => s.Clean("https://example.com/help?lang=en")).Returns("https://example.com/help?lang=en");
            int nextCallCount = 0;
            UrlSanitizerMiddleware middleware = new(_ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, sanitizer.Object);
            DefaultHttpContext context = CreateContext("https", "example.com", "/help", "?lang=en");

            await middleware.InvokeAsync(context);

            Assert.Multiple(() =>
            {
                Assert.That(nextCallCount, Is.EqualTo(1));
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            });
            sanitizer.Verify(s => s.Clean("https://example.com/help?lang=en"), Moq.Times.Once);
        }

        [Test]
        public async Task InvokeAsync_WhenSanitizerRejectsUrl_ReturnsBadRequestAndSkipsNext()
        {
            Moq.Mock<IUrlSanitizer> sanitizer = new();
            sanitizer.Setup(s => s.Clean("https://example.com/help?lang=en")).Returns((string?)null);
            int nextCallCount = 0;
            UrlSanitizerMiddleware middleware = new(_ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, sanitizer.Object);
            DefaultHttpContext context = CreateContext("https", "example.com", "/help", "?lang=en");

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            string responseBody = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();

            Assert.Multiple(() =>
            {
                Assert.That(nextCallCount, Is.EqualTo(0));
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(responseBody, Is.EqualTo("Invalid or unsafe URL."));
            });
            sanitizer.Verify(s => s.Clean("https://example.com/help?lang=en"), Moq.Times.Once);
        }

        private static DefaultHttpContext CreateContext(string scheme, string host, string path, string queryString)
        {
            DefaultHttpContext context = new();
            context.Request.Scheme = scheme;
            context.Request.Host = new HostString(host);
            context.Request.Path = new PathString(path);
            context.Request.QueryString = new QueryString(queryString);
            context.Response.Body = new MemoryStream();
            return context;
        }
    }
}

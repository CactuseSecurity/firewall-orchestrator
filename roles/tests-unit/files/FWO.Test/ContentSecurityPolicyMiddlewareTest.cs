using System.Security.Cryptography;
using System.Text;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the Content-Security-Policy the UI sends as defence in depth (GHSA-mm29-m6vq-gwgg).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ContentSecurityPolicyMiddlewareTest
    {
        private static string ExpectedHash(string inlineScript)
        {
            return $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(inlineScript)))}'";
        }

        private static string ScriptSource(string policy)
        {
            return policy.Split("; ").Single(directive => directive.StartsWith("script-src ", StringComparison.Ordinal));
        }

        [Test]
        public void BuildPolicy_RefusesInlineScriptsButAllowsOwnHandlers()
        {
            string scriptSource = ScriptSource(ContentSecurityPolicyMiddleware.BuildPolicy(false));
            Assert.Multiple(() =>
            {
                Assert.That(scriptSource, Does.StartWith("script-src 'self' "));
                Assert.That(scriptSource, Does.Not.Contain("'unsafe-inline'"));
                Assert.That(scriptSource, Does.Not.Contain("'unsafe-eval'"));
                Assert.That(scriptSource, Does.Contain(ExpectedHash("return false")));
                Assert.That(scriptSource, Does.Contain(ExpectedHash("event.stopPropagation();")));
            });
        }

        [Test]
        public void BuildPolicy_ContainsHardeningDirectives()
        {
            string policy = ContentSecurityPolicyMiddleware.BuildPolicy(false);
            Assert.Multiple(() =>
            {
                Assert.That(policy, Does.Contain("default-src 'self'"));
                Assert.That(policy, Does.Contain("object-src 'none'"));
                Assert.That(policy, Does.Contain("base-uri 'self'"));
                Assert.That(policy, Does.Contain("frame-ancestors 'self'"));
                Assert.That(policy, Does.Contain("connect-src 'self';"));
            });
        }

        [Test]
        public void BuildPolicy_AllowsHotReloadWebsocketOnlyInDevelopment()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ContentSecurityPolicyMiddleware.BuildPolicy(true), Does.Contain("connect-src 'self' ws: wss:"));
                Assert.That(ContentSecurityPolicyMiddleware.BuildPolicy(false), Does.Not.Contain("ws:"));
            });
        }

        [Test]
        public async Task InvokeAsync_SetsHeaderWhenResponseStarts()
        {
            Moq.Mock<IWebHostEnvironment> environment = new();
            environment.SetupGet(env => env.EnvironmentName).Returns(Environments.Production);
            int nextCallCount = 0;
            ContentSecurityPolicyMiddleware middleware = new(_ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, environment.Object);
            CapturingResponseFeature responseFeature = new();
            DefaultHttpContext context = new();
            context.Features.Set<IHttpResponseFeature>(responseFeature);

            await middleware.InvokeAsync(context);
            await responseFeature.StartAsync();

            Assert.Multiple(() =>
            {
                Assert.That(nextCallCount, Is.EqualTo(1));
                Assert.That(context.Response.Headers[ContentSecurityPolicyMiddleware.kHeaderName].ToString(),
                    Is.EqualTo(ContentSecurityPolicyMiddleware.BuildPolicy(false)));
            });
        }

        /// <summary>
        /// Response feature that runs the registered OnStarting callbacks on request, as the server would.
        /// </summary>
        private sealed class CapturingResponseFeature : HttpResponseFeature
        {
            private readonly List<(Func<object, Task> Callback, object State)> startingCallbacks = [];

            public override void OnStarting(Func<object, Task> callback, object state)
            {
                startingCallbacks.Add((callback, state));
            }

            public async Task StartAsync()
            {
                foreach ((Func<object, Task> callback, object state) in startingCallbacks)
                {
                    await callback(state);
                }
            }
        }
    }
}

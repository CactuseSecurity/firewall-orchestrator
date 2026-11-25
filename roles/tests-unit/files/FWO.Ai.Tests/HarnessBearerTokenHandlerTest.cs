using FWO.Ai.Harness.Mcp;
using NUnit.Framework;
using System.Net;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests that the harness sends the calling user's JWT with every request to the MCP server.
/// </summary>
[TestFixture]
internal class HarnessBearerTokenHandlerTest
{
    private const string kRequestUri = "https://middleware.example.com:8888/mcp";
    private const string kFirstToken = "first-token";
    private const string kRenewedToken = "renewed-token";

    private static readonly List<string> kExpectedAuthorizationHeaders = [$"Bearer {kFirstToken}", $"Bearer {kRenewedToken}"];

    /// <summary>
    /// Verifies that each request carries the token the provider returns at that moment, so a renewed token is used.
    /// </summary>
    [Test]
    public async Task SendAsync_AsksProviderPerRequest_UsesCurrentToken()
    {
        string currentToken = kFirstToken;
        int providerCalls = 0;
        RecordingHandler recordingHandler = new();
        using HttpMessageInvoker invoker = new(new BearerTokenHandler(_ =>
        {
            providerCalls++;
            return ValueTask.FromResult(currentToken);
        })
        { InnerHandler = recordingHandler });

        using HttpResponseMessage firstResponse = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, kRequestUri), CancellationToken.None);
        currentToken = kRenewedToken;
        using HttpResponseMessage secondResponse = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, kRequestUri), CancellationToken.None);

        Assert.That(providerCalls, Is.EqualTo(2));
        Assert.That(recordingHandler.AuthorizationHeaders, Is.EqualTo(kExpectedAuthorizationHeaders));
    }

    /// <summary>
    /// Verifies that a request is not sent when the provider returns no token.
    /// </summary>
    [TestCase("")]
    [TestCase("  ")]
    public void SendAsync_WithBlankToken_ThrowsWithoutSending(string token)
    {
        RecordingHandler recordingHandler = new();
        using HttpMessageInvoker invoker = new(new BearerTokenHandler(_ => ValueTask.FromResult(token)) { InnerHandler = recordingHandler });

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, kRequestUri), CancellationToken.None));
        Assert.That(recordingHandler.AuthorizationHeaders, Is.Empty);
    }

    /// <summary>
    /// Handler recording the authorization header of each request instead of sending it.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        /// <summary>
        /// Authorization headers of the requests, in the order they were sent.
        /// </summary>
        internal List<string> AuthorizationHeaders { get; } = [];

        /// <summary>
        /// Records the authorization header and answers with 200.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

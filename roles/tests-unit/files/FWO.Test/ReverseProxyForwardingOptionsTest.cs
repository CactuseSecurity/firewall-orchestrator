using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

/// <summary>
/// Tests the forwarding configuration of the UI, middleware and AI MCP server for the local Apache proxy.
/// </summary>
[TestFixture]
internal class ReverseProxyForwardingOptionsTest
{
    private const string kHttpScheme = "http";
    private const string kHttpsScheme = "https";

    /// <summary>
    /// Verifies that the HTTPS scheme and the client address are forwarded from loopback Apache proxies only.
    /// </summary>
    [Test]
    public void Create_ForwardsProtoAndClientFromLoopbackProxies()
    {
        ForwardedHeadersOptions options = ReverseProxyForwardingOptions.Create();

        Assert.That(options.ForwardedHeaders, Is.EqualTo(ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor));
        Assert.That(options.ForwardLimit, Is.EqualTo(1));
        Assert.That(options.KnownProxies, Does.Contain(IPAddress.Loopback));
        Assert.That(options.KnownProxies, Does.Contain(IPAddress.IPv6Loopback));
    }

    /// <summary>
    /// Verifies that the scheme and the client address are the ones Apache appended, not an address the client sent itself.
    /// </summary>
    [Test]
    public async Task ForwardedHeaders_UseTheValuesAppendedByTheProxy()
    {
        DefaultHttpContext context = CreateProxiedContext(IPAddress.Loopback, "127.0.0.1, 192.0.2.30");

        await ApplyForwardedHeaders(context);

        Assert.That(context.Request.Scheme, Is.EqualTo(kHttpsScheme));
        Assert.That(context.Connection.RemoteIpAddress, Is.EqualTo(IPAddress.Parse("192.0.2.30")));
    }

    /// <summary>
    /// Verifies that a client connecting directly can choose neither its address nor the scheme through the headers.
    /// </summary>
    [Test]
    public async Task ForwardedHeaders_IgnoreTheHeadersFromUntrustedPeers()
    {
        DefaultHttpContext context = CreateProxiedContext(IPAddress.Parse("192.0.2.40"), "127.0.0.1");

        await ApplyForwardedHeaders(context);

        Assert.That(context.Request.Scheme, Is.EqualTo(kHttpScheme));
        Assert.That(context.Connection.RemoteIpAddress, Is.EqualTo(IPAddress.Parse("192.0.2.40")));
    }

    private static DefaultHttpContext CreateProxiedContext(IPAddress peerAddress, string forwardedFor)
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = kHttpScheme;
        context.Connection.RemoteIpAddress = peerAddress;
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = kHttpsScheme;
        return context;
    }

    private static async Task ApplyForwardedHeaders(HttpContext context)
    {
        ForwardedHeadersMiddleware middleware = new(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(ReverseProxyForwardingOptions.Create()));
        await middleware.Invoke(context);
    }
}

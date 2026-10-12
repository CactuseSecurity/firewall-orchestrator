using FWO.Middleware.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

/// <summary>
/// Tests the middleware forwarding configuration for the local Apache proxy.
/// </summary>
[TestFixture]
internal class ReverseProxyForwardingOptionsTest
{
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
    /// Verifies that the client address is the entry Apache appended, not one the client sent itself.
    /// </summary>
    [Test]
    public async Task ForwardedHeaders_UseTheAddressAppendedByTheProxy()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1, 192.0.2.30";
        ForwardedHeadersMiddleware middleware = new(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(ReverseProxyForwardingOptions.Create()));

        await middleware.Invoke(context);

        Assert.That(context.Connection.RemoteIpAddress, Is.EqualTo(IPAddress.Parse("192.0.2.30")));
    }

    /// <summary>
    /// Verifies that a client connecting directly cannot choose its address through the header.
    /// </summary>
    [Test]
    public async Task ForwardedHeaders_IgnoreTheHeaderFromUntrustedPeers()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.40");
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        ForwardedHeadersMiddleware middleware = new(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(ReverseProxyForwardingOptions.Create()));

        await middleware.Invoke(context);

        Assert.That(context.Connection.RemoteIpAddress, Is.EqualTo(IPAddress.Parse("192.0.2.40")));
    }
}

using FWO.Middleware.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
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
    /// Verifies that only the HTTPS scheme is forwarded from loopback Apache proxies.
    /// </summary>
    [Test]
    public void Create_ForwardsProtoFromLoopbackProxies()
    {
        ForwardedHeadersOptions options = ReverseProxyForwardingOptions.Create();

        Assert.That(options.ForwardedHeaders, Is.EqualTo(ForwardedHeaders.XForwardedProto));
        Assert.That(options.KnownProxies, Does.Contain(IPAddress.Loopback));
        Assert.That(options.KnownProxies, Does.Contain(IPAddress.IPv6Loopback));
    }
}

using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace FWO.Middleware.Server;

/// <summary>
/// Creates forwarding settings for the local Apache HTTPS reverse proxy.
/// </summary>
public static class ReverseProxyForwardingOptions
{
    /// <summary>
    /// Creates options that trust HTTPS metadata from the local Apache reverse proxy only.
    /// </summary>
    /// <returns>Forwarded-header options for the middleware request pipeline.</returns>
    public static ForwardedHeadersOptions Create()
    {
        ForwardedHeadersOptions options = new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto
        };

        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);

        return options;
    }
}

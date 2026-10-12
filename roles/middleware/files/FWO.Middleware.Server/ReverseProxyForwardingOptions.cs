using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace FWO.Middleware.Server;

/// <summary>
/// Creates forwarding settings for the local Apache HTTPS reverse proxy.
/// </summary>
public static class ReverseProxyForwardingOptions
{
    /// <summary>
    /// Creates options that trust HTTPS metadata and the client address from the local Apache reverse proxy only.
    /// </summary>
    /// <remarks>
    /// Apache appends the address of its client to X-Forwarded-For. Only that last entry is used
    /// (ForwardLimit 1), so a client cannot choose its own address by sending the header itself. The client
    /// address keys the per-client login limit, see <see cref="LoginThrottle"/>.
    /// </remarks>
    /// <returns>Forwarded-header options for the middleware request pipeline.</returns>
    public static ForwardedHeadersOptions Create()
    {
        ForwardedHeadersOptions options = new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
            ForwardLimit = 1
        };

        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);

        return options;
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace FWO.Basics;

/// <summary>
/// Creates forwarding settings for the local Apache HTTPS reverse proxy in front of the UI, middleware and AI MCP server.
/// </summary>
public static class ReverseProxyForwardingOptions
{
    /// <summary>
    /// Creates options that trust HTTPS metadata and the client address from the local Apache reverse proxy only.
    /// </summary>
    /// <remarks>
    /// Apache appends the address of its client to X-Forwarded-For. Only that last entry is used
    /// (ForwardLimit 1), so a client cannot choose its own address by sending the header itself. The client
    /// address is logged with logins and keys the per-client login limit of the middleware.
    /// </remarks>
    /// <returns>Forwarded-header options for the request pipeline.</returns>
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

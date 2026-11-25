using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace FWO.Basics
{
    /// <summary>
    /// Checks the endpoints a secret (an API key, a client secret or a user's JWT) is sent to, and the listeners of the
    /// web servers such secrets are sent to. They must use https unless they are a loopback address, so that a secret
    /// never crosses the network in clear text. The endpoint itself is never part of an error message, as it may contain
    /// user information.
    /// </summary>
    public static class EndpointGuard
    {
        private const string kLocalhost = "localhost";
        private const char kListenerUrlSeparator = ';';

        /// <summary>
        /// Requires an absolute http or https URI.
        /// </summary>
        /// <param name="endpoint">Endpoint to check</param>
        /// <param name="purpose">What the endpoint is used for, for the error message</param>
        /// <exception cref="ArgumentNullException">The endpoint is null.</exception>
        /// <exception cref="ArgumentException">The endpoint is relative or uses another scheme.</exception>
        public static void RequireHttpOrHttps(Uri endpoint, string purpose)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            bool isHttpOrHttps = endpoint.IsAbsoluteUri && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);
            if (!isHttpOrHttps)
            {
                throw new ArgumentException($"The {purpose} must be an absolute http or https URI.", nameof(endpoint));
            }
        }

        /// <summary>
        /// Requires an absolute https URI, or an http URI of a loopback address, for endpoints a secret is sent to.
        /// </summary>
        /// <param name="endpoint">Endpoint to check</param>
        /// <param name="purpose">What the endpoint is used for, for the error message</param>
        /// <exception cref="ArgumentNullException">The endpoint is null.</exception>
        /// <exception cref="ArgumentException">The endpoint is relative, uses another scheme or plain http to a remote host.</exception>
        public static void RequireHttpsOrLoopback(Uri endpoint, string purpose)
        {
            RequireHttpOrHttps(endpoint, purpose);
            if (endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
            {
                throw new ArgumentException($"The {purpose} must use https unless it is a loopback address, as a secret is sent to it.", nameof(endpoint));
            }
        }

        /// <summary>
        /// Requires every URL of a web server listener, separated by ';' as Kestrel's UseUrls takes them, to use https or
        /// to bind plain http to loopback only, for web servers secrets are sent to. Loopback is decided the way Kestrel
        /// binds, not by <see cref="Uri.IsLoopback"/>: only "localhost" and loopback IP addresses bind to loopback, while
        /// every other host name (also "loopback", which <see cref="Uri"/> treats as localhost), "*", "+" and the
        /// unspecified addresses bind to every interface.
        /// </summary>
        /// <param name="listenerUrls">Listener URLs, separated by ';'</param>
        /// <param name="purpose">What the listener is used for, for the error message</param>
        /// <exception cref="ArgumentNullException">The listener URLs are null.</exception>
        /// <exception cref="ArgumentException">There is no URL, or one is no http or https URL or binds plain http to more
        /// than loopback.</exception>
        public static void RequireHttpsOrLoopbackListener(string listenerUrls, string purpose)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(listenerUrls);
            List<string> listenerUrlList = listenerUrls
                .Split(kListenerUrlSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (listenerUrlList.Count == 0)
            {
                throw new ArgumentException($"The {purpose} must contain at least one URL.", nameof(listenerUrls));
            }
            foreach (string listenerUrl in listenerUrlList)
            {
                RequireHttpsOrLoopbackListenerUrl(listenerUrl, purpose);
            }
        }

        /// <summary>
        /// Requires the listener a web application takes from its configuration to use https or to bind plain http to
        /// loopback only, for web servers secrets are sent to. Follows the order in which Kestrel picks it: the "urls"
        /// setting (ASPNETCORE_URLS, --urls or the launch profile); without it the "http_ports" setting, which binds to
        /// every interface and is therefore refused; without both Kestrel's default, http://localhost:5000. Endpoints in
        /// the "Kestrel" section of the configuration are not checked; FWO does not configure any.
        /// </summary>
        /// <param name="configuration">Configuration of the web application</param>
        /// <param name="purpose">What the listener is used for, for the error message</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentException">A URL is no http or https URL, or plain http is bound to more than
        /// loopback.</exception>
        public static void RequireHttpsOrLoopbackListener(IConfiguration configuration, string purpose)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            string? listenerUrls = configuration[WebHostDefaults.ServerUrlsKey];
            if (!string.IsNullOrWhiteSpace(listenerUrls))
            {
                RequireHttpsOrLoopbackListener(listenerUrls, purpose);
                return;
            }
            if (!string.IsNullOrWhiteSpace(configuration[WebHostDefaults.HttpPortsKey]))
            {
                throw new ArgumentException($"The {purpose} must use https unless it binds to a loopback address only, as secrets are sent to it. "
                    + "Plain http ports without a URL bind to every interface.", nameof(configuration));
            }
        }

        /// <summary>
        /// Requires a single listener URL to use https or to bind plain http to loopback only.
        /// </summary>
        /// <param name="listenerUrl">Listener URL</param>
        /// <param name="purpose">What the listener is used for, for the error message</param>
        /// <exception cref="ArgumentException">The URL is no http or https URL or binds plain http to more than loopback.</exception>
        private static void RequireHttpsOrLoopbackListenerUrl(string listenerUrl, string purpose)
        {
            BindingAddress binding;
            try
            {
                binding = BindingAddress.Parse(listenerUrl);
            }
            catch (FormatException)
            {
                // Not passed on as inner exception, as its message contains the URL.
                throw new ArgumentException($"Each URL of the {purpose} must be an absolute http or https URL.", nameof(listenerUrl));
            }
            bool isHttp = string.Equals(binding.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
            bool isHttps = string.Equals(binding.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttp && !isHttps)
            {
                throw new ArgumentException($"Each URL of the {purpose} must be an absolute http or https URL.", nameof(listenerUrl));
            }
            if (isHttp && !BindsToLoopbackOnly(binding))
            {
                throw new ArgumentException($"The {purpose} must use https unless it binds to a loopback address only, as secrets are sent to it. "
                    + "Any host name other than localhost binds to every interface.", nameof(listenerUrl));
            }
        }

        /// <summary>
        /// Returns whether Kestrel binds the address to loopback only: "localhost" or a loopback IP address. Every other
        /// host binds to every interface.
        /// </summary>
        private static bool BindsToLoopbackOnly(BindingAddress binding)
        {
            if (string.Equals(binding.Host, kLocalhost, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return IPAddress.TryParse(binding.Host, out IPAddress? address) && IPAddress.IsLoopback(address);
        }
    }
}

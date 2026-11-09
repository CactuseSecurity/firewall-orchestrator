using System.Security.Cryptography;
using System.Text;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Sends a Content-Security-Policy with every UI response, as defence in depth against injected markup
    /// (GHSA-mm29-m6vq-gwgg). Scripts may only come from this origin. Inline event handlers are refused,
    /// except for the few fixed handlers the UI itself writes, which are allowed by their hash.
    /// Inline styles stay allowed, because components and generated reports set style attributes.
    /// </summary>
    public sealed class ContentSecurityPolicyMiddleware
    {
        /// <summary>
        /// Name of the response header carrying the policy.
        /// </summary>
        public const string kHeaderName = "Content-Security-Policy";

        /// <summary>
        /// Inline event handlers the UI writes itself: forms that must not submit on enter, and report links
        /// (<c>ReportBase.ConstructLink</c>) that must not trigger the click handler of the surrounding row.
        /// </summary>
        private static readonly List<string> AllowedInlineHandlers = ["return false", "event.stopPropagation();"];

        private readonly RequestDelegate _next;
        private readonly string _policy;

        /// <summary>
        /// Creates the middleware.
        /// </summary>
        /// <param name="next">The next middleware of the pipeline.</param>
        /// <param name="environment">Hosting environment; development additionally allows the hot reload websocket.</param>
        public ContentSecurityPolicyMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            ArgumentNullException.ThrowIfNull(environment);
            _policy = BuildPolicy(environment.IsDevelopment());
        }

        /// <summary>
        /// Adds the policy header and passes the request on.
        /// </summary>
        /// <param name="context">The current http context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[kHeaderName] = _policy;
                return Task.CompletedTask;
            });
            await _next(context);
        }

        /// <summary>
        /// Builds the policy string.
        /// </summary>
        /// <param name="isDevelopment">True to also allow websocket connections to other local ports (hot reload).</param>
        /// <returns>The value of the Content-Security-Policy header.</returns>
        public static string BuildPolicy(bool isDevelopment)
        {
            string handlerHashes = string.Join(" ", AllowedInlineHandlers.Select(HashSource));
            string connectSources = isDevelopment ? "'self' ws: wss:" : "'self'";
            List<string> directives =
            [
                "default-src 'self'",
                $"script-src 'self' 'unsafe-hashes' {handlerHashes}",
                "style-src 'self' 'unsafe-inline'",
                "img-src 'self' data:",
                "font-src 'self'",
                $"connect-src {connectSources}",
                "object-src 'none'",
                "base-uri 'self'",
                "form-action 'self'",
                "frame-ancestors 'self'"
            ];
            return string.Join("; ", directives);
        }

        private static string HashSource(string inlineScript)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(inlineScript));
            return $"'sha256-{Convert.ToBase64String(hash)}'";
        }
    }
}

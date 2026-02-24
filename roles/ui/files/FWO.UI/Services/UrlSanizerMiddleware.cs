namespace FWO.Ui.Services
{
    public sealed class UrlSanitizerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IUrlSanitizer _sanitizer;

        public UrlSanitizerMiddleware(RequestDelegate next, IUrlSanitizer sanitizer)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Build ABSOLUTE URL because Clean expects absolute
            var req = context.Request;
            var absolute = $"{req.Scheme}://{req.Host}{req.Path}{req.QueryString}";

            var sanitized = _sanitizer.Clean(absolute);
            if (sanitized is null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid or unsafe URL.");
                return;
            }

            await _next(context);
        }
    }
}

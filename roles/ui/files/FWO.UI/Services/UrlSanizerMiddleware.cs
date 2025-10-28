namespace FWO.Ui.Services
{
    public class UrlSanitizerMiddleware
    {
        public interface IUrlSanitizer
        {
            string? Clean(string url);
        }
        private readonly RequestDelegate _next;
        private readonly IUrlSanitizer _sanitizer;

        public UrlSanitizerMiddleware(RequestDelegate next, IUrlSanitizer sanitizer)
        {
            _next = next;
            _sanitizer = sanitizer;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var originalUrl = context.Request.Path + context.Request.QueryString;
            var sanitized = _sanitizer.Clean(originalUrl);

            if (sanitized == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid or unsafe URL.");
                return;
            }

            // Continue with request
            await _next(context);
        }
    }
}

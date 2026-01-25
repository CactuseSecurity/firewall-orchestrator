using Microsoft.AspNetCore.Mvc.Filters;

namespace FWO.Ui.Services

{
    public class SanitizeUrlFilter(IUrlSanitizer sanitizer) : IActionFilter
    {
        private readonly IUrlSanitizer _sanitizer = sanitizer;

        public void OnActionExecuted(ActionExecutedContext context)
        {
            throw new NotImplementedException();
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            foreach (var arg in context.ActionArguments.ToList())
            {
                if (arg.Value is string s && s.Contains("http", StringComparison.OrdinalIgnoreCase))
                    context.ActionArguments[arg.Key] = _sanitizer.Clean(s);
            }
        }

    }
}

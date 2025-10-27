using System.Text;
using System.Web;

namespace FWO.Ui.Services
{

    public sealed class UrlSanitizer : IUrlSanitizer
    {
        const char PathDelimiter = '/';


        public string? Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // Trim + Unicode normalize
            input = input.Trim().Normalize(NormalizationForm.FormC);

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
                return null;

            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme is not ("http" or "https"))
                return null;

            // Sanitize path segments
            var sanitizedPath = string.Join(PathDelimiter, uri.Segments
                .Select(s => HttpUtility.UrlEncode(
                    HttpUtility.UrlDecode(s.TrimEnd('/'))
                ))
            );
            if (!sanitizedPath.StartsWith(PathDelimiter.ToString()))
                sanitizedPath = PathDelimiter + sanitizedPath;

            // Parse and sanitize query parameters
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var sanitizedQuery = new StringBuilder();
            foreach (string key in queryParams)
            {
                if (key == null) continue;

                if (sanitizedQuery.Length > 0)
                    sanitizedQuery.Append('&');

                sanitizedQuery.Append(HttpUtility.UrlEncode(key));
                sanitizedQuery.Append('=');
                var value = queryParams[key]?.Replace("<", "").Replace(">", "");
                sanitizedQuery.Append(HttpUtility.UrlEncode(value));
            }

            // Rebuild URL
            var builder = new UriBuilder(uri)
            {
                Path = sanitizedPath,
                Fragment = string.Empty,
                Query = sanitizedQuery.ToString()
            };

            if (builder.Uri.AbsoluteUri.Length > 2048)
                return null;

            return builder.Uri.AbsoluteUri;
        }

    }

}

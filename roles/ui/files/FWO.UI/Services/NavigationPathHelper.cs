namespace FWO.Ui.Services
{
    public static class NavigationPathHelper
    {
        /// <summary>
        /// Converts a navigation target to a normalized path relative to the UI application.
        /// </summary>
        public static string GetBaseRelativePath(string location, string baseUri)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return "";
            }

            string relativePath = location;
            if (Uri.TryCreate(location, UriKind.Absolute, out Uri? targetUri) && IsHttpUri(targetUri))
            {
                if (!Uri.TryCreate(baseUri, UriKind.Absolute, out Uri? parsedBaseUri) || !parsedBaseUri.IsBaseOf(targetUri))
                {
                    return "";
                }

                relativePath = parsedBaseUri.MakeRelativeUri(targetUri).ToString();
            }

            return relativePath.Split(['?', '#'])[0].Trim('/').ToLowerInvariant();
        }

        /// <summary>
        /// Determines whether the URI uses an HTTP scheme handled by Blazor navigation.
        /// </summary>
        private static bool IsHttpUri(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}

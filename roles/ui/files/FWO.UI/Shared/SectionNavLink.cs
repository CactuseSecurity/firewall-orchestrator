using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace FWO.Ui.Shared
{
    /// <summary>
    /// A navigation link whose active state can represent a complete application section.
    /// </summary>
    public class SectionNavLink : NavLink
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        /// <summary>
        /// Gets or sets the application-relative path which identifies the section.
        /// </summary>
        [Parameter]
        public string ActivePath { get; set; } = "";

        /// <summary>
        /// Gets or sets an optional application-relative path excluded from this section.
        /// </summary>
        [Parameter]
        public string ExcludedPath { get; set; } = "";

        /// <summary>
        /// Determines whether the current URI belongs to the configured application section.
        /// </summary>
        protected override bool ShouldMatch(string uriAbsolute)
        {
            string currentPath = NavigationManager.ToBaseRelativePath(uriAbsolute);
            int queryOrFragmentStart = currentPath.IndexOfAny('?', '#');
            if (queryOrFragmentStart >= 0)
            {
                currentPath = currentPath[..queryOrFragmentStart];
            }

            return IsPathMatch(currentPath, ActivePath) && !IsPathMatch(currentPath, ExcludedPath);
        }

        /// <summary>
        /// Checks a route path using segment boundaries so similarly named routes do not match.
        /// </summary>
        private static bool IsPathMatch(string currentPath, string configuredPath)
        {
            string normalizedPath = configuredPath.Trim('/');
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return false;
            }

            return currentPath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)
                || currentPath.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}

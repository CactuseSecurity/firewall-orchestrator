namespace FWO.Basics
{
    /// <summary>
    /// Locates a distribution-provided (headless) chromium binary that can be used
    /// as fallback when no downloaded Chrome for Testing installation is available.
    /// </summary>
    public static class SystemChromium
    {
        /// <summary>
        /// Known locations of system chromium binaries, in order of preference.
        /// </summary>
        public static readonly List<string> DefaultCandidatePaths =
        [
            GlobalConst.ChromiumHeadlessBinPathLinux,
            "/usr/lib64/chromium-browser/headless_shell",
            "/usr/bin/chromium-browser",
            "/usr/bin/chromium"
        ];

        /// <summary>
        /// Returns the path of the first existing system chromium binary or null if none exists.
        /// </summary>
        public static string? GetPath()
        {
            return GetPath(DefaultCandidatePaths, File.Exists);
        }

        /// <summary>
        /// Returns the first candidate path accepted by the given existence check or null.
        /// </summary>
        public static string? GetPath(IEnumerable<string> candidatePaths, Func<string, bool> fileExists)
        {
            return candidatePaths.FirstOrDefault(fileExists);
        }
    }
}

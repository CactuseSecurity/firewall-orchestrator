namespace FWO.Ui.Services
{
    /// <summary>
    /// Exposes the build configuration the UI was compiled with, so markup can show debug-only features.
    /// </summary>
    public static class BuildConfiguration
    {
        /// <summary>
        /// True if the UI was built in Debug configuration.
        /// Not a const, so checks against it do not produce unreachable code warnings.
        /// </summary>
#if DEBUG
        public static bool IsDebug { get; } = true;
#else
        public static bool IsDebug { get; } = false;
#endif
    }
}

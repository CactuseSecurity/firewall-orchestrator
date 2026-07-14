namespace FWO.Services
{
    /// <summary>
    /// Timing options shared by the background services that rotate short-lived tokens before they expire.
    /// </summary>
    public class TokenRefreshOptions
    {
        /// <summary>
        /// Time before token expiry at which a refresh should begin.
        /// </summary>
        public TimeSpan RefreshLeadTime { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Interval used by the background refresh loop.
        /// </summary>
        public TimeSpan RefreshCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}

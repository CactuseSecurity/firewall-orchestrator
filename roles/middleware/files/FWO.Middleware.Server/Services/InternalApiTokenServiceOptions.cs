namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Timing options for refreshing the shared middleware-server JWT.
    /// </summary>
    public class InternalApiTokenServiceOptions
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

namespace FWO.Ui.Services
{
    /// <summary>
    /// Options for refreshing the anonymous token used by the singleton global configuration subscription.
    /// </summary>
    public class GlobalConfigTokenRefreshOptions
    {
        /// <summary>
        /// How long before expiry the anonymous global config token should be rotated.
        /// </summary>
        public TimeSpan RefreshLeadTime { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// How often the background service checks whether the token needs to be rotated.
        /// </summary>
        public TimeSpan RefreshCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}

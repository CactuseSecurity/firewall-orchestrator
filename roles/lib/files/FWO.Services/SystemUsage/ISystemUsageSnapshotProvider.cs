namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Provides the current system and process resource usage of the own FWO service for UI consumption.
    /// </summary>
    public interface ISystemUsageSnapshotProvider
    {
        /// <summary>
        /// Returns the current usage values. Samples taken within the caching interval of a previous
        /// call return the already known snapshot, so that concurrent callers share one measurement.
        /// </summary>
        /// <returns>The current usage snapshot.</returns>
        SystemUsageSnapshot Collect();
    }
}

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Point in time measurement of the system and process resource usage of an FWO service.
    /// </summary>
    public class SystemUsageSnapshot
    {
        /// <summary>
        /// Point in time (UTC) at which the values were sampled.
        /// </summary>
        public DateTime CollectedAt { get; set; }

        /// <summary>
        /// True if the operating system counters could be read. If false, all system values are zero.
        /// </summary>
        public bool SourceAvailable { get; set; }

        /// <summary>
        /// Usage of the other FWO services running on the same host, empty if none of them was found.
        /// </summary>
        public List<ServiceUsage> Services { get; set; } = [];

        /// <summary>
        /// Total physical memory of the system in bytes.
        /// </summary>
        public long MemoryTotalBytes { get; set; }

        /// <summary>
        /// Completely unused physical memory of the system in bytes.
        /// </summary>
        public long MemoryFreeBytes { get; set; }

        /// <summary>
        /// Memory in bytes that is available for new applications without swapping (includes reclaimable caches).
        /// </summary>
        public long MemoryAvailableBytes { get; set; }

        /// <summary>
        /// Physical memory in bytes that is currently in use (total minus available).
        /// </summary>
        public long MemoryUsedBytes => Math.Max(0, MemoryTotalBytes - MemoryAvailableBytes);

        /// <summary>
        /// Share of the physical memory that is currently in use, in percent.
        /// </summary>
        public double MemoryUsedPercent => Percentage(MemoryUsedBytes, MemoryTotalBytes);

        /// <summary>
        /// Total swap space of the system in bytes.
        /// </summary>
        public long SwapTotalBytes { get; set; }

        /// <summary>
        /// Unused swap space of the system in bytes.
        /// </summary>
        public long SwapFreeBytes { get; set; }

        /// <summary>
        /// Swap space in bytes that is currently in use.
        /// </summary>
        public long SwapUsedBytes => Math.Max(0, SwapTotalBytes - SwapFreeBytes);

        /// <summary>
        /// Share of the swap space that is currently in use, in percent.
        /// </summary>
        public double SwapUsedPercent => Percentage(SwapUsedBytes, SwapTotalBytes);

        /// <summary>
        /// System wide CPU utilization since the previous sample, in percent.
        /// </summary>
        public double CpuUsedPercent { get; set; }

        /// <summary>
        /// System load average of the last minute.
        /// </summary>
        public double LoadAverage1 { get; set; }

        /// <summary>
        /// System load average of the last five minutes.
        /// </summary>
        public double LoadAverage5 { get; set; }

        /// <summary>
        /// System load average of the last fifteen minutes.
        /// </summary>
        public double LoadAverage15 { get; set; }

        /// <summary>
        /// Number of logical processors available to the service.
        /// </summary>
        public int ProcessorCount { get; set; }

        /// <summary>
        /// CPU utilization of the own service process since the previous sample, in percent of all cores.
        /// </summary>
        public double ProcessCpuPercent { get; set; }

        /// <summary>
        /// Resident memory of the own service process in bytes.
        /// </summary>
        public long ProcessWorkingSetBytes { get; set; }

        /// <summary>
        /// Private memory of the own service process in bytes.
        /// </summary>
        public long ProcessPrivateMemoryBytes { get; set; }

        /// <summary>
        /// Size of the managed heap of the own service process in bytes.
        /// </summary>
        public long ProcessManagedHeapBytes { get; set; }

        /// <summary>
        /// Number of threads of the own service process.
        /// </summary>
        public int ProcessThreadCount { get; set; }

        /// <summary>
        /// Point in time (UTC) at which the own service process was started.
        /// </summary>
        public DateTime ProcessStartTime { get; set; }

        /// <summary>
        /// Time the own service process has been running.
        /// </summary>
        public TimeSpan ProcessUpTime => CollectedAt > ProcessStartTime ? CollectedAt - ProcessStartTime : TimeSpan.Zero;

        private static double Percentage(long part, long total)
        {
            return total > 0 ? Math.Clamp(100.0 * part / total, 0, 100) : 0;
        }
    }
}

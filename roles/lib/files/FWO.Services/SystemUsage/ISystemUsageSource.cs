namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Abstraction of the operating system counters needed to build a <see cref="SystemUsageSnapshot"/>.
    /// Allows the collector to be tested without touching the file system.
    /// </summary>
    public interface ISystemUsageSource
    {
        /// <summary>
        /// Reads one of the kernel status files below /proc.
        /// </summary>
        /// <param name="fileName">File name below /proc, e.g. "meminfo".</param>
        /// <returns>The file content or null if it cannot be read.</returns>
        string? ReadProcFile(string fileName);

        /// <summary>
        /// Re-reads the counters of the own process. Called once before the process values of a sample are
        /// taken, so that they do not have to be read again for every single value.
        /// </summary>
        void RefreshProcessInfo();

        /// <summary>
        /// Total processor time consumed by the own process so far.
        /// </summary>
        TimeSpan ProcessCpuTime { get; }

        /// <summary>
        /// Resident memory of the own process in bytes.
        /// </summary>
        long ProcessWorkingSetBytes { get; }

        /// <summary>
        /// Private memory of the own process in bytes.
        /// </summary>
        long ProcessPrivateMemoryBytes { get; }

        /// <summary>
        /// Size of the managed heap of the own process in bytes.
        /// </summary>
        long ProcessManagedHeapBytes { get; }

        /// <summary>
        /// Number of threads of the own process.
        /// </summary>
        int ProcessThreadCount { get; }

        /// <summary>
        /// Point in time (UTC) at which the own process was started.
        /// </summary>
        DateTime ProcessStartTimeUtc { get; }

        /// <summary>
        /// Number of logical processors available to the own process.
        /// </summary>
        int ProcessorCount { get; }

        /// <summary>
        /// Current point in time (UTC).
        /// </summary>
        DateTime UtcNow { get; }
    }
}

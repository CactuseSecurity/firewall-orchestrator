namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Aggregated resource usage of one FWO service that runs on the same host as the UI. A service can consist
    /// of several processes (for example the database), all of them are summed up into one entry.
    /// </summary>
    public class ServiceUsage
    {
        /// <summary>
        /// Text key of the service name, to be translated by the caller.
        /// </summary>
        public string NameKey { get; set; } = "";

        /// <summary>
        /// Number of processes found for this service.
        /// </summary>
        public int ProcessCount { get; set; }

        /// <summary>
        /// CPU utilization of all processes of this service since the previous sample, in percent of all cores.
        /// </summary>
        public double CpuPercent { get; set; }

        /// <summary>
        /// Resident memory of all processes of this service in bytes.
        /// </summary>
        public long MemoryBytes { get; set; }

        /// <summary>
        /// Number of threads of all processes of this service.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Time the longest running process of this service has been up.
        /// </summary>
        public TimeSpan UpTime { get; set; }
    }
}

using System.Diagnostics;

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Reads the operating system counters from the Linux /proc file system and the own process.
    /// </summary>
    public class ProcSystemUsageSource : ISystemUsageSource
    {
        private const string kProcDirectory = "/proc";

        private readonly Process process = Process.GetCurrentProcess();

        /// <inheritdoc />
        public TimeSpan ProcessCpuTime => ReadProcessValue(currentProcess => currentProcess.TotalProcessorTime, TimeSpan.Zero);

        /// <inheritdoc />
        public long ProcessWorkingSetBytes => ReadProcessValue(currentProcess => currentProcess.WorkingSet64, 0L);

        /// <inheritdoc />
        public long ProcessPrivateMemoryBytes => ReadProcessValue(currentProcess => currentProcess.PrivateMemorySize64, 0L);

        /// <inheritdoc />
        public long ProcessManagedHeapBytes => GC.GetTotalMemory(false);

        /// <inheritdoc />
        public int ProcessThreadCount => ReadProcessValue(currentProcess => currentProcess.Threads.Count, 0);

        /// <inheritdoc />
        public DateTime ProcessStartTimeUtc => ReadProcessValue(currentProcess => currentProcess.StartTime.ToUniversalTime(), DateTime.UtcNow);

        /// <inheritdoc />
        public int ProcessorCount => Environment.ProcessorCount;

        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;

        /// <inheritdoc />
        public void RefreshProcessInfo()
        {
            try
            {
                process.Refresh();
            }
            catch (Exception)
            {
                // the counters are best effort only, the cached values are used instead
            }
        }

        /// <inheritdoc />
        public string? ReadProcFile(string fileName)
        {
            try
            {
                string path = Path.Combine(kProcDirectory, fileName);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                // the counters are best effort only, a missing or unreadable file must not break the caller
                return null;
            }
        }

        private T ReadProcessValue<T>(Func<Process, T> read, T fallback)
        {
            try
            {
                return read(process);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}

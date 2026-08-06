using System.Diagnostics;

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Reads the operating system counters from the Linux /proc file system and the own process.
    /// </summary>
    public sealed class ProcSystemUsageSource : ISystemUsageSource, IDisposable
    {
        private const string kProcDirectory = "/proc";

        private readonly Process process = Process.GetCurrentProcess();
        private bool disposed;

        /// <inheritdoc />
        public TimeSpan ProcessCpuTime => ReadProcessValue(currentProcess => currentProcess.TotalProcessorTime, TimeSpan.Zero);

        /// <inheritdoc />
        public long ProcessWorkingSetBytes => ReadProcessValue(currentProcess => currentProcess.WorkingSet64, 0L);

        /// <inheritdoc />
        public long ProcessPrivateMemoryBytes => ReadProcessValue(currentProcess => currentProcess.PrivateMemorySize64, 0L);

        /// <inheritdoc />
        public long ProcessManagedHeapBytes => GC.GetTotalMemory(false);

        /// <inheritdoc />
        // reading Threads builds a fresh collection on every call, which is why the collector takes this
        // value once per sample rather than per displayed field
        public int ProcessThreadCount => ReadProcessValue(currentProcess => currentProcess.Threads.Count, 0);

        /// <inheritdoc />
        public DateTime ProcessStartTimeUtc => ReadProcessValue(currentProcess => currentProcess.StartTime.ToUniversalTime(), DateTime.UtcNow);

        /// <inheritdoc />
        public int ProcessorCount => Environment.ProcessorCount;

        /// <inheritdoc />
        public int MemoryPageSizeBytes => Environment.SystemPageSize;

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
                // deliberately no File.Exists probe beforehand: a process can vanish between the check and
                // the read anyway, so the failure has to be handled here in any case, and the extra call
                // would double the file system round trips of a scan across all processes of the host
                return File.ReadAllText(Path.Combine(kProcDirectory, fileName));
            }
            catch (Exception)
            {
                // the counters are best effort only, a missing or unreadable file must not break the caller
                return null;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<int> ListProcessIds()
        {
            try
            {
                List<int> processIds = [];
                foreach (string directory in Directory.EnumerateDirectories(kProcDirectory))
                {
                    // every process owns a directory named after its id, the other entries are kernel information
                    if (int.TryParse(Path.GetFileName(directory), out int processId))
                    {
                        processIds.Add(processId);
                    }
                }
                return processIds;
            }
            catch (Exception)
            {
                // the counters are best effort only, an unreadable /proc must not break the caller
                return [];
            }
        }

        /// <summary>
        /// Releases the handle on the own process. The source lives as long as the service does, so this
        /// only runs when the dependency injection container is shut down.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                process.Dispose();
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

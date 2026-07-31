using System.Globalization;

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Builds <see cref="SystemUsageSnapshot"/>s from the Linux kernel counters and the own process.
    /// Instances are thread safe and meant to be registered as singleton.
    /// </summary>
    /// <param name="source">Source of the operating system counters.</param>
    public class SystemUsageCollector(ISystemUsageSource source) : ISystemUsageCollector
    {
        /// <summary>
        /// Minimum time between two real measurements. Deliberately a bit below the refresh interval of the
        /// monitoring page so that concurrent viewers share one sample instead of shortening each other's
        /// measuring interval.
        /// </summary>
        private static readonly TimeSpan kMinSampleInterval = TimeSpan.FromSeconds(4);

        private static readonly char[] kWhitespaceSeparators = [' ', '\t'];
        private static readonly char[] kLineSeparators = ['\n', '\r'];

        private const string kMemInfoFile = "meminfo";
        private const string kStatFile = "stat";
        private const string kLoadAvgFile = "loadavg";
        private const string kCpuLineKey = "cpu";
        private const string kMemTotalKey = "MemTotal";
        private const string kMemFreeKey = "MemFree";
        private const string kMemAvailableKey = "MemAvailable";
        private const string kSwapTotalKey = "SwapTotal";
        private const string kSwapFreeKey = "SwapFree";
        private const long kBytesPerKibibyte = 1024;
        private const int kLoadAverageFieldCount = 3;
        private const int kMinCpuFieldCount = 5;
        private const double kFullPercent = 100.0;

        private readonly object sampleLock = new();
        private readonly ServiceUsageScanner serviceScanner = new(source);

        private SystemUsageSnapshot? lastSnapshot;
        private long lastCpuBusyTicks;
        private long lastCpuTotalTicks;
        private TimeSpan lastProcessCpuTime = TimeSpan.Zero;
        private DateTime lastProcessSampleTime = DateTime.MinValue;

        /// <inheritdoc />
        public SystemUsageSnapshot Collect()
        {
            lock (sampleLock)
            {
                DateTime now = source.UtcNow;
                TimeSpan elapsedSinceLastSample = lastSnapshot == null
                    ? TimeSpan.MaxValue
                    : now - lastSnapshot.CollectedAt;
                if (lastSnapshot != null
                    && elapsedSinceLastSample >= TimeSpan.Zero
                    && elapsedSinceLastSample < kMinSampleInterval)
                {
                    return lastSnapshot;
                }

                lastSnapshot = Sample(now);
                return lastSnapshot;
            }
        }

        private SystemUsageSnapshot Sample(DateTime now)
        {
            source.RefreshProcessInfo();
            SystemUsageSnapshot snapshot = new()
            {
                CollectedAt = now,
                ProcessorCount = Math.Max(1, source.ProcessorCount),
                ProcessWorkingSetBytes = source.ProcessWorkingSetBytes,
                ProcessPrivateMemoryBytes = source.ProcessPrivateMemoryBytes,
                ProcessManagedHeapBytes = source.ProcessManagedHeapBytes,
                ProcessThreadCount = source.ProcessThreadCount,
                ProcessStartTime = source.ProcessStartTimeUtc
            };

            snapshot.ProcessCpuPercent = SampleProcessCpuPercent(now, snapshot.ProcessorCount, snapshot.ProcessStartTime);
            bool memoryRead = ApplyMemory(snapshot);
            bool cpuRead = ApplyCpu(snapshot);
            ApplyLoadAverage(snapshot);
            snapshot.Services = serviceScanner.Scan(now, snapshot.ProcessorCount, snapshot.MemoryTotalBytes);
            snapshot.SourceAvailable = memoryRead && cpuRead;
            return snapshot;
        }

        private bool ApplyMemory(SystemUsageSnapshot snapshot)
        {
            Dictionary<string, long> memInfo = ParseMemInfo(source.ReadProcFile(kMemInfoFile));
            if (!memInfo.TryGetValue(kMemTotalKey, out long memTotal) || memTotal <= 0)
            {
                return false;
            }

            memInfo.TryGetValue(kMemFreeKey, out long memFree);
            snapshot.MemoryTotalBytes = memTotal;
            snapshot.MemoryFreeBytes = memFree;
            // MemAvailable is the meaningful "free for applications" value, fall back to MemFree on old kernels
            snapshot.MemoryAvailableBytes = memInfo.TryGetValue(kMemAvailableKey, out long memAvailable) ? memAvailable : memFree;

            memInfo.TryGetValue(kSwapTotalKey, out long swapTotal);
            memInfo.TryGetValue(kSwapFreeKey, out long swapFree);
            snapshot.SwapTotalBytes = swapTotal;
            snapshot.SwapFreeBytes = swapFree;
            return true;
        }

        private bool ApplyCpu(SystemUsageSnapshot snapshot)
        {
            if (!TryParseCpuTicks(source.ReadProcFile(kStatFile), out long busyTicks, out long totalTicks))
            {
                return false;
            }

            long busyDelta = busyTicks - lastCpuBusyTicks;
            long totalDelta = totalTicks - lastCpuTotalTicks;
            if (totalDelta > 0 && busyDelta >= 0)
            {
                snapshot.CpuUsedPercent = Math.Clamp(kFullPercent * busyDelta / totalDelta, 0, kFullPercent);
            }

            lastCpuBusyTicks = busyTicks;
            lastCpuTotalTicks = totalTicks;
            return true;
        }

        private void ApplyLoadAverage(SystemUsageSnapshot snapshot)
        {
            string? content = source.ReadProcFile(kLoadAvgFile);
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            List<string> fields = [.. content.Split(kWhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries)];
            if (fields.Count < kLoadAverageFieldCount)
            {
                return;
            }

            snapshot.LoadAverage1 = ParseDouble(fields[0]);
            snapshot.LoadAverage5 = ParseDouble(fields[1]);
            snapshot.LoadAverage15 = ParseDouble(fields[2]);
        }

        private double SampleProcessCpuPercent(DateTime now, int processorCount, DateTime processStartTime)
        {
            TimeSpan processCpuTime = source.ProcessCpuTime;
            // on the very first sample there is no previous measurement, so report the average since process start
            DateTime referenceTime = lastProcessSampleTime == DateTime.MinValue ? processStartTime : lastProcessSampleTime;
            TimeSpan referenceCpuTime = lastProcessSampleTime == DateTime.MinValue ? TimeSpan.Zero : lastProcessCpuTime;

            double elapsedSeconds = (now - referenceTime).TotalSeconds;
            double cpuSeconds = (processCpuTime - referenceCpuTime).TotalSeconds;

            lastProcessCpuTime = processCpuTime;
            lastProcessSampleTime = now;

            if (elapsedSeconds <= 0 || cpuSeconds < 0)
            {
                return 0;
            }
            return Math.Clamp(kFullPercent * cpuSeconds / (elapsedSeconds * processorCount), 0, kFullPercent);
        }

        private static Dictionary<string, long> ParseMemInfo(string? content)
        {
            Dictionary<string, long> values = [];
            if (string.IsNullOrWhiteSpace(content))
            {
                return values;
            }

            foreach (string line in content.Split(kLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0)
                {
                    continue;
                }

                string key = line[..colonIndex].Trim();
                List<string> valueFields = [.. line[(colonIndex + 1)..].Split(kWhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries)];
                if (valueFields.Count == 0 || !long.TryParse(valueFields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long rawValue))
                {
                    continue;
                }

                // all sizes in /proc/meminfo are given in kB, values without a unit are plain counts
                values[key] = valueFields.Count > 1 ? rawValue * kBytesPerKibibyte : rawValue;
            }
            return values;
        }

        private static bool TryParseCpuTicks(string? content, out long busyTicks, out long totalTicks)
        {
            busyTicks = 0;
            totalTicks = 0;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            foreach (string line in content.Split(kLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                List<string> fields = [.. line.Split(kWhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries)];
                if (fields.Count < kMinCpuFieldCount || fields[0] != kCpuLineKey)
                {
                    continue;
                }
                return TryAccumulateCpuTicks(fields, out busyTicks, out totalTicks);
            }
            return false;
        }

        private static bool TryAccumulateCpuTicks(List<string> fields, out long busyTicks, out long totalTicks)
        {
            const int kIdleFieldIndex = 4;
            const int kIoWaitFieldIndex = 5;
            const int kGuestFieldIndex = 9;
            const int kGuestNiceFieldIndex = 10;

            busyTicks = 0;
            totalTicks = 0;
            for (int index = 1; index < fields.Count; index++)
            {
                if (!long.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                {
                    return false;
                }

                // Linux also includes guest and guest_nice in user and nice, so counting those fields
                // separately would double-count CPU time on virtualization hosts.
                if (index == kGuestFieldIndex || index == kGuestNiceFieldIndex)
                {
                    continue;
                }

                totalTicks += ticks;
                // idle and iowait are the only non busy states of the aggregated cpu line
                if (index != kIdleFieldIndex && index != kIoWaitFieldIndex)
                {
                    busyTicks += ticks;
                }
            }
            return totalTicks > 0;
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
        }
    }
}

using System.Diagnostics.Metrics;
using System.Globalization;

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Builds <see cref="SystemUsageSnapshot"/>s from <c>Microsoft.Extensions.Diagnostics.ResourceMonitoring</c>
    /// metrics and supplements them with Linux host data that the package does not expose directly.
    /// </summary>
    /// <param name="source">Source of the operating system counters.</param>
    public sealed class ResourceMonitoringSystemUsageProvider : ISystemUsageSnapshotProvider, IDisposable
    {
        private static readonly TimeSpan kMinSampleInterval = TimeSpan.FromSeconds(4);

        private static readonly char[] kWhitespaceSeparators = [' ', '\t'];
        private static readonly char[] kLineSeparators = ['\n', '\r'];
        private static readonly HashSet<string> kObservedInstruments =
        [
            "process.cpu.utilization",
            "container.memory.usage",
            "dotnet.process.memory.virtual.utilization",
            "container.memory.limit.utilization"
        ];

        private const string kResourceMonitoringMeterName = "Microsoft.Extensions.Diagnostics.ResourceMonitoring";
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

        private readonly ISystemUsageSource source;
        private readonly object sampleLock = new();
        private readonly ServiceUsageScanner serviceScanner;
        private readonly MeterListener meterListener = new();

        private SystemUsageSnapshot? lastSnapshot;
        private long lastCpuBusyTicks;
        private long lastCpuTotalTicks;
        private TimeSpan lastProcessCpuTime = TimeSpan.Zero;
        private DateTime lastProcessSampleTime = DateTime.MinValue;
        private double? latestProcessCpuUtilization;
        private double? latestContainerMemoryUsageBytes;
        private double? latestProcessMemoryUtilization;
        private double? latestContainerMemoryLimitUtilization;
        private bool disposed;

        /// <summary>
        /// Starts listening to the resource monitoring metrics published by .NET.
        /// </summary>
        public ResourceMonitoringSystemUsageProvider(ISystemUsageSource source)
        {
            this.source = source;
            serviceScanner = new(source);
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == kResourceMonitoringMeterName
                    && kObservedInstruments.Contains(instrument.Name))
                {
                    listener.EnableMeasurementEvents(instrument, null);
                }
            };
            meterListener.SetMeasurementEventCallback<double>(OnDoubleMeasurement);
            meterListener.SetMeasurementEventCallback<float>((instrument, value, _, __) =>
                OnDoubleMeasurement(instrument, value, default, default));
            meterListener.SetMeasurementEventCallback<long>((instrument, value, _, __) =>
                OnDoubleMeasurement(instrument, value, default, default));
            meterListener.SetMeasurementEventCallback<int>((instrument, value, _, __) =>
                OnDoubleMeasurement(instrument, value, default, default));
            meterListener.Start();
        }

        /// <inheritdoc />
        public SystemUsageSnapshot Collect()
        {
            lock (sampleLock)
            {
                meterListener.RecordObservableInstruments();

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

        /// <summary>
        /// Releases the metric listener when the dependency injection container shuts down.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                meterListener.Dispose();
            }
        }

        private void OnDoubleMeasurement(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> _, object? __)
        {
            if (instrument.Meter.Name != kResourceMonitoringMeterName)
            {
                return;
            }

            switch (instrument.Name)
            {
                case "process.cpu.utilization":
                    latestProcessCpuUtilization = value;
                    break;
                case "container.memory.usage":
                    latestContainerMemoryUsageBytes = value;
                    break;
                case "dotnet.process.memory.virtual.utilization":
                    latestProcessMemoryUtilization = value;
                    break;
                case "container.memory.limit.utilization":
                    latestContainerMemoryLimitUtilization = value;
                    break;
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

            ApplyProcessMetrics(snapshot, now);
            bool memoryRead = ApplyMemory(snapshot);
            ApplyResourceMonitoringMemory(snapshot);
            bool cpuRead = ApplyCpu(snapshot);
            ApplyLoadAverage(snapshot);
            snapshot.Services = serviceScanner.Scan(now, snapshot.ProcessorCount, snapshot.MemoryTotalBytes);
            snapshot.ServicesVisible = serviceScanner.ForeignProcessesVisible();
            snapshot.SourceAvailable = memoryRead && cpuRead;
            return snapshot;
        }

        private void ApplyProcessMetrics(SystemUsageSnapshot snapshot, DateTime now)
        {
            snapshot.ProcessCpuPercent = latestProcessCpuUtilization.HasValue
                ? Math.Clamp(latestProcessCpuUtilization.Value * kFullPercent, 0, kFullPercent)
                : SampleProcessCpuPercent(now, snapshot.ProcessorCount, snapshot.ProcessStartTime);

            if (latestContainerMemoryUsageBytes.HasValue)
            {
                long processMemoryBytes = Math.Max(0, (long)latestContainerMemoryUsageBytes.Value);
                if (snapshot.ProcessWorkingSetBytes <= 0)
                {
                    snapshot.ProcessWorkingSetBytes = processMemoryBytes;
                }

                if (snapshot.ProcessPrivateMemoryBytes <= 0)
                {
                    snapshot.ProcessPrivateMemoryBytes = processMemoryBytes;
                }
            }
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
            snapshot.MemoryAvailableBytes = memInfo.TryGetValue(kMemAvailableKey, out long memAvailable) ? memAvailable : memFree;

            memInfo.TryGetValue(kSwapTotalKey, out long swapTotal);
            memInfo.TryGetValue(kSwapFreeKey, out long swapFree);
            snapshot.SwapTotalBytes = swapTotal;
            snapshot.SwapFreeBytes = swapFree;
            return true;
        }

        private void ApplyResourceMonitoringMemory(SystemUsageSnapshot snapshot)
        {
            if (latestContainerMemoryUsageBytes.HasValue)
            {
                long usedBytes = Math.Max(0, (long)latestContainerMemoryUsageBytes.Value);
                long totalBytes = snapshot.MemoryTotalBytes;

                if (totalBytes <= 0 && latestContainerMemoryLimitUtilization is > 0)
                {
                    totalBytes = (long)Math.Round(usedBytes / latestContainerMemoryLimitUtilization.Value);
                }

                if (totalBytes > 0)
                {
                    snapshot.MemoryTotalBytes = totalBytes;
                    snapshot.MemoryAvailableBytes = Math.Max(0, totalBytes - usedBytes);
                    snapshot.MemoryFreeBytes = Math.Min(snapshot.MemoryFreeBytes, snapshot.MemoryAvailableBytes);
                }
            }
            else if (latestProcessMemoryUtilization.HasValue && snapshot.MemoryTotalBytes > 0)
            {
                double utilization = Math.Clamp(latestProcessMemoryUtilization.Value, 0, 1);
                long usedBytes = (long)Math.Round(snapshot.MemoryTotalBytes * utilization);
                snapshot.MemoryAvailableBytes = Math.Max(0, snapshot.MemoryTotalBytes - usedBytes);
                snapshot.MemoryFreeBytes = Math.Min(snapshot.MemoryFreeBytes, snapshot.MemoryAvailableBytes);
            }
        }

        private bool ApplyCpu(SystemUsageSnapshot snapshot)
        {
            if (!TryParseCpuTicks(source.ReadProcFile(kStatFile), out long busyTicks, out long totalTicks))
            {
                return false;
            }

            if (lastCpuTotalTicks == 0 && totalTicks > 0)
            {
                // On the first sample there is no previous delta yet. Use the aggregate share seen so far
                // so the UI shows a meaningful system value immediately instead of a guaranteed zero.
                snapshot.CpuUsedPercent = Math.Clamp(kFullPercent * busyTicks / totalTicks, 0, kFullPercent);
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

                if (index == kGuestFieldIndex || index == kGuestNiceFieldIndex)
                {
                    continue;
                }

                totalTicks += ticks;
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

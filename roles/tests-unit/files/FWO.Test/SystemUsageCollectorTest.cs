using FWO.Services.SystemUsage;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class SystemUsageCollectorTest
    {
        private const long kKibiByte = 1024;

        private const string kMemInfo =
            "MemTotal:       16000000 kB\n" +
            "MemFree:         2000000 kB\n" +
            "MemAvailable:    8000000 kB\n" +
            "Buffers:          100000 kB\n" +
            "SwapTotal:       4000000 kB\n" +
            "SwapFree:        3000000 kB\n";

        // user nice system idle iowait irq softirq steal -> busy = total - idle - iowait
        private const string kStatFirstSample =
            "cpu  1000 0 500 8000 500 0 0 0\n" +
            "cpu0 500 0 250 4000 250 0 0 0\n" +
            "intr 12345\n";

        private const string kStatSecondSample =
            "cpu  1200 0 600 8700 500 0 0 0\n" +
            "cpu0 600 0 300 4350 250 0 0 0\n" +
            "intr 12999\n";

        private const string kStatGuestFirstSample = "cpu  1000 0 500 8000 500 0 0 0 200 100\n";
        private const string kStatGuestSecondSample = "cpu  1100 0 500 8900 500 0 0 0 300 100\n";

        private const string kLoadAvg = "0.52 1.25 2.00 2/1234 5678\n";

        private static FakeSystemUsageSource CreateSource()
        {
            return new FakeSystemUsageSource
            {
                MemInfo = kMemInfo,
                Stat = kStatFirstSample,
                LoadAvg = kLoadAvg,
                ProcessorCount = 4,
                ProcessWorkingSetBytes = 400 * kKibiByte * kKibiByte,
                ProcessPrivateMemoryBytes = 500 * kKibiByte * kKibiByte,
                ProcessManagedHeapBytes = 120 * kKibiByte * kKibiByte,
                ProcessThreadCount = 42,
                ProcessStartTimeUtc = new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc),
                UtcNow = new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc),
                ProcessCpuTime = TimeSpan.FromSeconds(60)
            };
        }

        [Test]
        public void Collect_ParsesMemoryValues()
        {
            SystemUsageCollector collector = new(CreateSource());

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceAvailable, Is.True);
                Assert.That(snapshot.MemoryTotalBytes, Is.EqualTo(16000000 * kKibiByte));
                Assert.That(snapshot.MemoryFreeBytes, Is.EqualTo(2000000 * kKibiByte));
                Assert.That(snapshot.MemoryAvailableBytes, Is.EqualTo(8000000 * kKibiByte));
                Assert.That(snapshot.MemoryUsedBytes, Is.EqualTo(8000000 * kKibiByte));
                Assert.That(snapshot.MemoryUsedPercent, Is.EqualTo(50).Within(0.01));
                Assert.That(snapshot.SwapTotalBytes, Is.EqualTo(4000000 * kKibiByte));
                Assert.That(snapshot.SwapUsedBytes, Is.EqualTo(1000000 * kKibiByte));
                Assert.That(snapshot.SwapUsedPercent, Is.EqualTo(25).Within(0.01));
            });
        }

        [Test]
        public void Collect_FallsBackToMemFreeWithoutMemAvailable()
        {
            FakeSystemUsageSource source = CreateSource();
            source.MemInfo = "MemTotal:       16000000 kB\nMemFree:         2000000 kB\n";
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.That(snapshot.MemoryAvailableBytes, Is.EqualTo(2000000 * kKibiByte));
        }

        [Test]
        public void Collect_IncludesTheServicesRunningOnThisHost()
        {
            FakeSystemUsageSource source = CreateSource();
            source.ProcFiles["uptime"] = "1000.00 3900.00\n";
            source.ProcessIds.Add(101);
            source.ProcFiles["101/stat"] = "101 (postgres) S 1 1 1 0 -1 4194560 1000 0 0 0 400 200 0 0 20 0 3 0 30000 1 2\n";
            source.ProcFiles["101/statm"] = "20000 1000 500 100 0 3000 0\n";
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Services, Has.Count.EqualTo(1));
                Assert.That(snapshot.Services[0].NameKey, Is.EqualTo("database"));
                Assert.That(snapshot.Services[0].ThreadCount, Is.EqualTo(3));
                // the memory share is related to the total memory of the same sample: 1000 pages of 16000000 kB
                Assert.That(snapshot.Services[0].MemoryPercent, Is.EqualTo(0.025).Within(0.0001));
            });
        }

        [Test]
        public void Collect_WithoutOtherServicesLeavesTheServiceListEmpty()
        {
            SystemUsageCollector collector = new(CreateSource());

            Assert.That(collector.Collect().Services, Is.Empty);
        }

        [Test]
        public void Collect_ParsesLoadAverageAndProcessValues()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.LoadAverage1, Is.EqualTo(0.52).Within(0.001));
                Assert.That(snapshot.LoadAverage5, Is.EqualTo(1.25).Within(0.001));
                Assert.That(snapshot.LoadAverage15, Is.EqualTo(2.00).Within(0.001));
                Assert.That(snapshot.ProcessorCount, Is.EqualTo(4));
                Assert.That(snapshot.ProcessThreadCount, Is.EqualTo(42));
                Assert.That(snapshot.ProcessWorkingSetBytes, Is.EqualTo(source.ProcessWorkingSetBytes));
                Assert.That(snapshot.ProcessManagedHeapBytes, Is.EqualTo(source.ProcessManagedHeapBytes));
                Assert.That(snapshot.ProcessUpTime, Is.EqualTo(TimeSpan.FromHours(2)));
            });
        }

        [Test]
        public void Collect_FirstSampleReportsProcessCpuSinceProcessStart()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            // 60 s cpu time in 7200 s wall time on 4 cores -> 60 / (7200 * 4) = 0.208 %
            Assert.That(snapshot.ProcessCpuPercent, Is.EqualTo(0.2083).Within(0.001));
        }

        [Test]
        public void Collect_SecondSampleUsesDeltaOfBothCpuCounters()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);
            collector.Collect();

            source.UtcNow = source.UtcNow.AddSeconds(10);
            source.Stat = kStatSecondSample;
            source.ProcessCpuTime = TimeSpan.FromSeconds(64);
            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                // busy delta 300, total delta 1000 -> 30 %
                Assert.That(snapshot.CpuUsedPercent, Is.EqualTo(30).Within(0.01));
                // 4 s cpu time in 10 s on 4 cores -> 10 %
                Assert.That(snapshot.ProcessCpuPercent, Is.EqualTo(10).Within(0.01));
            });
        }

        [Test]
        public void Collect_ReusesSnapshotWithinCachingInterval()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);
            SystemUsageSnapshot first = collector.Collect();

            source.UtcNow = source.UtcNow.AddSeconds(1);
            source.Stat = kStatSecondSample;
            SystemUsageSnapshot second = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.SameAs(first));
                Assert.That(source.MemInfoReadCount, Is.EqualTo(1));
                // the process counters are read once per sample, not once per value
                Assert.That(source.RefreshProcessInfoCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void Collect_SamplesAgainAfterCachingInterval()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);
            SystemUsageSnapshot first = collector.Collect();

            source.UtcNow = source.UtcNow.AddSeconds(5);
            SystemUsageSnapshot second = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(source.MemInfoReadCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void Collect_SamplesAgainWhenClockMovesBackwards()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);
            SystemUsageSnapshot first = collector.Collect();

            source.UtcNow = source.UtcNow.AddMinutes(-5);
            SystemUsageSnapshot second = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(source.MemInfoReadCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void Collect_DoesNotDoubleCountGuestCpuTicks()
        {
            FakeSystemUsageSource source = CreateSource();
            source.Stat = kStatGuestFirstSample;
            SystemUsageCollector collector = new(source);
            collector.Collect();

            source.UtcNow = source.UtcNow.AddSeconds(10);
            source.Stat = kStatGuestSecondSample;
            SystemUsageSnapshot snapshot = collector.Collect();

            // guest delta is already included in user: busy delta 100, total delta 1000 -> 10 %
            Assert.That(snapshot.CpuUsedPercent, Is.EqualTo(10).Within(0.01));
        }

        [Test]
        public void Collect_WithoutProcFilesReportsSourceUnavailable()
        {
            FakeSystemUsageSource source = CreateSource();
            source.MemInfo = null;
            source.Stat = null;
            source.LoadAvg = null;
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceAvailable, Is.False);
                Assert.That(snapshot.MemoryTotalBytes, Is.EqualTo(0));
                Assert.That(snapshot.MemoryUsedPercent, Is.EqualTo(0));
                Assert.That(snapshot.CpuUsedPercent, Is.EqualTo(0));
                Assert.That(snapshot.LoadAverage1, Is.EqualTo(0));
            });
        }

        [Test]
        public void Collect_WithGarbageContentReportsSourceUnavailable()
        {
            FakeSystemUsageSource source = CreateSource();
            source.MemInfo = "this is not meminfo";
            source.Stat = "neither is this";
            source.LoadAvg = "nor this";
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceAvailable, Is.False);
                Assert.That(snapshot.MemoryTotalBytes, Is.EqualTo(0));
                Assert.That(snapshot.LoadAverage1, Is.EqualTo(0));
            });
        }

        [Test]
        public void Collect_WithUnparsableCpuTicksReportsSourceUnavailable()
        {
            FakeSystemUsageSource source = CreateSource();
            source.Stat = "cpu  1000 0 xxx 8000 500 0 0 0\n";
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceAvailable, Is.False);
                // the memory values are still usable
                Assert.That(snapshot.MemoryTotalBytes, Is.EqualTo(16000000 * kKibiByte));
            });
        }

        [Test]
        public void Collect_WithSingleProcessorNeverExceedsFullPercent()
        {
            FakeSystemUsageSource source = CreateSource();
            source.ProcessorCount = 0;
            source.ProcessCpuTime = TimeSpan.FromHours(100);
            SystemUsageCollector collector = new(source);

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ProcessorCount, Is.EqualTo(1));
                Assert.That(snapshot.ProcessCpuPercent, Is.EqualTo(100));
            });
        }

        [Test]
        public void Collect_WithCountersResetDoesNotReportNegativeCpu()
        {
            FakeSystemUsageSource source = CreateSource();
            SystemUsageCollector collector = new(source);
            collector.Collect();

            source.UtcNow = source.UtcNow.AddSeconds(10);
            source.Stat = "cpu  10 0 5 80 5 0 0 0\n";
            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.That(snapshot.CpuUsedPercent, Is.EqualTo(0));
        }
    }

    internal sealed class FakeSystemUsageSource : ISystemUsageSource
    {
        public string? MemInfo { get; set; }
        public string? Stat { get; set; }
        public string? LoadAvg { get; set; }
        public int MemInfoReadCount { get; private set; }
        public int RefreshProcessInfoCount { get; private set; }

        /// <summary>
        /// Contents of the remaining files below /proc, keyed by their path, e.g. "42/stat".
        /// </summary>
        public Dictionary<string, string> ProcFiles { get; } = [];

        public List<int> ProcessIds { get; } = [];
        public int MemoryPageSizeBytes { get; set; } = 4096;

        public TimeSpan ProcessCpuTime { get; set; }
        public long ProcessWorkingSetBytes { get; set; }
        public long ProcessPrivateMemoryBytes { get; set; }
        public long ProcessManagedHeapBytes { get; set; }
        public int ProcessThreadCount { get; set; }
        public DateTime ProcessStartTimeUtc { get; set; }
        public int ProcessorCount { get; set; }
        public DateTime UtcNow { get; set; }

        public void RefreshProcessInfo()
        {
            RefreshProcessInfoCount++;
        }

        public string? ReadProcFile(string fileName)
        {
            switch (fileName)
            {
                case "meminfo":
                    MemInfoReadCount++;
                    return MemInfo;
                case "stat":
                    return Stat;
                case "loadavg":
                    return LoadAvg;
                default:
                    return ProcFiles.TryGetValue(fileName, out string? content) ? content : null;
            }
        }

        public IReadOnlyList<int> ListProcessIds()
        {
            return ProcessIds;
        }
    }
}

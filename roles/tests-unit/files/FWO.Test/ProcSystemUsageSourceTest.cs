using FWO.Services.SystemUsage;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ProcSystemUsageSourceTest
    {
        [Test]
        public void ReadProcFile_ReturnsKernelCountersOnLinux()
        {
            ProcSystemUsageSource source = new();

            string? memInfo = source.ReadProcFile("meminfo");
            string? stat = source.ReadProcFile("stat");
            string? loadAvg = source.ReadProcFile("loadavg");

            if (OperatingSystem.IsLinux())
            {
                Assert.Multiple(() =>
                {
                    Assert.That(memInfo, Does.Contain("MemTotal"));
                    Assert.That(stat, Does.StartWith("cpu"));
                    Assert.That(loadAvg, Is.Not.Null.And.Not.Empty);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(memInfo, Is.Null);
                    Assert.That(stat, Is.Null);
                    Assert.That(loadAvg, Is.Null);
                });
            }
        }

        [Test]
        public void ReadProcFile_WithUnknownFileReturnsNull()
        {
            ProcSystemUsageSource source = new();

            Assert.That(source.ReadProcFile("no_such_counter_file"), Is.Null);
        }

        [Test]
        public void ProcessValues_AreReadFromTheOwnProcess()
        {
            ProcSystemUsageSource source = new();

            source.RefreshProcessInfo();

            Assert.Multiple(() =>
            {
                Assert.That(source.ProcessCpuTime, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(source.ProcessWorkingSetBytes, Is.GreaterThan(0));
                Assert.That(source.ProcessPrivateMemoryBytes, Is.GreaterThan(0));
                Assert.That(source.ProcessManagedHeapBytes, Is.GreaterThan(0));
                Assert.That(source.ProcessThreadCount, Is.GreaterThan(0));
                Assert.That(source.ProcessStartTimeUtc, Is.LessThanOrEqualTo(DateTime.UtcNow));
                Assert.That(source.ProcessorCount, Is.GreaterThan(0));
                Assert.That(source.UtcNow, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
            });
        }

        [Test]
        public void ListProcessIds_ReturnsTheRunningProcessesOnLinux()
        {
            ProcSystemUsageSource source = new();

            IReadOnlyList<int> processIds = source.ListProcessIds();

            Assert.Multiple(() =>
            {
                Assert.That(source.MemoryPageSizeBytes, Is.GreaterThan(0));
                if (OperatingSystem.IsLinux())
                {
                    // the test process itself is always among them and its stat file has to be readable
                    Assert.That(processIds, Does.Contain(Environment.ProcessId));
                    Assert.That(source.ReadProcFile($"{Environment.ProcessId}/stat"), Is.Not.Null.And.Not.Empty);
                }
                else
                {
                    Assert.That(processIds, Is.Empty);
                }
            });
        }

        [Test]
        public void Collector_WorksWithTheRealSource()
        {
            SystemUsageCollector collector = new(new ProcSystemUsageSource());

            SystemUsageSnapshot snapshot = collector.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceAvailable, Is.EqualTo(OperatingSystem.IsLinux()));
                Assert.That(snapshot.ProcessWorkingSetBytes, Is.GreaterThan(0));
                Assert.That(snapshot.ProcessCpuPercent, Is.InRange(0, 100));
                if (OperatingSystem.IsLinux())
                {
                    Assert.That(snapshot.MemoryTotalBytes, Is.GreaterThan(0));
                    Assert.That(snapshot.MemoryUsedPercent, Is.InRange(0, 100));
                    Assert.That(snapshot.CpuUsedPercent, Is.InRange(0, 100));
                }
            });
        }
    }
}

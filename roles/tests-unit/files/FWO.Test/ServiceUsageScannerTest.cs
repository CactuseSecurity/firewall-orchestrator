using FWO.Services.SystemUsage;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ServiceUsageScannerTest
    {
        private const int kPageSize = 4096;
        private const int kProcessorCount = 4;
        private const long kMemoryTotalBytes = 100000L * kPageSize;
        private const double kClockTicksPerSecond = 100;
        private const string kMiddlewareKey = "middleware";
        private const string kImporterKey = "importer";
        private const string kApiKey = "hasura_api";
        private const string kDatabaseKey = "database";
        private const string kLdapKey = "ldap_server";

        // the system has been up for 1000 seconds when the first sample is taken
        private const string kUpTime = "1000.00 3900.00\n";

        // the stat file of the init process, readable only while processes of other users are visible
        private const string kInitProcessStatFile = "1/stat";

        private static readonly DateTime kFirstSampleTime = new(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Builds the stat file of one process. The values are placed at the field positions the kernel uses,
        /// the fields the scanner does not read are filled with plausible constants.
        /// </summary>
        /// <param name="processId">Id of the process.</param>
        /// <param name="executableName">Executable name as reported in brackets, cut off after 15 characters.</param>
        /// <param name="userTicks">CPU ticks spent in user mode.</param>
        /// <param name="systemTicks">CPU ticks spent in kernel mode.</param>
        /// <param name="threadCount">Number of threads of the process.</param>
        /// <param name="startTicks">Start time of the process in ticks since boot.</param>
        /// <returns>The content of the stat file.</returns>
        private static string BuildStat(int processId, string executableName, long userTicks, long systemTicks,
            int threadCount, long startTicks)
        {
            return $"{processId} ({executableName}) S 1 1 1 0 -1 4194560 1000 0 0 0 "
                + $"{userTicks} {systemTicks} 0 0 20 0 {threadCount} 0 {startTicks} 123456 789\n";
        }

        private static string BuildStatm(long residentPages)
        {
            return $"20000 {residentPages} 500 100 0 3000 0\n";
        }

        private static string BuildCommandLine(params string[] arguments)
        {
            return string.Join('\0', arguments) + '\0';
        }

        private static void AddProcess(FakeSystemUsageSource source, int processId, string stat, long residentPages,
            string commandLine)
        {
            source.ProcessIds.Add(processId);
            source.ProcFiles[$"{processId}/stat"] = stat;
            source.ProcFiles[$"{processId}/statm"] = BuildStatm(residentPages);
            source.ProcFiles[$"{processId}/cmdline"] = commandLine;
        }

        /// <summary>
        /// Builds a source holding one process per service, plus processes that must not be counted.
        /// </summary>
        /// <returns>The prepared source.</returns>
        private static FakeSystemUsageSource CreateSource()
        {
            FakeSystemUsageSource source = new()
            {
                UtcNow = kFirstSampleTime,
                ProcessorCount = kProcessorCount,
                MemoryPageSizeBytes = kPageSize
            };
            source.ProcFiles["uptime"] = kUpTime;
            source.ProcFiles[kInitProcessStatFile] = BuildStat(1, "systemd", 10, 10, 1, 0);

            // the middleware is only recognizable by its command line, the kernel cuts its name off after 15 characters
            AddProcess(source, 101, BuildStat(101, "FWO.Middleware.", 100, 50, 20, 40000), 1000,
                BuildCommandLine("/usr/local/fworch/middleware/bin/Release/net10.0/FWO.Middleware.Server"));
            AddProcess(source, 102, BuildStat(102, "python3", 200, 100, 5, 50000), 2000,
                BuildCommandLine("/usr/local/fworch/importer/.venv/bin/python", "/usr/local/fworch/importer/import_main_loop.py"));
            AddProcess(source, 103, BuildStat(103, "graphql-engine", 300, 100, 12, 60000), 3000,
                BuildCommandLine("/bin/graphql-engine", "serve"));
            AddProcess(source, 104, BuildStat(104, "postgres", 400, 200, 1, 30000), 4000,
                BuildCommandLine("/usr/lib/postgresql/16/bin/postgres", "-D", "/var/lib/postgresql/16/main"));
            AddProcess(source, 105, BuildStat(105, "postgres", 100, 100, 1, 35000), 500,
                BuildCommandLine("postgres:", "16/main:", "checkpointer"));
            AddProcess(source, 106, BuildStat(106, "slapd", 50, 50, 4, 20000), 1500,
                BuildCommandLine("/usr/sbin/slapd", "-h", "ldap:/// ldapi:///", "-g", "openldap"));
            // an unrelated process that only mentions a service on its command line
            AddProcess(source, 107, BuildStat(107, "systemctl", 900, 900, 1, 10000), 9000,
                BuildCommandLine("/usr/bin/systemctl", "status", "postgres"));
            return source;
        }

        private static ServiceUsage FindService(List<ServiceUsage> services, string nameKey)
        {
            return services.Find(service => service.NameKey == nameKey)
                ?? throw new AssertionException($"service {nameKey} was not found");
        }

        [Test]
        public void Scan_FindsAllServicesRunningOnThisHost()
        {
            ServiceUsageScanner scanner = new(CreateSource());

            List<ServiceUsage> services = scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            Assert.Multiple(() =>
            {
                Assert.That(services, Has.Count.EqualTo(5));
                Assert.That(services.ConvertAll(service => service.NameKey),
                    Is.EqualTo(new List<string> { kMiddlewareKey, kImporterKey, kApiKey, kDatabaseKey, kLdapKey }));
                Assert.That(FindService(services, kMiddlewareKey).MemoryBytes, Is.EqualTo(1000 * kPageSize));
                Assert.That(FindService(services, kMiddlewareKey).ThreadCount, Is.EqualTo(20));
                Assert.That(FindService(services, kImporterKey).ProcessCount, Is.EqualTo(1));
                Assert.That(FindService(services, kApiKey).MemoryBytes, Is.EqualTo(3000 * kPageSize));
                Assert.That(FindService(services, kLdapKey).ThreadCount, Is.EqualTo(4));
            });
        }

        [Test]
        public void Scan_SumsUpAllProcessesOfOneService()
        {
            ServiceUsageScanner scanner = new(CreateSource());

            ServiceUsage database = FindService(scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes), kDatabaseKey);

            Assert.Multiple(() =>
            {
                // the two database processes are counted together, the systemctl command is not one of them
                Assert.That(database.ProcessCount, Is.EqualTo(2));
                Assert.That(database.MemoryBytes, Is.EqualTo(4500 * kPageSize));
                // 4500 of the 100000 memory pages of the system
                Assert.That(database.MemoryPercent, Is.EqualTo(4.5).Within(0.001));
                Assert.That(database.ThreadCount, Is.EqualTo(2));
                // the uptime is the one of the oldest process: 1000 seconds up minus 300 seconds until its start
                Assert.That(database.UpTime, Is.EqualTo(TimeSpan.FromSeconds(700)));
            });
        }

        [Test]
        public void Scan_IgnoresProcessesOnlyMentioningAServiceOnTheCommandLine()
        {
            FakeSystemUsageSource source = CreateSource();
            ServiceUsageScanner scanner = new(source);

            List<ServiceUsage> services = scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            // the systemctl process would add 9000 pages and 1800 ticks if it were counted as database
            Assert.That(FindService(services, kDatabaseKey).MemoryBytes, Is.EqualTo(4500 * kPageSize));
        }

        [Test]
        public void Scan_ReportsAverageSinceServiceStartOnFirstSample()
        {
            ServiceUsageScanner scanner = new(CreateSource());

            ServiceUsage middleware = FindService(scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes), kMiddlewareKey);

            // 150 ticks of cpu time in 600 seconds of uptime on four cores
            double expectedPercent = 100.0 * (150 / kClockTicksPerSecond) / (600 * kProcessorCount);
            Assert.That(middleware.CpuPercent, Is.EqualTo(expectedPercent).Within(0.001));
        }

        [Test]
        public void Scan_ReportsCpuUsageBetweenTwoSamplesAcrossAllCores()
        {
            FakeSystemUsageSource source = CreateSource();
            ServiceUsageScanner scanner = new(source);
            scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            // the middleware consumed two full seconds of cpu time during the ten seconds until the next sample
            source.ProcFiles["101/stat"] = BuildStat(101, "FWO.Middleware.", 250, 100, 20, 40000);
            ServiceUsage middleware = FindService(scanner.Scan(kFirstSampleTime.AddSeconds(10), kProcessorCount, kMemoryTotalBytes), kMiddlewareKey);

            // two seconds of cpu time in ten seconds is half a core, which is an eighth of the four cores
            Assert.That(middleware.CpuPercent, Is.EqualTo(5).Within(0.001));
        }

        [Test]
        public void Scan_WithRestartedServiceDoesNotReportNegativeCpu()
        {
            FakeSystemUsageSource source = CreateSource();
            ServiceUsageScanner scanner = new(source);
            scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            // the service was restarted and starts counting its cpu time from the beginning again
            source.ProcFiles["101/stat"] = BuildStat(101, "FWO.Middleware.", 10, 5, 20, 99000);
            ServiceUsage middleware = FindService(scanner.Scan(kFirstSampleTime.AddSeconds(10), kProcessorCount, kMemoryTotalBytes), kMiddlewareKey);

            Assert.That(middleware.CpuPercent, Is.EqualTo(0));
        }

        [Test]
        public void Scan_WithoutAnyKnownServiceReturnsEmptyList()
        {
            FakeSystemUsageSource source = new() { ProcessorCount = kProcessorCount, MemoryPageSizeBytes = kPageSize };
            source.ProcFiles["uptime"] = kUpTime;
            AddProcess(source, 201, BuildStat(201, "sshd", 10, 10, 1, 1000), 100, BuildCommandLine("/usr/sbin/sshd", "-D"));
            ServiceUsageScanner scanner = new(source);

            Assert.That(scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes), Is.Empty);
        }

        [Test]
        public void Scan_WithUnreadableProcessFilesSkipsTheProcess()
        {
            FakeSystemUsageSource source = CreateSource();
            // a process that ended between listing and reading leaves no readable files behind
            source.ProcessIds.Add(999);
            source.ProcFiles["104/stat"] = "104 (postgres) S 1 1\n";
            ServiceUsageScanner scanner = new(source);

            List<ServiceUsage> services = scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            Assert.Multiple(() =>
            {
                Assert.That(services, Has.Count.EqualTo(5));
                // only the second database process could be read
                Assert.That(FindService(services, kDatabaseKey).ProcessCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void Scan_WithoutKnownTotalMemoryReportsNoMemoryShare()
        {
            ServiceUsageScanner scanner = new(CreateSource());

            ServiceUsage middleware = FindService(scanner.Scan(kFirstSampleTime, kProcessorCount, 0), kMiddlewareKey);

            Assert.Multiple(() =>
            {
                Assert.That(middleware.MemoryPercent, Is.EqualTo(0));
                Assert.That(middleware.MemoryBytes, Is.EqualTo(1000 * kPageSize));
            });
        }

        [Test]
        public void Scan_DoesNotReadTheCommandLineOfEveryProcess()
        {
            FakeSystemUsageSource source = CreateSource();
            ServiceUsageScanner scanner = new(source);

            scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            Assert.Multiple(() =>
            {
                // an ordinary short executable name cannot hide a service, so its command line stays untouched
                Assert.That(source.ReadProcFileNames, Does.Not.Contain("107/cmdline"));
                // a name cut off by the kernel and an interpreter still have to be looked at
                Assert.That(source.ReadProcFileNames, Does.Contain("101/cmdline"));
                Assert.That(source.ReadProcFileNames, Does.Contain("102/cmdline"));
            });
        }

        [TestCase("python")]
        [TestCase("python3")]
        [TestCase("python3.11")]
        public void Scan_FindsTheImporterBehindAnyPythonVersion(string interpreterName)
        {
            FakeSystemUsageSource source = new() { ProcessorCount = kProcessorCount, MemoryPageSizeBytes = kPageSize };
            source.ProcFiles["uptime"] = kUpTime;
            AddProcess(source, 302, BuildStat(302, interpreterName, 200, 100, 5, 50000), 2000,
                BuildCommandLine($"/usr/local/fworch/importer/.venv/bin/{interpreterName}",
                    "/usr/local/fworch/importer/import_main_loop.py"));
            ServiceUsageScanner scanner = new(source);

            List<ServiceUsage> services = scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            Assert.That(FindService(services, kImporterKey).ProcessCount, Is.EqualTo(1));
        }

        [Test]
        public void Scan_FindsAServiceBehindALongExecutableName()
        {
            FakeSystemUsageSource source = new() { ProcessorCount = kProcessorCount, MemoryPageSizeBytes = kPageSize };
            source.ProcFiles["uptime"] = kUpTime;
            // the kernel reports exactly 15 characters, the full name is only on the command line
            AddProcess(source, 301, BuildStat(301, "FWO.Middleware.", 100, 50, 20, 40000), 1000,
                BuildCommandLine("/usr/local/fworch/middleware/bin/Release/net10.0/FWO.Middleware.Server"));
            ServiceUsageScanner scanner = new(source);

            List<ServiceUsage> services = scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes);

            Assert.That(FindService(services, kMiddlewareKey).MemoryBytes, Is.EqualTo(1000 * kPageSize));
        }

        [Test]
        public void ForeignProcessesVisible_WithReadableInitProcess()
        {
            ServiceUsageScanner scanner = new(CreateSource());

            Assert.That(scanner.ForeignProcessesVisible(), Is.True);
        }

        [Test]
        public void ForeignProcessesVisible_WithHiddenInitProcessReportsThemInvisible()
        {
            FakeSystemUsageSource source = CreateSource();
            // a /proc mounted with hidepid only shows the own processes, the init process of root is gone
            source.ProcFiles.Remove(kInitProcessStatFile);
            ServiceUsageScanner scanner = new(source);

            Assert.That(scanner.ForeignProcessesVisible(), Is.False);
        }

        [Test]
        public void Scan_WithoutUpTimeReportsNoUpTimeAndNoCpuUsage()
        {
            FakeSystemUsageSource source = CreateSource();
            source.ProcFiles.Remove("uptime");
            ServiceUsageScanner scanner = new(source);

            ServiceUsage middleware = FindService(scanner.Scan(kFirstSampleTime, kProcessorCount, kMemoryTotalBytes), kMiddlewareKey);

            Assert.Multiple(() =>
            {
                Assert.That(middleware.UpTime, Is.EqualTo(TimeSpan.Zero));
                Assert.That(middleware.CpuPercent, Is.EqualTo(0));
                // the memory share does not depend on the uptime and is still known
                Assert.That(middleware.MemoryPercent, Is.EqualTo(1).Within(0.001));
            });
        }
    }
}

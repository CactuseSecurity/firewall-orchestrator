using System.Globalization;

namespace FWO.Services.SystemUsage
{
    /// <summary>
    /// Finds the FWO services that run on the same host as the UI and aggregates their resource usage.
    /// A service is recognized by the name of its executable or by an argument of its command line, which
    /// covers the services started by systemd as well as the API running inside a container. Services that
    /// are not found on this host are left out, they usually run on another machine of the installation.
    /// </summary>
    /// <param name="source">Source of the operating system counters.</param>
    public class ServiceUsageScanner(ISystemUsageSource source)
    {
        private const string kStatFileName = "stat";
        private const string kStatmFileName = "statm";
        private const string kCommandLineFileName = "cmdline";
        private const string kUpTimeFile = "uptime";
        // the init process belongs to root and is therefore only readable if foreign processes are visible
        private const int kInitProcessId = 1;
        // times below /proc are reported in USER_HZ, which Linux fixes at 100 on all supported architectures
        private const double kClockTicksPerSecond = 100.0;
        private const double kFullPercent = 100.0;
        private const int kUserTimeFieldIndex = 11;
        private const int kSystemTimeFieldIndex = 12;
        private const int kThreadCountFieldIndex = 17;
        private const int kStartTimeFieldIndex = 19;
        private const int kResidentPagesFieldIndex = 1;

        // the kernel cuts the executable name in the stat file off after this many characters
        private const int kMaxExecutableNameLength = 15;

        private static readonly char[] kFieldSeparators = [' ', '\t', '\n', '\r'];
        private static readonly char[] kArgumentSeparators = ['\0'];

        // a service started through an interpreter carries the interpreter as executable name, its own name
        // is then only found on the command line. Matched as prefixes, because the name of an interpreter
        // usually carries its version, e.g. "python3.11".
        private static readonly List<string> kInterpreterNamePrefixes = ["python", "dotnet"];

        private static readonly List<string> kMiddlewareProcessNames = ["FWO.Middleware.Server"];
        private static readonly List<string> kImporterProcessNames = ["import_main_loop.py"];
        private static readonly List<string> kApiProcessNames = ["graphql-engine"];
        private static readonly List<string> kDatabaseProcessNames = ["postgres", "postmaster"];
        private static readonly List<string> kLdapProcessNames = ["slapd"];

        private static readonly List<ServiceDefinition> kServiceDefinitions =
        [
            new("middleware", kMiddlewareProcessNames, true),
            new("importer", kImporterProcessNames, true),
            new("hasura_api", kApiProcessNames, true),
            // database and ldap are matched by executable name only, otherwise commands mentioning them would be counted
            new("database", kDatabaseProcessNames, false),
            new("ldap_server", kLdapProcessNames, false)
        ];

        private readonly Dictionary<string, long> lastCpuTicks = [];
        private DateTime lastScanTime = DateTime.MinValue;

        /// <summary>
        /// Collects the usage of all known services currently running on this host.
        /// </summary>
        /// <param name="now">Point in time (UTC) of this sample.</param>
        /// <param name="processorCount">Number of logical processors, used to relate the CPU time to all cores.</param>
        /// <param name="memoryTotalBytes">Physical memory of the system, used to relate the memory of a service to it.</param>
        /// <returns>One entry per service found, in the order of the service definitions.</returns>
        public List<ServiceUsage> Scan(DateTime now, int processorCount, long memoryTotalBytes)
        {
            Dictionary<string, ServiceAccumulator> accumulators = [];
            foreach (int processId in source.ListProcessIds())
            {
                AddProcess(processId, accumulators);
            }

            double systemUpTimeSeconds = ParseUpTimeSeconds(source.ReadProcFile(kUpTimeFile));
            double elapsedSeconds = lastScanTime == DateTime.MinValue ? 0 : (now - lastScanTime).TotalSeconds;
            List<ServiceUsage> services = [];
            Dictionary<string, long> currentCpuTicks = [];
            foreach (ServiceDefinition definition in kServiceDefinitions)
            {
                if (accumulators.TryGetValue(definition.NameKey, out ServiceAccumulator? accumulator))
                {
                    services.Add(BuildUsage(definition.NameKey, accumulator, new SampleContext(elapsedSeconds,
                        processorCount, systemUpTimeSeconds, memoryTotalBytes)));
                    currentCpuTicks[definition.NameKey] = accumulator.CpuTicks;
                }
            }

            // services that vanished are dropped, so that a restarted service starts measuring from scratch
            lastCpuTicks.Clear();
            foreach (KeyValuePair<string, long> entry in currentCpuTicks)
            {
                lastCpuTicks[entry.Key] = entry.Value;
            }
            lastScanTime = now;
            return services;
        }

        /// <summary>
        /// Checks whether processes of other users are visible at all. A /proc mounted with the hidepid
        /// option only shows the own processes, and services running on another host or in another process
        /// namespace are invisible as well. Without this check an empty scan result could not be told apart
        /// from services that really are not running here.
        /// </summary>
        /// <returns>True if foreign processes can be read.</returns>
        public bool ForeignProcessesVisible()
        {
            return source.ReadProcFile($"{kInitProcessId}/{kStatFileName}") != null;
        }

        private void AddProcess(int processId, Dictionary<string, ServiceAccumulator> accumulators)
        {
            string? statContent = source.ReadProcFile($"{processId}/{kStatFileName}");
            if (statContent == null || !TryParseStat(statContent, out ProcessStat stat))
            {
                return;
            }

            ServiceDefinition? definition = FindDefinition(processId, stat.Comm);
            if (definition == null)
            {
                return;
            }

            if (!accumulators.TryGetValue(definition.NameKey, out ServiceAccumulator? accumulator))
            {
                accumulator = new();
                accumulators[definition.NameKey] = accumulator;
            }
            accumulator.Add(stat, ReadResidentBytes(processId));
        }

        private ServiceDefinition? FindDefinition(int processId, string executableName)
        {
            ServiceDefinition? definition = kServiceDefinitions.Find(candidate => candidate.Matches(executableName));
            if (definition != null)
            {
                return definition;
            }

            if (!MayHideNameOnCommandLine(executableName))
            {
                return null;
            }

            // the executable name of the kernel is cut off, so the command line is needed to recognize
            // services with longer names
            List<string> commandLineNames = ReadCommandLineNames(processId);
            return commandLineNames.Count == 0
                ? null
                : kServiceDefinitions.Find(candidate => candidate.MatchCommandLine && commandLineNames.Exists(candidate.Matches));
        }

        /// <summary>
        /// Checks whether the name of a service can still be hidden behind the given executable name.
        /// Reading the command line of every process of the host would double the cost of a scan, and only
        /// a name cut off by the kernel or an interpreter running a script elsewhere can carry one.
        /// </summary>
        /// <param name="executableName">Executable name as reported in the stat file.</param>
        /// <returns>True if the command line still has to be examined.</returns>
        private static bool MayHideNameOnCommandLine(string executableName)
        {
            return executableName.Length >= kMaxExecutableNameLength
                || kInterpreterNamePrefixes.Exists(prefix => executableName.StartsWith(prefix, StringComparison.Ordinal));
        }

        private ServiceUsage BuildUsage(string nameKey, ServiceAccumulator accumulator, SampleContext context)
        {
            TimeSpan upTime = CalculateUpTime(accumulator.EarliestStartTicks, context.SystemUpTimeSeconds);
            return new ServiceUsage
            {
                NameKey = nameKey,
                ProcessCount = accumulator.ProcessCount,
                MemoryBytes = accumulator.MemoryBytes,
                MemoryPercent = CalculateMemoryPercent(accumulator.MemoryBytes, context.MemoryTotalBytes),
                ThreadCount = accumulator.ThreadCount,
                UpTime = upTime,
                CpuPercent = CalculateCpuPercent(nameKey, accumulator.CpuTicks, context.ElapsedSeconds,
                    context.ProcessorCount, upTime)
            };
        }

        private static double CalculateMemoryPercent(long memoryBytes, long memoryTotalBytes)
        {
            return memoryTotalBytes > 0 ? Math.Clamp(kFullPercent * memoryBytes / memoryTotalBytes, 0, kFullPercent) : 0;
        }

        private double CalculateCpuPercent(string nameKey, long cpuTicks, double elapsedSeconds,
            int processorCount, TimeSpan upTime)
        {
            // on the very first sample there is no previous measurement, so report the average since service start
            bool hasPreviousSample = lastCpuTicks.TryGetValue(nameKey, out long previousTicks) && elapsedSeconds > 0;
            double ticks = hasPreviousSample ? cpuTicks - previousTicks : cpuTicks;
            double seconds = hasPreviousSample ? elapsedSeconds : upTime.TotalSeconds;
            if (ticks <= 0 || seconds <= 0 || processorCount <= 0)
            {
                return 0;
            }
            return Math.Clamp(kFullPercent * (ticks / kClockTicksPerSecond) / (seconds * processorCount), 0, kFullPercent);
        }

        private static TimeSpan CalculateUpTime(long startTicks, double systemUpTimeSeconds)
        {
            double upTimeSeconds = systemUpTimeSeconds - startTicks / kClockTicksPerSecond;
            return upTimeSeconds > 0 ? TimeSpan.FromSeconds(upTimeSeconds) : TimeSpan.Zero;
        }

        private List<string> ReadCommandLineNames(int processId)
        {
            string? content = source.ReadProcFile($"{processId}/{kCommandLineFileName}");
            if (string.IsNullOrEmpty(content))
            {
                return [];
            }

            // the arguments are separated by null characters, only the file name of each argument is of interest
            return [.. content.Split(kArgumentSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(argument => Path.GetFileName(argument.Trim()))];
        }

        private long ReadResidentBytes(int processId)
        {
            string? content = source.ReadProcFile($"{processId}/{kStatmFileName}");
            if (string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            List<string> fields = [.. content.Split(kFieldSeparators, StringSplitOptions.RemoveEmptyEntries)];
            // the second value is the resident set size, given in memory pages
            return fields.Count <= kResidentPagesFieldIndex
                ? 0
                : ParseLong(fields[kResidentPagesFieldIndex]) * source.MemoryPageSizeBytes;
        }

        private static bool TryParseStat(string content, out ProcessStat stat)
        {
            stat = default;
            int nameStart = content.IndexOf('(');
            int nameEnd = content.LastIndexOf(')');
            if (nameStart < 0 || nameEnd <= nameStart)
            {
                return false;
            }

            // the executable name may contain blanks and brackets, the fields behind it start with the state
            List<string> fields = [.. content[(nameEnd + 1)..].Split(kFieldSeparators, StringSplitOptions.RemoveEmptyEntries)];
            if (fields.Count <= kStartTimeFieldIndex)
            {
                return false;
            }

            long cpuTicks = ParseLong(fields[kUserTimeFieldIndex]) + ParseLong(fields[kSystemTimeFieldIndex]);
            stat = new ProcessStat(content[(nameStart + 1)..nameEnd], cpuTicks,
                (int)ParseLong(fields[kThreadCountFieldIndex]), ParseLong(fields[kStartTimeFieldIndex]));
            return true;
        }

        private static double ParseUpTimeSeconds(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            List<string> fields = [.. content.Split(kFieldSeparators, StringSplitOptions.RemoveEmptyEntries)];
            return fields.Count == 0 || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                ? 0
                : seconds;
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
        }

        /// <summary>
        /// Values of one process taken from its stat file below /proc.
        /// </summary>
        private readonly record struct ProcessStat(string Comm, long CpuTicks, int ThreadCount, long StartTicks);

        /// <summary>
        /// System values of one sample that all services are related to.
        /// </summary>
        private readonly record struct SampleContext(double ElapsedSeconds, int ProcessorCount,
            double SystemUpTimeSeconds, long MemoryTotalBytes);

        /// <summary>
        /// Describes how the processes of one service are recognized.
        /// </summary>
        private sealed class ServiceDefinition(string nameKey, List<string> processNames, bool matchCommandLine)
        {
            /// <summary>
            /// Text key of the service name.
            /// </summary>
            public string NameKey => nameKey;

            /// <summary>
            /// True if the service may also be recognized by an argument of the command line.
            /// </summary>
            public bool MatchCommandLine => matchCommandLine;

            /// <summary>
            /// Checks whether a process or file name belongs to this service.
            /// </summary>
            /// <param name="name">Name to check.</param>
            /// <returns>True if the name identifies this service.</returns>
            public bool Matches(string name)
            {
                return processNames.Exists(processName => string.Equals(processName, name, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Sums up the values of all processes belonging to one service.
        /// </summary>
        private sealed class ServiceAccumulator
        {
            public int ProcessCount { get; private set; }
            public long CpuTicks { get; private set; }
            public long MemoryBytes { get; private set; }
            public int ThreadCount { get; private set; }
            public long EarliestStartTicks { get; private set; } = long.MaxValue;

            /// <summary>
            /// Adds one process to the totals of the service.
            /// </summary>
            /// <param name="stat">Values read from the stat file of the process.</param>
            /// <param name="memoryBytes">Resident memory of the process in bytes.</param>
            public void Add(ProcessStat stat, long memoryBytes)
            {
                ProcessCount++;
                CpuTicks += stat.CpuTicks;
                MemoryBytes += memoryBytes;
                ThreadCount += stat.ThreadCount;
                EarliestStartTicks = Math.Min(EarliestStartTicks, stat.StartTicks);
            }
        }
    }
}

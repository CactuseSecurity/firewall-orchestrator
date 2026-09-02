using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Logging;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace FWO.Services.HeadlessBrowser
{
    /// <summary>
    /// Resolves the headless browser used for pdf rendering and starts it with fallbacks for
    /// environments in which the chrome sandbox cannot be used (no unprivileged user namespaces,
    /// missing or misconfigured setuid sandbox helper, very small /dev/shm).
    /// </summary>
    public static class HeadlessBrowserLauncher
    {
        /// <summary>
        /// Browser flavour fworch renders its pdfs with.
        /// </summary>
        public const SupportedBrowser WantedBrowser = SupportedBrowser.Chrome;

        private const string kLogTitle = "Report Export";
        private const string kSandboxHint = "the chrome sandbox could not be used on this system - "
            + "check unprivileged user namespaces (kernel.unprivileged_userns_clone, user.max_user_namespaces, "
            + "apparmor restrictions), the setuid helper chrome_sandbox and the size of /dev/shm";

        private static readonly List<string> kNoExtraArgs = [];
        private static readonly List<string> kSandboxlessArgs = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"];
        private static readonly List<string> kCompatibilityArgs =
        [
            .. kSandboxlessArgs,
            "--disable-gpu",
            "--disable-software-rasterizer",
            "--disable-features=Vulkan",
            "--disable-crash-reporter"
        ];

        private static volatile int FirstAttemptIndex;

        /// <summary>
        /// Resolves the browser binary to render with: the newest downloaded chrome for testing,
        /// or the chromium provided by the distribution as fallback.
        /// </summary>
        public static async Task<string> ResolveExecutablePath()
        {
            OperatingSystem os = Environment.OSVersion;
            bool onWindows = os.Platform == PlatformID.Win32NT;
            BrowserFetcher browserFetcher = CreateBrowserFetcher(ResolvePlatform(os), onWindows ? "" : GlobalConst.ChromeBinPathLinux);
            List<InstalledBrowser> installedBrowsers = GetInstalledBrowsers(browserFetcher);

            if (installedBrowsers.Count == 0 && onWindows)
            {
                Log.WriteInfo(kLogTitle, $"{WantedBrowser} not found for Windows! Trying to download...");
                await browserFetcher.DownloadAsync();
                installedBrowsers = GetInstalledBrowsers(browserFetcher);
            }

            if (installedBrowsers.Count == 0)
            {
                return ResolveSystemChromiumFallback(SystemChromium.GetPath());
            }

            return PreferAccessibleBrowser(SelectNewestExecutablePath(installedBrowsers), SystemChromium.GetPath(), File.Exists);
        }

        /// <summary>
        /// Starts the headless browser, falling back to a sandboxless start and to the system
        /// chromium when the wanted browser refuses to start.
        /// </summary>
        /// <param name="executablePath">Browser binary to start, as resolved by <see cref="ResolveExecutablePath"/>.</param>
        /// <param name="launchTimeoutMs">Timeout for the browser start.</param>
        /// <param name="protocolTimeoutMs">Timeout for devtools protocol calls.</param>
        public static Task<IBrowser> LaunchAsync(string executablePath, int launchTimeoutMs, int protocolTimeoutMs)
        {
            return LaunchAsync(attempt => Puppeteer.LaunchAsync(BuildLaunchOptions(attempt, launchTimeoutMs, protocolTimeoutMs)),
                executablePath, SystemChromium.GetPath());
        }

        /// <summary>
        /// Runs the given launch function over all fallback attempts and returns the first browser
        /// that starts. Every failed attempt is logged with its original error.
        /// </summary>
        /// <param name="launch">Function starting the browser for one attempt.</param>
        /// <param name="executablePath">Browser binary to start.</param>
        /// <param name="systemChromiumPath">System chromium used as last fallback, may be null.</param>
        public static async Task<IBrowser> LaunchAsync(Func<BrowserLaunchAttempt, Task<IBrowser>> launch, string executablePath, string? systemChromiumPath)
        {
            List<BrowserLaunchAttempt> attempts = BuildLaunchAttempts(executablePath, systemChromiumPath, FirstAttemptIndex);
            Exception? lastFailure = null;
            int attemptIndex = FirstAttemptIndex;

            foreach (BrowserLaunchAttempt attempt in attempts)
            {
                try
                {
                    IBrowser browser = await launch(attempt);
                    RememberSuccessfulAttempt(attempt, attemptIndex);
                    return browser;
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                    Log.WriteWarning(kLogTitle, $"Starting {WantedBrowser} {attempt.Description} at {attempt.ExecutablePath} failed: {exception.Message}");
                }
                attemptIndex++;
            }

            throw BuildLaunchException(lastFailure);
        }

        /// <summary>
        /// Builds the ordered list of launch attempts: with sandbox, without sandbox, without
        /// sandbox and gpu, and finally the system chromium. The output of the last attempt is
        /// written to the fworch log, so a browser that keeps crashing can be diagnosed.
        /// </summary>
        /// <param name="executablePath">Browser binary to start.</param>
        /// <param name="systemChromiumPath">System chromium used as last fallback, may be null.</param>
        /// <param name="firstAttempt">Index of the first attempt to return, earlier ones are known to fail.</param>
        public static List<BrowserLaunchAttempt> BuildLaunchAttempts(string executablePath, string? systemChromiumPath, int firstAttempt)
        {
            List<BrowserLaunchAttempt> attempts =
            [
                new(executablePath, kNoExtraArgs, false, "with sandbox"),
                new(executablePath, kSandboxlessArgs, true, "without sandbox"),
                new(executablePath, kCompatibilityArgs, true, "without sandbox and gpu")
            ];

            if (!string.IsNullOrWhiteSpace(systemChromiumPath) && systemChromiumPath != executablePath)
            {
                attempts.Add(new(systemChromiumPath, kCompatibilityArgs, true, "as system chromium without sandbox and gpu"));
            }

            attempts[^1].DumpIo = true;

            return [.. attempts.Skip(Math.Clamp(firstAttempt, 0, attempts.Count - 1))];
        }

        /// <summary>
        /// Builds the puppeteer launch options for one attempt.
        /// </summary>
        /// <param name="attempt">Attempt to build the options for.</param>
        /// <param name="launchTimeoutMs">Timeout for the browser start.</param>
        /// <param name="protocolTimeoutMs">Timeout for devtools protocol calls.</param>
        public static LaunchOptions BuildLaunchOptions(BrowserLaunchAttempt attempt, int launchTimeoutMs, int protocolTimeoutMs)
        {
            return new()
            {
                ExecutablePath = attempt.ExecutablePath,
                Headless = true,
                Args = attempt.Args.ToArray(),
                DumpIO = attempt.DumpIo,
                Timeout = launchTimeoutMs,
                ProtocolTimeout = protocolTimeoutMs
            };
        }

        /// <summary>
        /// Returns the given browser binary, or the system chromium when the browser binary cannot
        /// be accessed by the service user - which happens when the chrome installation was
        /// unpacked with root only permissions.
        /// </summary>
        /// <param name="executablePath">Browser binary that was selected.</param>
        /// <param name="systemChromiumPath">Path of the system chromium, may be null.</param>
        /// <param name="isAccessible">Check telling whether a binary can be accessed.</param>
        public static string PreferAccessibleBrowser(string executablePath, string? systemChromiumPath, Func<string, bool> isAccessible)
        {
            if (isAccessible(executablePath))
            {
                return executablePath;
            }

            Log.WriteWarning(kLogTitle, $"{WantedBrowser} at {executablePath} cannot be accessed - " +
                "check the permissions of the whole path for the fworch service user.");

            if (string.IsNullOrWhiteSpace(systemChromiumPath) || !isAccessible(systemChromiumPath))
            {
                return executablePath;
            }

            Log.WriteWarning(kLogTitle, $"Falling back to system chromium at: {systemChromiumPath}");
            return systemChromiumPath;
        }

        /// <summary>
        /// Returns the system chromium to fall back on when no browser was downloaded, or throws
        /// when the system does not provide one either.
        /// </summary>
        /// <param name="systemChromiumPath">Path of the system chromium, may be null.</param>
        public static string ResolveSystemChromiumFallback(string? systemChromiumPath)
        {
            if (string.IsNullOrWhiteSpace(systemChromiumPath))
            {
                throw new EnvironmentException($"Found no installed {WantedBrowser} instances and no system chromium!");
            }

            Log.WriteInfo(kLogTitle, $"No installed {WantedBrowser} found, falling back to system chromium at: {systemChromiumPath}");
            return systemChromiumPath;
        }

        /// <summary>
        /// Maps the operating system to the puppeteer browser platform.
        /// </summary>
        /// <param name="os">Operating system to map.</param>
        public static Platform ResolvePlatform(OperatingSystem os)
        {
            return os.Platform switch
            {
                PlatformID.Win32NT => Platform.Win32,
                PlatformID.Unix => Platform.Linux,
                _ => Platform.Unknown
            };
        }

        /// <summary>
        /// Resets the remembered launch attempt, only needed for tests.
        /// </summary>
        public static void ResetSandboxState()
        {
            FirstAttemptIndex = 0;
        }

        private static Exception BuildLaunchException(Exception? lastFailure)
        {
            string reason = lastFailure == null ? "no launch attempt was made" : lastFailure.Message;
            Log.WriteAlert(kLogTitle, $"Couldn't start {WantedBrowser} instance! Last error: {reason}. Hint: {kSandboxHint}.");
            string message = $"Couldn't start {WantedBrowser} instance! Last error: {reason}";
            return lastFailure == null ? new EnvironmentException(message) : new EnvironmentException(message, lastFailure);
        }

        private static void RememberSuccessfulAttempt(BrowserLaunchAttempt attempt, int attemptIndex)
        {
            if (attempt.SandboxDisabled && attemptIndex > FirstAttemptIndex)
            {
                Log.WriteWarning(kLogTitle, $"Started {WantedBrowser} {attempt.Description} - {kSandboxHint}.");
            }
            FirstAttemptIndex = attemptIndex;
        }

        private static BrowserFetcher CreateBrowserFetcher(Platform platform, string path)
        {
            return new(new BrowserFetcherOptions() { Platform = platform, Browser = WantedBrowser, Path = path });
        }

        private static List<InstalledBrowser> GetInstalledBrowsers(BrowserFetcher browserFetcher)
        {
            return [.. browserFetcher.GetInstalledBrowsers().Where(installed => installed.Browser == WantedBrowser)];
        }

        private static string SelectNewestExecutablePath(List<InstalledBrowser> installedBrowsers)
        {
            string? newestBuildId = installedBrowsers.Max(installed => installed.BuildId);
            if (string.IsNullOrWhiteSpace(newestBuildId))
            {
                throw new EnvironmentException("Invalid build ID!");
            }

            InstalledBrowser newestBrowser = installedBrowsers.First(installed => installed.BuildId == newestBuildId);
            string executablePath = newestBrowser.GetExecutablePath();
            Log.WriteInfo(kLogTitle, $"Selecting latest installed {WantedBrowser}({newestBrowser.BuildId}) at: {executablePath}");
            return executablePath;
        }
    }
}

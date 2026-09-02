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

        private static volatile bool SandboxKnownUnusable;

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

            return SelectNewestExecutablePath(installedBrowsers);
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
            List<BrowserLaunchAttempt> attempts = BuildLaunchAttempts(executablePath, systemChromiumPath, SandboxKnownUnusable);
            Exception? lastFailure = null;

            foreach (BrowserLaunchAttempt attempt in attempts)
            {
                try
                {
                    IBrowser browser = await launch(attempt);
                    RememberSandboxState(attempt);
                    return browser;
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                    Log.WriteWarning(kLogTitle, $"Starting {WantedBrowser} {attempt.Description} at {attempt.ExecutablePath} failed: {exception.Message}");
                }
            }

            throw BuildLaunchException(lastFailure);
        }

        /// <summary>
        /// Builds the ordered list of launch attempts: first with sandbox, then without, then the
        /// system chromium without sandbox.
        /// </summary>
        /// <param name="executablePath">Browser binary to start.</param>
        /// <param name="systemChromiumPath">System chromium used as last fallback, may be null.</param>
        /// <param name="skipSandboxedAttempt">True to skip the sandboxed attempt because it is known to fail.</param>
        public static List<BrowserLaunchAttempt> BuildLaunchAttempts(string executablePath, string? systemChromiumPath, bool skipSandboxedAttempt)
        {
            List<BrowserLaunchAttempt> attempts = [];

            if (!skipSandboxedAttempt)
            {
                attempts.Add(new(executablePath, kNoExtraArgs, false, "with sandbox"));
            }
            attempts.Add(new(executablePath, kSandboxlessArgs, true, "without sandbox"));

            if (!string.IsNullOrWhiteSpace(systemChromiumPath) && systemChromiumPath != executablePath)
            {
                attempts.Add(new(systemChromiumPath, kSandboxlessArgs, true, "as system chromium without sandbox"));
            }

            return attempts;
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
                Timeout = launchTimeoutMs,
                ProtocolTimeout = protocolTimeoutMs
            };
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
        /// Resets the cached sandbox state, only needed for tests.
        /// </summary>
        public static void ResetSandboxState()
        {
            SandboxKnownUnusable = false;
        }

        private static Exception BuildLaunchException(Exception? lastFailure)
        {
            string reason = lastFailure == null ? "no launch attempt was made" : lastFailure.Message;
            Log.WriteAlert(kLogTitle, $"Couldn't start {WantedBrowser} instance! Last error: {reason}. Hint: {kSandboxHint}.");
            string message = $"Couldn't start {WantedBrowser} instance! Last error: {reason}";
            return lastFailure == null ? new EnvironmentException(message) : new EnvironmentException(message, lastFailure);
        }

        private static void RememberSandboxState(BrowserLaunchAttempt attempt)
        {
            if (attempt.SandboxDisabled && !SandboxKnownUnusable)
            {
                Log.WriteWarning(kLogTitle, $"Started {WantedBrowser} {attempt.Description} - {kSandboxHint}.");
                SandboxKnownUnusable = true;
            }
            else if (!attempt.SandboxDisabled)
            {
                SandboxKnownUnusable = false;
            }
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

namespace FWO.Services.HeadlessBrowser
{
    /// <summary>
    /// One attempt to start the headless browser: which binary is started with which extra arguments.
    /// </summary>
    public class BrowserLaunchAttempt
    {
        /// <summary>
        /// Path of the browser binary to start.
        /// </summary>
        public string ExecutablePath { get; }

        /// <summary>
        /// Extra command line arguments added to the puppeteer defaults.
        /// </summary>
        public List<string> Args { get; }

        /// <summary>
        /// True when this attempt starts the browser with its sandbox switched off.
        /// </summary>
        public bool SandboxDisabled { get; }

        /// <summary>
        /// Human readable description of this attempt, used for logging.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Creates a launch attempt.
        /// </summary>
        /// <param name="executablePath">Path of the browser binary to start.</param>
        /// <param name="args">Extra command line arguments.</param>
        /// <param name="sandboxDisabled">True when the sandbox is switched off.</param>
        /// <param name="description">Description used for logging.</param>
        public BrowserLaunchAttempt(string executablePath, List<string> args, bool sandboxDisabled, string description)
        {
            ExecutablePath = executablePath;
            Args = args;
            SandboxDisabled = sandboxDisabled;
            Description = description;
        }
    }
}

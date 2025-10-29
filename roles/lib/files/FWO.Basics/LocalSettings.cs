using System.IO;
using System.Text.Json;


namespace FWO.Basics
{
    public static class LocalSettings
    {
        public static bool CSharpUnitTestsVerbose { get; private set; } = false;

        /// <summary>
        /// Static constructor to load local settings from a JSON file specified by the
        /// FWORCH_LOCAL_SETTINGS_PATH environment variable. To set this environment variable permanently on a linux system,
        /// you can run a command like this in your terminal: 'export FWORCH_LOCAL_SETTINGS_PATH="/<path to repo>/.vscode/settings.local.json"'.
        /// Please adjust the path according to your setup.
        /// If the environment variable is not set, the file is not found or cannot be read, default settings are used.
        /// </summary>
        static LocalSettings()
        {
            string? localSettings = Environment.GetEnvironmentVariable("FWORCH_LOCAL_SETTINGS_PATH");

            if (File.Exists(localSettings))
            {
                try
                {
                    using FileStream s = File.OpenRead(localSettings);
                    JsonDocument json = JsonDocument.Parse(s);

                    if (json.RootElement.TryGetProperty("test.unittests.csharp.verbose", out var val))
                    {
                        CSharpUnitTestsVerbose = val.GetBoolean();
                    }

                }
                catch
                {
                    Console.WriteLine($"Reading local settings from {localSettings} failed. Using default settings.");
                }
            }
        }
    }    
}


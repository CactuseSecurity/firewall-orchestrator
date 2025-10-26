using System.IO;
using System.Text.Json;


namespace FWO.Basics
{
    public static class LocalSettings
    {
        public static bool CSharpUnitTestsVerbose { get; private set; } = false;

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


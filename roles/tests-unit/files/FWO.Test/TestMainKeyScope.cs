using System;
using System.IO;

namespace FWO.Test
{
    /// <summary>
    /// Points AesEnc at a temporary main key file for the lifetime of the scope, so a test
    /// can exercise real encryption without the installed key, which is readable by the FWO
    /// service account alone and absent on an ordinary build host.
    /// </summary>
    /// <remarks>
    /// The key path is an environment variable and therefore process global, so a fixture
    /// using this must be NonParallelizable: a test running concurrently would otherwise
    /// see a main key it did not expect.
    /// </remarks>
    internal sealed class TestMainKeyScope : IDisposable
    {
        private const string kMainKeyFileEnvVar = "FWO_MAIN_KEY_FILE";

        private readonly string? previousValue;
        private readonly string mainKeyFile;

        public TestMainKeyScope(string mainKey)
        {
            previousValue = Environment.GetEnvironmentVariable(kMainKeyFileEnvVar);
            mainKeyFile = Path.Combine(Path.GetTempPath(), $"fwo-test-main-key-{Guid.NewGuid():N}");
            File.WriteAllText(mainKeyFile, mainKey);
            Environment.SetEnvironmentVariable(kMainKeyFileEnvVar, mainKeyFile);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(kMainKeyFileEnvVar, previousValue);
            if (File.Exists(mainKeyFile))
            {
                File.Delete(mainKeyFile);
            }
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using FWO.Basics;
using NUnit.Framework;
using NUnit.Framework.Legacy;


namespace FWO.Test
{
    [SetUpFixture]
    class TestInitializer
    {
        private const string kConfigFilePathEnvVar = "FWO_CONFIG_FILE_PATH";
        private const string kLogLockDirEnvVar = "FWO_LOG_LOCK_DIR";

        private const string kTestConfigFileContent = @"{
          ""api_uri"": ""https://127.0.0.1:9443/api/v1/graphql/"",
          ""fworch_home"": ""/usr/local/fworch"",
          ""middleware_native_uri"": ""http://127.0.0.3:8880/"",
          ""middleware_uri"": ""http://127.0.0.1:8880/"",
          ""product_version"": ""9.2.1"",
          ""remote_addresses"": []
        }";

        private FakeLocalTimeZone? fakeLocalTimeZone;
        private string? testConfigFilePath;
        private bool logLockDirSet;

        [OneTimeSetUp]
        public void OnStart()
        {
            SetLogLockDirectory();
            SetConfigFilePath();
            SetGermanCultureOnAllUnitTest();
            SetGermanTimeZoneOnAllUnitTest();
            SetQueryBasePath();
        }

        [OneTimeTearDown]
        public void OnFinish()
        {
            fakeLocalTimeZone?.Dispose();

            if (testConfigFilePath != null)
            {
                Environment.SetEnvironmentVariable(kConfigFilePathEnvVar, null);
                if (File.Exists(testConfigFilePath))
                {
                    File.Delete(testConfigFilePath);
                }
            }

            if (logLockDirSet)
            {
                Environment.SetEnvironmentVariable(kLogLockDirEnvVar, null);
            }
        }

        /// <summary>
        /// Points the Log lock file at the writable test work directory so the log lock
        /// mechanism does not depend on /var/fworch/lock existing on the test host
        /// (e.g. CI runners). Must run before any test touches Log.
        /// </summary>
        private void SetLogLockDirectory()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(kLogLockDirEnvVar)))
            {
                return;
            }

            Environment.SetEnvironmentVariable(kLogLockDirEnvVar, TestContext.CurrentContext.WorkDirectory);
            logLockDirSet = true;
        }

        /// <summary>
        /// Points ConfigFile at a synthetic config file so its static constructor does not
        /// depend on /etc/fworch/fworch.json existing on the test host (e.g. CI runners).
        /// Must run before any test touches ConfigFile.
        /// </summary>
        private void SetConfigFilePath()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(kConfigFilePathEnvVar)))
            {
                return;
            }

            testConfigFilePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "fworch.test.json");
            File.WriteAllText(testConfigFilePath, kTestConfigFileContent);
            Environment.SetEnvironmentVariable(kConfigFilePathEnvVar, testConfigFilePath);
        }

        public static void SetGermanCultureOnAllUnitTest()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");
        }

        public void SetGermanTimeZoneOnAllUnitTest()
        {
            fakeLocalTimeZone = new FakeLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")) { };
        }

        private void SetQueryBasePath()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FWO_BASE_DIR")))
            {
                return;
            }

            string baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "common", "files"));
            string queryDir = Path.Combine(baseDir, "fwo-api-calls");
            if (Directory.Exists(queryDir))
            {
                Environment.SetEnvironmentVariable("FWO_BASE_DIR", baseDir);
            }
        }
    }
}

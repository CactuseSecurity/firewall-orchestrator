using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;


namespace FWO.Test
{
    [SetUpFixture]
    class TestInitializer
    {
        private FakeLocalTimeZone? fakeLocalTimeZone;

        [OneTimeSetUp]
        public void OnStart()
        {
            SetGermanCultureOnAllUnitTest();
            SetGermanTimeZoneOnAllUnitTest();
            SetQueryBasePath();
        }

        [OneTimeTearDown]
        public void OnFinish()
        {
            fakeLocalTimeZone?.Dispose();
        }


        public static void SetGermanCultureOnAllUnitTest()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");
        }

        public void SetGermanTimeZoneOnAllUnitTest()
        {
            fakeLocalTimeZone = new FakeLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")){};
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

using NUnit.Framework;
using System.Globalization;


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
    }
}

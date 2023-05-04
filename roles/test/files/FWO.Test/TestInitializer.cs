using NUnit.Framework;
using System.Globalization;


namespace FWO.Test
{
    [SetUpFixture]
    class TestInitializer
    {
        [OneTimeSetUp]
        public void OnStart()
        {
            SetGermanCultureOnAllUnitTest();
        }

        [OneTimeTearDown]
        public void OnFinish()
        {

        }

        public static void SetGermanCultureOnAllUnitTest()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");
        }
    }
}

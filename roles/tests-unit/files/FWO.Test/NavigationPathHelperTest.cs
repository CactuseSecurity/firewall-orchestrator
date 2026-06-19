using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class NavigationPathHelperTest
    {
        private const string kBaseUri = "https://fwo-stest.ruv.de/";

        [TestCase("/networkmodelling/", "networkmodelling")]
        [TestCase("networkmodelling/", "networkmodelling")]
        [TestCase("request/tickets/?id=1#details", "request/tickets")]
        [TestCase("https://fwo-stest.ruv.de/networkmodelling/?id=1#details", "networkmodelling")]
        [TestCase("HTTPS://FWO-STEST.RUV.DE/NetworkModelling/", "networkmodelling")]
        public void GetBaseRelativePath_ReturnsLocalPath(string location, string expectedPath)
        {
            string path = NavigationPathHelper.GetBaseRelativePath(location, kBaseUri);

            Assert.That(path, Is.EqualTo(expectedPath));
        }

        [Test]
        public void GetBaseRelativePath_ReturnsEmptyPathForExternalHttpUri()
        {
            string path = NavigationPathHelper.GetBaseRelativePath("https://example.invalid/networkmodelling/", kBaseUri);

            Assert.That(path, Is.Empty);
        }
    }
}

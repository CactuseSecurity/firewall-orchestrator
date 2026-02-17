using FWO.Data.Modelling;
using FWO.Services.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class AppServerHelperTest
    {
        private static ModellingNamingConvention CreateNamingConvention()
        {
            return new ModellingNamingConvention
            {
                AppServerPrefix = "srv-",
                NetworkPrefix = "net-",
                IpRangePrefix = "rng-"
            };
        }

        [Test]
        public void ConstructAppServerName_UsesPrefixAndIp_WhenNameMissing()
        {
            ModellingAppServer appServer = new()
            {
                Name = "",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("srv-10.0.0.1"));
        }

        [Test]
        public void ConstructAppServerName_ReturnsName_WhenStartsWithLetter()
        {
            ModellingAppServer appServer = new()
            {
                Name = "web-1",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("web-1"));
        }

        [Test]
        public void ConstructAppServerName_PrefixesName_WhenStartsWithDigit()
        {
            ModellingAppServer appServer = new()
            {
                Name = "1web",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("srv-1web"));
        }

        [Test]
        public void ConstructSanitizedAppServerName_ReplacesInvalidCharacters()
        {
            ModellingAppServer appServer = new()
            {
                Name = "web!1",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructSanitizedAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("web_1"));
        }
    }
}

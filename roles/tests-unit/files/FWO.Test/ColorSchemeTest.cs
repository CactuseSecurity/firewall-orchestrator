using FWO.Config.Api.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ColorSchemeTest
    {
        [Test]
        public void GetSchemeByName_KnownName_ReturnsMatchingScheme()
        {
            ColorScheme result = ColorScheme.GetSchemeByName("color_scheme_red");

            Assert.That(result.Name, Is.EqualTo("color_scheme_red"));
        }

        [Test]
        public void GetSchemeByName_UnknownName_ReturnsDefaultScheme()
        {
            ColorScheme result = ColorScheme.GetSchemeByName("not_a_real_scheme");

            Assert.That(result.IsDefault, Is.True);
        }

        [Test]
        public void GetSchemeByName_NullName_ReturnsDefaultScheme()
        {
            ColorScheme result = ColorScheme.GetSchemeByName(null!);

            Assert.That(result.IsDefault, Is.True);
        }
    }
}

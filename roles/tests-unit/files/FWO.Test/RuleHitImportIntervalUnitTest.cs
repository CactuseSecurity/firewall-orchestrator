using FWO.Config.Api;
using FWO.Data.Enums;
using FWO.Ui.Data.Extensions;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleHitImportIntervalUnitTest
    {
        [Test]
        public void IsDisplayedWithItsLocalizedName()
        {
            SimulatedUserConfig userConfig = new();

            Assert.Multiple(() =>
            {
                Assert.That(RuleHitImportIntervalUnit.Minutes.ToString(userConfig), Is.EqualTo(userConfig.GetText("Minutes2")));
                Assert.That(RuleHitImportIntervalUnit.Hours.ToString(userConfig), Is.EqualTo(userConfig.GetText("Hours")));
                Assert.That(RuleHitImportIntervalUnit.Days.ToString(userConfig), Is.EqualTo(userConfig.GetText("Days")));
                Assert.That(((RuleHitImportIntervalUnit)99).ToString(userConfig), Is.EqualTo("99"));
            });
        }

        [Test]
        public void RequiresAUserConfig()
        {
            Assert.That(() => RuleHitImportIntervalUnit.Days.ToString((UserConfig)null!),
                Throws.InstanceOf<ArgumentNullException>());
        }
    }
}

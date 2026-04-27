using FWO.Basics.Extensions;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Ui.Data.Extensions;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class EnumExtensionsTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["PreferredCollapseState_Collapsed"] = "Collapsed";
            SimulatedUserConfig.DummyTranslate["PreferredCollapseState_Expanded"] = "Expanded";
            SimulatedUserConfig.DummyTranslate["PreferredCollapseState_Intermediate"] = "Intermediate";
        }

        [TestCase(PreferredCollapseState.Collapsed, "Collapsed")]
        [TestCase(PreferredCollapseState.Expanded, "Expanded")]
        [TestCase(PreferredCollapseState.Intermediate, "Intermediate")]
        public void ToString_ReturnsLocalizedText(PreferredCollapseState state, string expected)
        {
            UserConfig userConfig = new SimulatedUserConfig();

            string result = state.ToString(userConfig);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ToString_NullUserConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PreferredCollapseState.Collapsed.ToString((UserConfig)null!));
        }

        [Test]
        public void Except_NoExcludedValues_ReturnsOriginalSequence()
        {
            PreferredCollapseState[] values =
            [
                PreferredCollapseState.Collapsed,
                PreferredCollapseState.Expanded
            ];

            IEnumerable<PreferredCollapseState> result = values.Except();

            Assert.That(result, Is.EqualTo(values));
        }

        [Test]
        public void Except_SingleExcludedValue_FiltersMatchingEntries()
        {
            PreferredCollapseState[] values =
            [
                PreferredCollapseState.Collapsed,
                PreferredCollapseState.Expanded,
                PreferredCollapseState.Intermediate,
                PreferredCollapseState.Expanded
            ];

            IEnumerable<PreferredCollapseState> result = values.Except(PreferredCollapseState.Expanded);

            Assert.That(result, Is.EqualTo(
            [
                PreferredCollapseState.Collapsed,
                PreferredCollapseState.Intermediate
            ]));
        }

        [Test]
        public void Except_MultipleExcludedValues_FiltersAllMatches()
        {
            PreferredCollapseState[] values =
            [
                PreferredCollapseState.Collapsed,
                PreferredCollapseState.Expanded,
                PreferredCollapseState.Intermediate
            ];

            IEnumerable<PreferredCollapseState> result = values.Except(
                PreferredCollapseState.Collapsed,
                PreferredCollapseState.Intermediate);

            Assert.That(result, Is.EqualTo(
            [
                PreferredCollapseState.Expanded
            ]));
        }

        [Test]
        public void Except_NullValues_ThrowsArgumentNullException()
        {
            IEnumerable<PreferredCollapseState>? values = null;

            Assert.Throws<ArgumentNullException>(() => values!.Except(PreferredCollapseState.Collapsed).ToList());
        }
    }
}

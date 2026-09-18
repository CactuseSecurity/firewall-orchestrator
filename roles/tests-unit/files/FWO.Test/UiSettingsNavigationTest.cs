using FWO.Config.Api;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSettingsNavigationTest
    {
        private const int kSectionCount = 11;
        private const string kUnmatchableTerm = "qqzzxx";

        private static readonly List<string> TopologyHrefs = new() { "/settings/matrix", "/settings/internet" };

        private readonly SimulatedUserConfig userConfig = new();

        [Test]
        public void GetSections_EmptyTerm_ReturnsCompleteNavigation()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(kSectionCount));
                Assert.That(sections[0].TextKey, Is.EqualTo("devices"));
                Assert.That(sections[^1].TextKey, Is.EqualTo("personal"));
                Assert.That(sections.Sum(section => section.Entries.Count), Is.EqualTo(41));
            });
        }

        [Test]
        public void GetSections_WhitespaceOrNullTerm_ReturnsCompleteNavigation()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SettingsNavigationService.GetSections(userConfig, "   "), Has.Count.EqualTo(kSectionCount));
                Assert.That(SettingsNavigationService.GetSections(userConfig, null), Has.Count.EqualTo(kSectionCount));
            });
        }

        [Test]
        public void GetSections_MatchingEntry_KeepsOnlyTheMatchingEntry()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "manage");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].TextKey, Is.EqualTo("devices"));
                Assert.That(sections[0].Entries, Has.Count.EqualTo(1));
                Assert.That(sections[0].Entries[0].Href, Is.EqualTo("settings/managements"));
            });
        }

        [Test]
        public void GetSections_MatchingHeading_KeepsEveryEntryOfThatSection()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "topolog");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].TextKey, Is.EqualTo("network_topology"));
                Assert.That(sections[0].Entries.Select(entry => entry.Href),
                    Is.EqualTo(TopologyHrefs));
            });
        }

        [Test]
        public void GetSections_UnmatchableTerm_ReturnsNoSection()
        {
            Assert.That(SettingsNavigationService.GetSections(userConfig, kUnmatchableTerm), Is.Empty);
        }

        [Test]
        public void GetSections_IgnoresCaseAndSurroundingWhitespace()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "  MANAGE ");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].Entries[0].Href, Is.EqualTo("settings/managements"));
            });
        }

        [Test]
        public void GetSections_FoldsDiacriticsInTheSearchTerm()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "Öwners");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].TextKey, Is.EqualTo("owners"));
                Assert.That(sections[0].Entries, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void NormalizeForSearch_LowerCasesAndStripsDiacritics()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SettingsNavigationService.NormalizeForSearch("Überwachung"), Is.EqualTo("uberwachung"));
                Assert.That(SettingsNavigationService.NormalizeForSearch("  ÄNDERUNG "), Is.EqualTo("anderung"));
                Assert.That(SettingsNavigationService.NormalizeForSearch("Managements"), Is.EqualTo("managements"));
                Assert.That(SettingsNavigationService.NormalizeForSearch(""), Is.Empty);
            });
        }

        [Test]
        public void GetSections_FilteringDoesNotMutateTheSharedNavigation()
        {
            SettingsNavigationService.GetSections(userConfig, "manage");
            SettingsNavigationService.GetSections(userConfig, kUnmatchableTerm);

            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(kSectionCount));
                Assert.That(sections.Sum(section => section.Entries.Count), Is.EqualTo(41));
            });
        }

        [Test]
        public void GetSections_EveryEntryIsFullyDefinedAndRoutedOnlyOnce()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(userConfig, "");
            List<SettingsNavEntry> entries = sections.SelectMany(section => section.Entries).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(entries.Select(entry => entry.Href).Distinct().Count(), Is.EqualTo(entries.Count),
                    "every settings page must be reachable through exactly one navigation entry");
                Assert.That(entries.Any(entry => entry.TextKey.Length == 0 || entry.Icon.Length == 0), Is.False);
                Assert.That(sections.Any(section => section.TextKey.Length == 0), Is.False);
                Assert.That(entries.Count(entry => entry.InternalUsersOnly), Is.EqualTo(1),
                    "only the own password page is limited to internal ldap users");
                Assert.That(entries.Count(entry => entry.MatchAll), Is.EqualTo(1),
                    "only the owners overview needs an exact route match");
            });
        }
    }
}

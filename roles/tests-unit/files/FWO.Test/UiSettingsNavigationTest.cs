using FWO.Basics;
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

        private const string kExternalDn = "uid=tester,ou=people,dc=example,dc=org";

        private static readonly List<string> TopologyHrefs = new() { "/settings/matrix", "/settings/internet" };
        private static readonly List<string> AdminRoles = new() { Roles.Admin };
        private static readonly List<string> FwAdminRoles = new() { Roles.FwAdmin };
        private static readonly List<string> ImporterRoles = new() { Roles.Importer };
        private static readonly List<string> PersonalOnly = new() { "personal" };
        private static readonly List<string> PasswordPolicyOnly = new() { "settings/passwordpolicy" };
        private static readonly List<string> TenantsOnly = new() { "settings/tenants" };

        private readonly SimulatedUserConfig userConfig = CreateUserConfig(AdminRoles, InternalDn());

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


        [Test]
        public void GetSections_DropsSectionsWithoutVisibleEntries()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(
                CreateUserConfig(ImporterRoles, InternalDn()), "");

            Assert.That(sections.Select(section => section.TextKey), Is.EqualTo(PersonalOnly));
        }

        [Test]
        public void GetSections_MatchOnlyInRoleHiddenEntries_ReturnsNoSection()
        {
            // The heading "authorization" is visible to fw admins, but none of the matching entries is.
            Assert.That(SettingsNavigationService.GetSections(CreateUserConfig(FwAdminRoles, InternalDn()), "user"), Is.Empty);
        }

        [Test]
        public void GetSections_MatchingHeading_KeepsOnlyTheVisibleEntries()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(
                CreateUserConfig(FwAdminRoles, InternalDn()), "authorization");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].Entries.Select(entry => entry.Href), Is.EqualTo(TenantsOnly));
            });
        }

        [Test]
        public void GetSections_ExternalUser_DoesNotFindTheOwnPasswordPage()
        {
            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(
                CreateUserConfig(AdminRoles, kExternalDn), "password");

            Assert.That(sections.SelectMany(section => section.Entries).Select(entry => entry.Href),
                Is.EqualTo(PasswordPolicyOnly));
        }

        [Test]
        public void GetSections_MatchesTheLocalizedLabelInsteadOfTheTextKey()
        {
            LabelledUserConfig labelledConfig = new(AdminRoles, InternalDn());
            labelledConfig.Labels["devices"] = "Geräte";
            labelledConfig.Labels["managements"] = "Verwaltungen";

            Assert.Multiple(() =>
            {
                Assert.That(SettingsNavigationService.GetSections(labelledConfig, "manage"), Is.Empty,
                    "the text key must not be searched");
                Assert.That(SettingsNavigationService.GetSections(labelledConfig, "verwalt").Single().Entries.Single().Href,
                    Is.EqualTo("settings/managements"));
            });
        }

        [Test]
        public void GetSections_FoldsDiacriticsInTheLocalizedLabel()
        {
            LabelledUserConfig labelledConfig = new(AdminRoles, InternalDn());
            labelledConfig.Labels["devices"] = "Geräte";

            IReadOnlyList<SettingsNavSection> sections = SettingsNavigationService.GetSections(labelledConfig, "gerate");

            Assert.Multiple(() =>
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].TextKey, Is.EqualTo("devices"));
                Assert.That(sections[0].Entries, Has.Count.EqualTo(3));
            });
        }

        [Test]
        public void IsVisible_AppliesExecutionModeRolesAndInternalUserRestriction()
        {
            SettingsNavEntry adminEntry = new("users", "settings/users", Icons.User, Roles.Admin);
            SettingsNavEntry internalEntry = new("password", "settings/password", Icons.Login, InternalUsersOnly: true);
            SimulatedUserConfig fwAdminConfig = CreateUserConfig(FwAdminRoles, InternalDn());
            SimulatedUserConfig externalConfig = CreateUserConfig(AdminRoles, kExternalDn);

            Assert.Multiple(() =>
            {
                Assert.That(SettingsNavigationService.IsVisible(userConfig, adminEntry), Is.True);
                Assert.That(SettingsNavigationService.IsVisible(fwAdminConfig, adminEntry), Is.False);
                Assert.That(SettingsNavigationService.IsVisible(userConfig, internalEntry), Is.True);
                Assert.That(SettingsNavigationService.IsVisible(externalConfig, internalEntry), Is.False);
            });
        }

        private static SimulatedUserConfig CreateUserConfig(List<string> roles, string userDn)
        {
            return new SimulatedUserConfig
            {
                User =
                {
                    Dn = userDn,
                    Roles = new(roles)
                }
            };
        }

        private static string InternalDn()
        {
            return $"uid=tester,ou=people,{GlobalConst.kLdapInternalPostfix}";
        }

        /// <summary>
        /// User config resolving selected text keys into real labels, so that tests can tell labels and keys apart.
        /// </summary>
        private sealed class LabelledUserConfig : SimulatedUserConfig
        {
            public Dictionary<string, string> Labels { get; } = new();

            public LabelledUserConfig(List<string> roles, string userDn)
            {
                User.Dn = userDn;
                User.Roles = new(roles);
            }

            public override string GetText(string key)
            {
                return Labels.TryGetValue(key, out string? label) ? label : base.GetText(key);
            }
        }
    }
}

using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class OwnerRecertDisplayTest
    {
        [Test]
        public void FormatNextRecertDate_UsesEffectiveNextRecertDate()
        {
            FwoOwner owner = new()
            {
                NextRecertDate = new DateTime(2026, 2, 15)
            };

            Assert.That(OwnerRecertDisplay.FormatNextRecertDate(owner, new SimulatedUserConfig()), Is.EqualTo("15.02.2026"));
        }

        [Test]
        public void FormatLastRecertified_AppendsCreatedForCreationDateFallback()
        {
            FwoOwner owner = new()
            {
                ChangelogOwners =
                [
                    new()
                    {
                        ChangeAction = ChangelogActionType.INSERT,
                        ChangeImport = new() { Time = new DateTime(2026, 2, 1) }
                    }
                ]
            };

            Assert.That(OwnerRecertDisplay.FormatLastRecertified(owner, new SimulatedUserConfig()), Is.EqualTo("01.02.2026 (Created)"));
        }

        [Test]
        public void FormatLastRecertified_UsesActualLastRecertifiedWithoutCreatedRemark()
        {
            FwoOwner owner = new()
            {
                LastRecertified = new DateTime(2026, 1, 20)
            };

            Assert.That(OwnerRecertDisplay.FormatLastRecertified(owner, new SimulatedUserConfig()), Is.EqualTo("20.01.2026"));
        }

        [Test]
        public void FormatMainResponsibles_UsesDisplayNamesSortedAndCommaSeparated()
        {
            FwoOwner owner = new()
            {
                OwnerResponsibles =
                [
                    new() { ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeMain, Dn = "cn=z.user,ou=users,dc=test,dc=local" },
                    new() { ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeMain, Dn = "cn=a.user,ou=users,dc=test,dc=local" },
                    new() { ResponsibleTypeId = 99, Dn = "cn=other.user,ou=users,dc=test,dc=local" }
                ]
            };

            Assert.That(OwnerRecertDisplay.FormatMainResponsibles(owner), Is.EqualTo("a.user, z.user"));
        }

        [Test]
        public void FormatAdditionalInfoValue_ReturnsRequestedValue()
        {
            FwoOwner owner = new()
            {
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["business_unit"] = "Finance"
                }
            };

            Assert.That(OwnerRecertDisplay.FormatAdditionalInfoValue(owner, "business_unit"), Is.EqualTo("Finance"));
        }

        [Test]
        public void MatchesAdditionalInfoFilter_CoversAllModes()
        {
            FwoOwner owner = new()
            {
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["business_unit"] = "Finance"
                }
            };

            Assert.Multiple(() =>
            {
                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "",
                    Mode = AddInfoFilterMode.display_only
                }), Is.True);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.display_only
                }), Is.True);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.existing
                }), Is.True);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "missing_label",
                    Mode = AddInfoFilterMode.existing
                }), Is.False);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.not_existing
                }), Is.False);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "missing_label",
                    Mode = AddInfoFilterMode.not_existing
                }), Is.True);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.value,
                    Value = "Finance"
                }), Is.True);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.value,
                    Value = "Other"
                }), Is.False);
            });
        }

        [Test]
        public void MatchesAdditionalInfoFilter_TreatsEmptyAdditionalInfoValueAsExisting()
        {
            FwoOwner owner = new()
            {
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["business_unit"] = ""
                }
            };

            Assert.Multiple(() =>
            {
                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.existing
                }), Is.True);

                Assert.That(OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner, new AddInfoFilter
                {
                    Name = "business_unit",
                    Mode = AddInfoFilterMode.not_existing
                }), Is.False);
            });
        }

        [Test]
        public void FormatResponsibles_UsesRequestedTypeAndSeparator()
        {
            FwoOwner owner = new()
            {
                OwnerResponsibles =
                [
                    new() { ResponsibleTypeId = 7, Dn = "cn=first.group,ou=groups,dc=test,dc=local" },
                    new() { ResponsibleTypeId = 7, Dn = "cn=second.group,ou=groups,dc=test,dc=local" }
                ]
            };

            Assert.That(OwnerRecertDisplay.FormatResponsibles(owner, 7, "; "), Is.EqualTo("first.group; second.group"));
        }

        [Test]
        public void FormatResponsibles_SkipsEmptyDnsAndFallsBackToDn()
        {
            const string invalidDn = "not-a-distinguished-name";
            FwoOwner owner = new()
            {
                OwnerResponsibles =
                [
                    new() { ResponsibleTypeId = 7, Dn = "" },
                    new() { ResponsibleTypeId = 7, Dn = invalidDn }
                ]
            };

            Assert.That(OwnerRecertDisplay.FormatResponsibles(owner, 7, ", "), Is.EqualTo(invalidDn));
        }
    }
}

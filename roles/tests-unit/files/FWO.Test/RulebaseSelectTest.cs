using FWO.Data;
using FWO.Data.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RulebaseSelectTest
    {
        private const int kManagementId = 1;
        private const int kOtherManagementId = 2;
        private static readonly List<int> kExpectedStartRulebaseIds = [10, 40, 50];
        private static readonly List<int> kExpectedAllRulebaseIds = [10, 11, 20];

        [Test]
        public void IsStartRulebase_IgnoresInitialLinksAndLinksFromOtherManagements()
        {
            RulebaseSelect initial = CreateRulebase(10, new RulebaseLink { IsInitial = true });
            RulebaseSelect fromGlobal = CreateRulebase(40, new RulebaseLink { FromRulebaseId = 99, FromRulebase = new Rulebase { MgmtId = kOtherManagementId } });
            RulebaseSelect fromOtherRule = CreateRulebase(50, new RulebaseLink { FromRuleId = 98, FromRule = new Rule { MgmtId = kOtherManagementId } });

            Assert.That(initial.IsStartRulebase(kManagementId), Is.True);
            Assert.That(fromGlobal.IsStartRulebase(kManagementId), Is.True);
            Assert.That(fromOtherRule.IsStartRulebase(kManagementId), Is.True);
        }

        [Test]
        public void IsStartRulebase_IsFalseForLinksWithinManagementOrUnknownSource()
        {
            RulebaseSelect ordered = CreateRulebase(20, new RulebaseLink { FromRulebaseId = 10, FromRulebase = new Rulebase { MgmtId = kManagementId } });
            RulebaseSelect inline = CreateRulebase(30, new RulebaseLink { FromRuleId = 5, FromRule = new Rule { MgmtId = kManagementId } });
            RulebaseSelect hiddenRule = CreateRulebase(60, new RulebaseLink { FromRuleId = 6 });
            RulebaseSelect hiddenRulebase = CreateRulebase(70, new RulebaseLink { FromRulebaseId = 7 });

            Assert.That(ordered.IsStartRulebase(kManagementId), Is.False);
            Assert.That(inline.IsStartRulebase(kManagementId), Is.False);
            Assert.That(hiddenRule.IsStartRulebase(kManagementId), Is.False);
            Assert.That(hiddenRulebase.IsStartRulebase(kManagementId), Is.False);
        }

        [Test]
        public void KeepStartRulebases_RemovesLinkedRulebasesAndEmptyManagements()
        {
            List<RulebaseManagementSelect> managements =
            [
                new RulebaseManagementSelect
                {
                    Id = kManagementId,
                    Name = "Management",
                    Rulebases =
                    [
                        CreateRulebase(10, new RulebaseLink { IsInitial = true }),
                        CreateRulebase(20, new RulebaseLink { FromRulebaseId = 10, FromRulebase = new Rulebase { MgmtId = kManagementId } }),
                        CreateRulebase(40, new RulebaseLink { FromRulebaseId = 99, FromRulebase = new Rulebase { MgmtId = kOtherManagementId } }),
                        new RulebaseSelect { Id = 50, Name = "Without gateway" }
                    ]
                },
                new RulebaseManagementSelect
                {
                    Id = kOtherManagementId,
                    Name = "Only linked",
                    Rulebases = [CreateRulebase(80, new RulebaseLink { FromRuleId = 8, FromRule = new Rule { MgmtId = kOtherManagementId } })]
                }
            ];

            List<RulebaseManagementSelect> result = RulebaseManagementSelect.KeepStartRulebases(managements);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Rulebases.Select(rulebase => rulebase.Id), Is.EqualTo(kExpectedStartRulebaseIds));
        }

        [Test]
        public void Toggle_AddsAndRemovesSelectedRulebase()
        {
            List<RulebaseManagementSelect> managements = CreateManagements();
            List<SelectedRulebase> selection = [];

            RulebaseSelectionHelper.Toggle(selection, managements[0], managements[0].Rulebases[0]);

            Assert.That(selection, Has.Count.EqualTo(1));
            Assert.That(selection[0].ManagementId, Is.EqualTo(kManagementId));
            Assert.That(selection[0].RulebaseId, Is.EqualTo(10));
            Assert.That(selection[0].ToString(), Is.EqualTo("Management A: Start A1"));
            Assert.That(RulebaseSelectionHelper.IsSelected(selection, 10), Is.True);

            RulebaseSelectionHelper.Toggle(selection, managements[0], managements[0].Rulebases[0]);

            Assert.That(selection, Is.Empty);
        }

        [Test]
        public void SelectAll_SelectsOnlyVisibleManagements()
        {
            List<RulebaseManagementSelect> managements = CreateManagements();
            managements.Add(new RulebaseManagementSelect { Id = 3, Name = "Hidden", Visible = false, Rulebases = [new RulebaseSelect { Id = 30, Name = "Hidden start" }] });

            List<SelectedRulebase> selection = RulebaseSelectionHelper.SelectAll(managements);

            Assert.That(selection.Select(selected => selected.RulebaseId), Is.EqualTo(kExpectedAllRulebaseIds));
            Assert.That(RulebaseSelectionHelper.AreAllSelected(selection, managements), Is.True);
            List<SelectedRulebase> partialSelection = [.. selection.Skip(1)];
            Assert.That(RulebaseSelectionHelper.AreAllSelected(partialSelection, managements), Is.False);
        }

        [Test]
        public void AreAllSelected_IsFalseWithoutVisibleRulebases()
        {
            List<SelectedRulebase> noSelection = [];
            List<RulebaseManagementSelect> noManagements = [];
            Assert.That(RulebaseSelectionHelper.AreAllSelected(noSelection, noManagements), Is.False);
        }

        [Test]
        public void KeepAvailable_DropsUnknownRulebasesAndRefreshesNames()
        {
            List<SelectedRulebase> selection =
            [
                new SelectedRulebase { ManagementId = kManagementId, RulebaseId = 11, ManagementName = "old", RulebaseName = "old" },
                new SelectedRulebase { ManagementId = kManagementId, RulebaseId = 999 }
            ];

            List<SelectedRulebase> result = RulebaseSelectionHelper.KeepAvailable(selection, CreateManagements());

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].RulebaseId, Is.EqualTo(11));
            Assert.That(result[0].ManagementName, Is.EqualTo("Management A"));
            Assert.That(result[0].RulebaseName, Is.EqualTo("Start A2"));
        }

        private static RulebaseSelect CreateRulebase(int id, RulebaseLink incomingLink)
        {
            return new RulebaseSelect { Id = id, Name = $"Rulebase {id}", IncomingLinks = [incomingLink] };
        }

        private static List<RulebaseManagementSelect> CreateManagements()
        {
            return
            [
                new RulebaseManagementSelect
                {
                    Id = kManagementId,
                    Name = "Management A",
                    Rulebases = [new RulebaseSelect { Id = 10, Name = "Start A1" }, new RulebaseSelect { Id = 11, Name = "Start A2" }]
                },
                new RulebaseManagementSelect
                {
                    Id = kOtherManagementId,
                    Name = "Management B",
                    Rulebases = [new RulebaseSelect { Id = 20, Name = "Start B1" }]
                }
            ];
        }
    }
}

using FWO.Data;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ChangeReportRuleSelectionTest
    {
        private const string kBefore = "before change";
        private const string kAfter = "after change";
        private static readonly long[] kExpectedVersionIds = [2, 11, 12, 21];

        [Test]
        public void SelectedChangesAreShownWithTheirRelevantRuleVersions()
        {
            List<RuleChange> changes =
            [
                new RuleChange { ChangeAction = 'I', RulebaseName = "Branch", OldRule = null!, NewRule = new Rule { Id = 2, MgmtId = 5, Name = "added" } },
                new RuleChange { ChangeAction = 'C', RulebaseName = "Branch", OldRule = new Rule { Id = 11, MgmtId = 5, Name = "mod" }, NewRule = new Rule { Id = 12, MgmtId = 5, Name = "mod" } },
                new RuleChange { ChangeAction = 'D', RulebaseName = "Core", OldRule = new Rule { Id = 21, MgmtId = 6, Name = "deleted" }, NewRule = null! }
            ];

            List<Rule> versions = ChangeReportRuleSelection.GetRuleVersions(changes, kBefore, kAfter);

            Assert.That(versions.Select(r => r.Id), Is.EqualTo(kExpectedVersionIds));
            Assert.That(versions[0].RulebaseName, Is.EqualTo("Branch"));
            Assert.That(versions[1].RulebaseName, Is.EqualTo($"Branch ({kBefore})"));
            Assert.That(versions[2].RulebaseName, Is.EqualTo($"Branch ({kAfter})"));
            Assert.That(versions[3].RulebaseName, Is.EqualTo("Core"));
            Assert.That(versions[3].MgmtId, Is.EqualTo(6), "the management id is needed to fetch the rule details");
        }

        [Test]
        public void ReportRulesAreNotModifiedAndVersionsAreListedOnce()
        {
            Rule newRule = new() { Id = 2, RulebaseName = "unchanged" };
            List<RuleChange> changes =
            [
                new RuleChange { ChangeAction = 'I', RulebaseName = "Branch", NewRule = newRule },
                new RuleChange { ChangeAction = 'I', RulebaseName = "Branch", NewRule = newRule },
                new RuleChange { ChangeAction = 'X', RulebaseName = "Branch", NewRule = new Rule { Id = 3 } },
                new RuleChange { ChangeAction = 'D', RulebaseName = "Branch", OldRule = new Rule() }
            ];

            List<Rule> versions = ChangeReportRuleSelection.GetRuleVersions(changes, kBefore, kAfter);

            Assert.That(versions, Has.Count.EqualTo(1), "duplicates, unknown change actions and rules without id are skipped");
            Assert.That(versions[0], Is.Not.SameAs(newRule));
            Assert.That(newRule.RulebaseName, Is.EqualTo("unchanged"));
        }
    }
}

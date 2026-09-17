using FWO.Basics;
using FWO.Data;
using FWO.Services;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace FWO.Test
{
    /// <summary>
    /// Covers how the result of a full reinitialize is turned into a history entry: the counts always carry
    /// the full number, while the listed pairs are capped so one run cannot fill the config entry.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleOwnerMappingRunHistoryTest
    {
        private const int kMaxListedPairs = 500;
        private const long kControlId = 42;

        private static readonly List<RuleOwner> NoRuleOwners = [];
        private static readonly List<long> NoPendingImports = [];

        private static List<RuleOwner> RuleOwners(int count, int firstRuleId = 1)
        {
            return Enumerable.Range(firstRuleId, count).Select(i => new RuleOwner { RuleId = i, OwnerId = 1 }).ToList();
        }

        [Test]
        public void BuildRun_KeepsTheFullCounts_WhenThePairListsAreTruncated()
        {
            // the counts are what the page reports, so they must survive the cap on the listed pairs
            List<RuleOwner> newRuleOwners = RuleOwners(kMaxListedPairs + 100);

            RuleOwnerMappingRun run = RuleOwnerMappingRunHistory.BuildRun(kControlId, OwnerMappingSourceStm.CustomField,
                NoRuleOwners, newRuleOwners, NoPendingImports);

            Assert.Multiple(() =>
            {
                Assert.That(run.AddedCount, Is.EqualTo(kMaxListedPairs + 100), "the count always holds the full number");
                Assert.That(run.Added, Has.Count.EqualTo(kMaxListedPairs), "the listed pairs are capped");
                Assert.That(run.PairListsTruncated, Is.True, "the page has to be able to say the list is incomplete");
            });
        }

        [Test]
        public void BuildRun_DoesNotReportTruncation_WhenThePairsFitExactly()
        {
            // the boundary itself is still complete, so claiming truncation here would be a false warning
            List<RuleOwner> newRuleOwners = RuleOwners(kMaxListedPairs);

            RuleOwnerMappingRun run = RuleOwnerMappingRunHistory.BuildRun(kControlId, OwnerMappingSourceStm.CustomField,
                NoRuleOwners, newRuleOwners, NoPendingImports);

            Assert.Multiple(() =>
            {
                Assert.That(run.Added, Has.Count.EqualTo(kMaxListedPairs));
                Assert.That(run.PairListsTruncated, Is.False);
            });
        }

        [Test]
        public void BuildRun_TruncatesRemovedPairsAsWell()
        {
            // a rebuild that drops a large set must not fill the config entry either
            List<RuleOwner> previousRuleOwners = RuleOwners(kMaxListedPairs + 50);

            RuleOwnerMappingRun run = RuleOwnerMappingRunHistory.BuildRun(kControlId, OwnerMappingSourceStm.CustomField,
                previousRuleOwners, NoRuleOwners, NoPendingImports);

            Assert.Multiple(() =>
            {
                Assert.That(run.RemovedCount, Is.EqualTo(kMaxListedPairs + 50));
                Assert.That(run.Removed, Has.Count.EqualTo(kMaxListedPairs));
                Assert.That(run.PairListsTruncated, Is.True);
            });
        }
    }
}

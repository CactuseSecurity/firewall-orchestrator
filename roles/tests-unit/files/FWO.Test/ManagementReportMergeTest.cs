using System.Collections.Generic;
using System.Linq;
using FWO.Data;
using FWO.Data.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ManagementReportMergeTest
    {
        private static readonly long[] ExpectedMergedObjectIds = [1, 2, 3];
        private static readonly long[] ExpectedMergedServiceIds = [10, 11];
        private static readonly long[] ExpectedMergedUserIds = [20, 21];
        private static readonly long[] ExpectedFirstRulebaseRuleIds = [100, 101];
        private static readonly long[] ExpectedSecondRulebaseRuleIds = [200, 201, 202];
        private static readonly long[] ExpectedObjectIdsFromEmptyTarget = [2, 3];

        [Test]
        public void Merge_AppendsArraysAndRulesByRulebaseId()
        {
            ManagementReport target = ManagementReport(
                objectIds: [1],
                serviceIds: [10],
                userIds: [20],
                rulebases:
                [
                    Rulebase(1, 100),
                    Rulebase(2, 200)
                ]);

            ManagementReport source = ManagementReport(
                objectIds: [2, 3],
                serviceIds: [11],
                userIds: [21],
                rulebases:
                [
                    Rulebase(2, 201, 202),
                    Rulebase(1, 101)
                ]);

            (bool newObjects, Dictionary<string, int> addedCounts) = target.Merge(source);

            Assert.That(newObjects, Is.True);
            Assert.That(target.Objects.Select(obj => obj.Id), Is.EqualTo(ExpectedMergedObjectIds));
            Assert.That(target.Services.Select(service => service.Id), Is.EqualTo(ExpectedMergedServiceIds));
            Assert.That(target.Users.Select(user => user.Id), Is.EqualTo(ExpectedMergedUserIds));
            Assert.That(target.Rulebases.Single(rulebase => rulebase.Id == 1).Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedFirstRulebaseRuleIds));
            Assert.That(target.Rulebases.Single(rulebase => rulebase.Id == 2).Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedSecondRulebaseRuleIds));
            Assert.That(addedCounts["NetworkObjects"], Is.EqualTo(2));
            Assert.That(addedCounts["Rules"], Is.EqualTo(2));
        }

        [Test]
        public void Merge_CopiesSourceArrays_WhenTargetArraysAreEmpty()
        {
            ManagementReport target = ManagementReport([], [], [], []);
            ManagementReport source = ManagementReport(
                objectIds: [2, 3],
                serviceIds: [],
                userIds: [],
                rulebases: []);

            (bool newObjects, Dictionary<string, int> addedCounts) = target.Merge(source);

            Assert.That(newObjects, Is.True);
            Assert.That(target.Objects.Select(obj => obj.Id), Is.EqualTo(ExpectedObjectIdsFromEmptyTarget));
            Assert.That(addedCounts["NetworkObjects"], Is.EqualTo(2));
        }

        [Test]
        public void Merge_DifferentRulebaseIds_Throws()
        {
            ManagementReport target = ManagementReport([], [], [], [Rulebase(1, 100)]);
            ManagementReport source = ManagementReport([], [], [], [Rulebase(2, 200)]);

            Assert.That(() => target.Merge(source), Throws.TypeOf<NotSupportedException>());
        }

        private static ManagementReport ManagementReport(long[] objectIds, long[] serviceIds, long[] userIds, RulebaseReport[] rulebases)
        {
            return new ManagementReport
            {
                Objects = objectIds.Select(id => new NetworkObject { Id = id }).ToArray(),
                Services = serviceIds.Select(id => new NetworkService { Id = id }).ToArray(),
                Users = userIds.Select(id => new NetworkUser { Id = id }).ToArray(),
                Rulebases = rulebases
            };
        }

        private static RulebaseReport Rulebase(int id, params long[] ruleIds)
        {
            return new RulebaseReport
            {
                Id = id,
                Rules = ruleIds.Select(ruleId => new Rule { Id = ruleId, RulebaseId = id }).ToArray()
            };
        }
    }
}

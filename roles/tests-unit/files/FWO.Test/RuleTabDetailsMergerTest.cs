using FWO.Data;
using FWO.Ui.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class RuleTabDetailsMergerTest
    {
        private static readonly long[] kMergedObjectIds = [1, 2, 3];
        private static readonly long[] kMergedServiceIds = [10, 11];

        [Test]
        public void DetailedObjectsReplaceTheReportedOverviewAndReportOnlyObjectsAreKept()
        {
            NetworkObject overviewGroup = new() { Id = 1, Name = "grp" };
            NetworkObject detailedGroup = new() { Id = 1, Name = "grp", ObjectGroups = [new Group<NetworkObject> { Object = new NetworkObject { Id = 9 } }] };
            Rule rule = new()
            {
                Froms = [new NetworkLocation(new NetworkUser(), overviewGroup), new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 3 })],
                Services = [new ServiceWrapper { Content = new NetworkService { Id = 10 } }]
            };
            Rule detailed = new()
            {
                Froms = [new NetworkLocation(new NetworkUser(), detailedGroup), new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 2 })],
                Services = [new ServiceWrapper { Content = new NetworkService { Id = 10, Name = "detailed" } }, new ServiceWrapper { Content = new NetworkService { Id = 11 } }]
            };

            RuleTabDetailsMerger.Merge(rule, detailed);

            Assert.That(rule.Froms.Select(l => l.Object.Id).Order(), Is.EqualTo(kMergedObjectIds));
            Assert.That(rule.Froms.Single(l => l.Object.Id == 1).Object, Is.SameAs(detailedGroup), "the group with its members is shown, not the overview");
            Assert.That(rule.Services.Select(s => s.Content.Id), Is.EqualTo(kMergedServiceIds));
            Assert.That(rule.Services[0].Content.Name, Is.EqualTo("detailed"));
        }

        [Test]
        public void LocationsDifferingOnlyInTheUserAreKeptSeparately()
        {
            NetworkObject sharedObject = new() { Id = 5 };
            NetworkLocation[] reported = [new NetworkLocation(new NetworkUser { Id = 7 }, sharedObject)];
            NetworkLocation[] detailed = [new NetworkLocation(new NetworkUser { Id = 8 }, sharedObject)];

            Assert.That(RuleTabDetailsMerger.MergeLocations(reported, detailed), Has.Length.EqualTo(2));
            Assert.That(RuleTabDetailsMerger.MergeLocations(null, null), Is.Empty);
        }

        [Test]
        public void NatTranslationsAreMergedToo()
        {
            Rule rule = new() { NatData = new NatData { TranslatedTos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 4, Name = "overview" })] } };
            Rule detailed = new() { NatData = new NatData { TranslatedTos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 4, Name = "detailed" })] } };

            RuleTabDetailsMerger.Merge(rule, detailed);

            Assert.That(rule.NatData.TranslatedTos.Single().Object.Name, Is.EqualTo("detailed"));
        }
    }
}

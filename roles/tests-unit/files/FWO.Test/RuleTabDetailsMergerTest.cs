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

        [Test]
        public void MergeIntoACopiedRuleDoesNotChangeTheTranslationOfTheReportRule()
        {
            NetworkLocation reportedSource = new(new NetworkUser(), new NetworkObject { Id = 1, Name = "overview" });
            NetworkLocation[] reportedDestinations = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 2 })];
            ServiceWrapper[] reportedServices = [new ServiceWrapper { Content = new NetworkService { Id = 3 } }];
            Rule reportRule = new()
            {
                NatRule = true,
                NatData = new NatData
                {
                    TranslatedSource = "src",
                    TranslatedSourceNegated = true,
                    TranslatedFroms = [reportedSource],
                    TranslatedDestination = "dst",
                    TranslatedDestinationNegated = true,
                    TranslatedTos = reportedDestinations,
                    TranslatedService = "svc",
                    TranslatedServiceNegated = true,
                    TranslatedServices = reportedServices
                }
            };
            Rule ruleTabCopy = new(reportRule);
            Rule detailed = new()
            {
                NatData = new NatData
                {
                    TranslatedFroms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 1, Name = "detailed" }), new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 4 })],
                    TranslatedTos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 5 })],
                    TranslatedServices = [new ServiceWrapper { Content = new NetworkService { Id = 6 } }]
                }
            };

            RuleTabDetailsMerger.Merge(ruleTabCopy, detailed);

            Assert.That(reportRule.NatData.TranslatedFroms.Single(), Is.SameAs(reportedSource));
            Assert.That(reportRule.NatData.TranslatedTos, Is.SameAs(reportedDestinations));
            Assert.That(reportRule.NatData.TranslatedServices, Is.SameAs(reportedServices));
            Assert.That(ruleTabCopy.NatData.TranslatedFroms, Has.Length.EqualTo(2));
            Assert.That(ruleTabCopy.NatData.TranslatedTos, Has.Length.EqualTo(2));
            Assert.That(ruleTabCopy.NatData.TranslatedServices, Has.Length.EqualTo(2));
            Assert.That(ruleTabCopy.NatData.TranslatedSource, Is.EqualTo("src"));
            Assert.That(ruleTabCopy.NatData.TranslatedSourceNegated, Is.True);
            Assert.That(ruleTabCopy.NatData.TranslatedDestination, Is.EqualTo("dst"));
            Assert.That(ruleTabCopy.NatData.TranslatedDestinationNegated, Is.True);
            Assert.That(ruleTabCopy.NatData.TranslatedService, Is.EqualTo("svc"));
            Assert.That(ruleTabCopy.NatData.TranslatedServiceNegated, Is.True);
        }

        [Test]
        public void MissingTranslationsAreHandled()
        {
            Rule rule = new() { NatData = null! };
            Rule detailed = new() { NatData = new NatData { TranslatedFroms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 4 })] } };
            Rule ruleWithTranslation = new();

            RuleTabDetailsMerger.Merge(rule, detailed);
            RuleTabDetailsMerger.Merge(ruleWithTranslation, new Rule { NatData = null! });

            Assert.That(rule.NatData.TranslatedFroms.Single().Object.Id, Is.EqualTo(4));
            Assert.That(ruleWithTranslation.NatData.TranslatedFroms, Is.Empty);
        }
    }
}

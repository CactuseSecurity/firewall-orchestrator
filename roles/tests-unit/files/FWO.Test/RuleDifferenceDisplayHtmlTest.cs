using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Verifies the labels and styles of missing and surplus objects in the variance analysis report.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleDifferenceDisplayHtmlTest
    {
        private const string kLineThrough = "line-through";
        private const string kMissingLabel = "missing in firewall rule";
        private const string kSurplusLabel = "surplus in firewall rule";

        private readonly SimulatedUserConfig userConfig = new();

        private static NetworkObject Host(string name, bool isSurplus)
        {
            return new NetworkObject { Id = 1, Name = name, IP = "10.0.0.1/32", IpEnd = "10.0.0.1/32", Type = new NetworkObjectType { Name = ObjectType.Host }, IsSurplus = isSurplus };
        }

        private static NetworkService Service(string name, bool isSurplus)
        {
            return new NetworkService { Id = 1, Name = name, Type = new NetworkServiceType { Name = "simple" }, IsSurplus = isSurplus };
        }

        private static Rule DiffRule()
        {
            return new Rule
            {
                Id = 1,
                Froms = [new NetworkLocation(new NetworkUser(), Host("SurplusSrc", true)), new NetworkLocation(new NetworkUser(), Host("UnchangedSrc", false))],
                DisregardedFroms = [new NetworkLocation(new NetworkUser(), Host("MissingSrc", false))],
                Tos = [new NetworkLocation(new NetworkUser(), Host("SurplusDst", true))],
                DisregardedTos = [new NetworkLocation(new NetworkUser(), Host("MissingDst", false))],
                Services = [new ServiceWrapper { Content = Service("SurplusSvc", true) }],
                DisregardedServices = [Service("MissingSvc", false)]
            };
        }

        private static void AssertDiff(string html, string surplusName, string missingName)
        {
            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain($"{kMissingLabel}: <p style=\"{GlobalConst.kStyleMissing}\">"));
                Assert.That(html, Does.Contain($"{kSurplusLabel}: <p style=\"{GlobalConst.kStyleSurplus}\">"));
                Assert.That(html, Does.Contain(surplusName));
                Assert.That(html, Does.Contain(missingName));
                Assert.That(html, Does.Not.Contain(kLineThrough));
                Assert.That(html, Does.Not.Contain(GlobalConst.kStyleAdded));
            });
        }

        [Test]
        public void DisplaySourceDiff_MarksMissingAndSurplusObjects()
        {
            RuleDifferenceDisplayHtml display = new(userConfig);
            string html = display.DisplaySourceDiff(DiffRule(), OutputLocation.report, ReportType.VarianceAnalysis);
            AssertDiff(html, "SurplusSrc", "MissingSrc");
            Assert.That(html, Does.Contain("UnchangedSrc"));
        }

        [Test]
        public void DisplayDestinationDiff_MarksMissingAndSurplusObjects()
        {
            RuleDifferenceDisplayHtml display = new(userConfig);
            AssertDiff(display.DisplayDestinationDiff(DiffRule(), OutputLocation.report, ReportType.VarianceAnalysis), "SurplusDst", "MissingDst");
        }

        [Test]
        public void DisplayServiceDiff_MarksMissingAndSurplusServices()
        {
            RuleDifferenceDisplayHtml display = new(userConfig);
            AssertDiff(display.DisplayServiceDiff(DiffRule(), OutputLocation.report, ReportType.VarianceAnalysis), "SurplusSvc", "MissingSvc");
        }

        [TestCase('D', GlobalConst.kStyleDeleted)]
        [TestCase('I', GlobalConst.kStyleAdded)]
        public void ChangeReport_KeepsDeletedAndAddedStyles(char changeAction, string expectedStyle)
        {
            Rule rule = new() { Id = 1, Froms = [new NetworkLocation(new NetworkUser(), Host("ChangedSrc", false))] };
            RuleChange ruleChange = new() { ChangeAction = changeAction, OldRule = rule, NewRule = rule };
            RuleChangeDisplayHtml display = new(userConfig);
            string html = display.DisplaySource(ruleChange, OutputLocation.report, ReportType.Changes);
            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain(expectedStyle));
                Assert.That(html, Does.Contain("ChangedSrc"));
                Assert.That(html, Does.Not.Contain(GlobalConst.kStyleSurplus));
            });
        }
    }
}

using FWO.Data.Flow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowAccessPresentationHelperTest
    {
        [Test]
        public void BuildAccessSummary_ReturnsCompactSummary()
        {
            FlowAccess access = new()
            {
                Id = 77,
                AccessHash = "abc123",
                State = "implemented",
                Sources =
                [
                    new FlowAccessSource { NwObject = new FlowNwObject { Name = "src-a" } }
                ],
                Destinations =
                [
                    new FlowAccessDestination { NwObject = new FlowNwObject { Name = "dst-b" } }
                ],
                Services =
                [
                    new FlowAccessService { SvcObject = new FlowSvcObject { Name = "svc-c" } }
                ],
                TimeObjects =
                [
                    new FlowAccessTimeObject { TimeObject = new FlowTimeObject { Name = "office-hours" } }
                ]
            };

            string summary = FlowAccessPresentationHelper.BuildAccessSummary(access);

            Assert.That(summary, Does.Contain("S: src-a"));
            Assert.That(summary, Does.Contain("D: dst-b"));
            Assert.That(summary, Does.Contain("V: svc-c"));
            Assert.That(summary, Does.Contain("T: office-hours"));
        }

        [Test]
        public void BuildSearchText_ContainsRelevantFlowParts()
        {
            FlowAccess access = new()
            {
                Id = 77,
                AccessHash = "abc123",
                State = "implemented",
                Sources =
                [
                    new FlowAccessSource { NwObject = new FlowNwObject { Name = "src-a" } }
                ],
                SourceGroups =
                [
                    new FlowAccessSourceGroup { NwGroup = new FlowNwGroup { Name = "src-group" } }
                ]
            };

            string searchText = FlowAccessPresentationHelper.BuildSearchText(access);

            Assert.That(searchText, Does.Contain("77"));
            Assert.That(searchText, Does.Contain("abc123"));
            Assert.That(searchText, Does.Contain("implemented"));
            Assert.That(searchText, Does.Contain("src-a"));
            Assert.That(searchText, Does.Contain("src-group"));
        }
    }
}

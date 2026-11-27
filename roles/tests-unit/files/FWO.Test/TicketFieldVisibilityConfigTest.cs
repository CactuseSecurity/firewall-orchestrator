using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class TicketFieldVisibilityConfigTest
    {
        private static readonly string kEndOnlyFlowIntegration =
            new FlowIntegrationConfig { TimeObjectBounds = FlowIntegrationTimeObjectBoundsOptions.EndOnly }.ToConfigValue();
        private static readonly string kStartAndEndFlowIntegration = new FlowIntegrationConfig().ToConfigValue();

        [Test]
        public void ParseEffective_HidesValidFromWhenTimeObjectsAreEndOnly()
        {
            string visibility = new TicketFieldVisibilityConfig().ToConfigValue();

            AccessTaskFieldVisibilityConfig effective = TicketFieldVisibilityConfig.ParseEffective(visibility, kEndOnlyFlowIntegration).AccessTask;

            Assert.Multiple(() =>
            {
                Assert.That(effective.ShowValidFrom, Is.False);
                Assert.That(effective.ShowValidTo, Is.True);
                Assert.That(effective.ShowManagement, Is.True);
            });
        }

        [Test]
        public void ParseEffective_KeepsStoredVisibilityWhenTimeObjectsHaveStartAndEnd()
        {
            TicketFieldVisibilityConfig stored = new();
            stored.AccessTask.ShowValidTo = false;

            AccessTaskFieldVisibilityConfig effective = TicketFieldVisibilityConfig.ParseEffective(stored.ToConfigValue(), kStartAndEndFlowIntegration).AccessTask;

            Assert.Multiple(() =>
            {
                Assert.That(effective.ShowValidFrom, Is.True);
                Assert.That(effective.ShowValidTo, Is.False);
            });
        }

        [Test]
        public void ParseEffective_KeepsHiddenValidFromWhenTimeObjectsHaveStartAndEnd()
        {
            TicketFieldVisibilityConfig stored = new();
            stored.AccessTask.ShowValidFrom = false;

            AccessTaskFieldVisibilityConfig effective = TicketFieldVisibilityConfig.ParseEffective(stored.ToConfigValue(), kStartAndEndFlowIntegration).AccessTask;

            Assert.That(effective.ShowValidFrom, Is.False);
        }

        [Test]
        public void ParseEffective_UsesDefaultsForMissingConfigurations()
        {
            TicketFieldVisibilityConfig effective = TicketFieldVisibilityConfig.ParseEffective(null, null);

            Assert.Multiple(() =>
            {
                Assert.That(effective.AccessTask.ShowValidFrom, Is.True);
                Assert.That(effective.AccessTask.ShowValidTo, Is.True);
            });
        }

        [Test]
        public void ParseEffective_DoesNotChangeTheStoredConfiguration()
        {
            string visibility = new TicketFieldVisibilityConfig().ToConfigValue();

            TicketFieldVisibilityConfig.ParseEffective(visibility, kEndOnlyFlowIntegration);
            TicketFieldVisibilityConfig stored = TicketFieldVisibilityConfig.Parse(visibility);

            Assert.That(stored.AccessTask.ShowValidFrom, Is.True);
        }
    }
}

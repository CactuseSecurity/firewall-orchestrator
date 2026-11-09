using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class FlowIntegrationConfigTest
    {
        [Test]
        public void Parse_ReturnsDefaults_WhenConfigIsEmpty()
        {
            FlowIntegrationConfig config = FlowIntegrationConfig.Parse("");

            Assert.That(config.SelectObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(config.SelectServices, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(config.SelectTimeObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(config.TimeObjectPrecision, Is.EqualTo(FlowIntegrationTimePrecisionOptions.Seconds));
        }

        [Test]
        public void Parse_FallsBackToDefault_WhenOptionIsUnsupported()
        {
            FlowIntegrationConfig config = FlowIntegrationConfig.Parse("{\"select_objects\":\"unsupported\",\"select_services\":\"unsupported\",\"select_time_objects\":\"unsupported\",\"time_object_precision\":\"unsupported\"}");

            Assert.That(config.SelectObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(config.SelectServices, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(config.SelectTimeObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(config.TimeObjectPrecision, Is.EqualTo(FlowIntegrationTimePrecisionOptions.Seconds));
        }

        [Test]
        public void ToConfigValue_RoundTripsSelection()
        {
            FlowIntegrationConfig config = new()
            {
                SelectObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                SelectServices = FlowIntegrationObjectSelectionOptions.Manually,
                SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Minutes
            };

            string serialized = config.ToConfigValue();
            FlowIntegrationConfig parsed = FlowIntegrationConfig.Parse(serialized);

            Assert.That(parsed.SelectObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.FromFlowDb));
            Assert.That(parsed.SelectServices, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Manually));
            Assert.That(parsed.SelectTimeObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
            Assert.That(parsed.TimeObjectPrecision, Is.EqualTo(FlowIntegrationTimePrecisionOptions.Minutes));
        }
    }
}

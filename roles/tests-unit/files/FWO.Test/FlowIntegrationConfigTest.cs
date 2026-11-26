using FWO.Data.Flow;
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

        [Test]
        public void Parse_DefaultsToStartAndEnd_WhenTimeObjectBoundsAreMissing()
        {
            FlowIntegrationConfig config = FlowIntegrationConfig.Parse("{\"select_objects\":\"Both\",\"select_services\":\"Both\",\"select_time_objects\":\"Both\",\"time_object_precision\":\"date\"}");

            Assert.Multiple(() =>
            {
                Assert.That(config.TimeObjectBounds, Is.EqualTo(FlowIntegrationTimeObjectBoundsOptions.StartAndEnd));
                Assert.That(config.IsEndOnly, Is.False);
                Assert.That(config.TimeObjectPrecision, Is.EqualTo(FlowIntegrationTimePrecisionOptions.Date));
            });
        }

        [Test]
        public void Parse_FallsBackToStartAndEnd_WhenTimeObjectBoundsAreUnsupported()
        {
            FlowIntegrationConfig config = FlowIntegrationConfig.Parse("{\"time_object_bounds\":\"start_only\"}");

            Assert.Multiple(() =>
            {
                Assert.That(config.TimeObjectBounds, Is.EqualTo(FlowIntegrationTimeObjectBoundsOptions.StartAndEnd));
                Assert.That(config.IsEndOnly, Is.False);
            });
        }

        [Test]
        public void Parse_ReadsEndOnlyTimeObjectBounds()
        {
            FlowIntegrationConfig config = FlowIntegrationConfig.Parse("{\"time_object_bounds\":\"end_only\"}");

            Assert.Multiple(() =>
            {
                Assert.That(config.TimeObjectBounds, Is.EqualTo(FlowIntegrationTimeObjectBoundsOptions.EndOnly));
                Assert.That(config.IsEndOnly, Is.True);
            });
        }

        [Test]
        public void ToConfigValue_RoundTripsEndOnlyWithoutSerializingDerivedFlag()
        {
            FlowIntegrationConfig config = new() { TimeObjectBounds = FlowIntegrationTimeObjectBoundsOptions.EndOnly };

            string serialized = config.ToConfigValue();
            FlowIntegrationConfig parsed = FlowIntegrationConfig.Parse(serialized);

            Assert.Multiple(() =>
            {
                Assert.That(serialized, Does.Contain("\"time_object_bounds\":\"end_only\""));
                Assert.That(serialized, Does.Not.Contain(nameof(FlowIntegrationConfig.IsEndOnly)));
                Assert.That(parsed.IsEndOnly, Is.True);
            });
        }

        [Test]
        public void ToConfigValue_NormalizesUnsupportedTimeObjectBounds()
        {
            FlowIntegrationConfig config = new() { TimeObjectBounds = "unsupported" };

            config.ToConfigValue();

            Assert.That(config.TimeObjectBounds, Is.EqualTo(FlowIntegrationTimeObjectBoundsOptions.StartAndEnd));
        }

        [Test]
        public void IsSelectableTimeObject_OffersOnlyTimeObjectsWithoutStartWhenEndOnly()
        {
            FlowTimeObject withStart = new() { StartTime = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc), EndTime = new DateTime(2026, 7, 9, 18, 0, 0, DateTimeKind.Utc) };
            FlowTimeObject endOnly = new() { EndTime = new DateTime(2026, 7, 9, 18, 0, 0, DateTimeKind.Utc) };
            FlowIntegrationConfig startAndEndConfig = new();
            FlowIntegrationConfig endOnlyConfig = new() { TimeObjectBounds = FlowIntegrationTimeObjectBoundsOptions.EndOnly };

            Assert.Multiple(() =>
            {
                Assert.That(startAndEndConfig.IsSelectableTimeObject(withStart), Is.True);
                Assert.That(startAndEndConfig.IsSelectableTimeObject(endOnly), Is.True);
                Assert.That(endOnlyConfig.IsSelectableTimeObject(withStart), Is.False);
                Assert.That(endOnlyConfig.IsSelectableTimeObject(endOnly), Is.True);
            });
        }

        [Test]
        public void GetEffectiveStartTime_DropsStartOnlyWhenEndOnly()
        {
            DateTime requestedStart = new(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc);
            FlowIntegrationConfig startAndEnd = new();
            FlowIntegrationConfig endOnly = new() { TimeObjectBounds = FlowIntegrationTimeObjectBoundsOptions.EndOnly };

            Assert.Multiple(() =>
            {
                Assert.That(startAndEnd.GetEffectiveStartTime(requestedStart), Is.EqualTo(requestedStart));
                Assert.That(startAndEnd.GetEffectiveStartTime(null), Is.Null);
                Assert.That(endOnly.GetEffectiveStartTime(requestedStart), Is.Null);
            });
        }
    }
}

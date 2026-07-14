using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    internal class FlowPayloadMergerTest
    {
        [Test]
        public void MergeBundled_MergesOnlyPayloadsWithSameBundleId()
        {
            FlowCreationPayload first = CreatePayload(1, "10.0.0.1", "10.0.1.1", 443);
            FlowCreationPayload second = CreatePayload(2, "10.0.0.1", "10.0.1.2", 443);
            FlowCreationPayload third = CreatePayload(3, "10.0.0.1", "10.0.1.3", 443);
            first.BundleId = "flow-1-2";
            second.BundleId = "flow-1-2";

            List<FlowCreationPayload> result = new FlowPayloadMerger().MergeBundled([second, first, third]);

            Assert.That(result, Has.Count.EqualTo(2));
            FlowCreationPayload bundled = result.Single(payload => payload.BundleId == "flow-1-2");
            Assert.That(bundled.OriginRequestTaskIds, Is.EqualTo(new List<long> { 1, 2 }));
            Assert.That(bundled.Sources, Has.Count.EqualTo(2));
            Assert.That(bundled.Destinations, Has.Count.EqualTo(2));
            Assert.That(result.Single(payload => payload.OriginRequestTaskIds.SequenceEqual([3])).Destinations, Has.Count.EqualTo(1));
        }

        [Test]
        public void MergeBundled_DoesNotMergePayloadsFromDifferentManagements()
        {
            FlowCreationPayload first = CreatePayload(1, "10.0.0.1", "10.0.1.1", 443);
            FlowCreationPayload second = CreatePayload(2, "10.0.0.1", "10.0.1.2", 443);
            first.BundleId = "flow-1-2";
            second.BundleId = "flow-1-2";
            second.ManagementId = 3;

            List<FlowCreationPayload> result = new FlowPayloadMerger().MergeBundled([first, second]);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(payload => payload.ManagementId), Is.EquivalentTo(new int?[] { 2, 3 }));
            Assert.That(result.SelectMany(payload => payload.OriginRequestTaskIds), Is.EquivalentTo(new long[] { 1, 2 }));
        }

        [Test]
        public void MergeBundled_DoesNotMergePayloadsWithDifferentTimeObjects()
        {
            FlowCreationPayload first = CreatePayload(1, "10.0.0.1", "10.0.1.1", 443);
            FlowCreationPayload second = CreatePayload(2, "10.0.0.1", "10.0.1.2", 443);
            first.BundleId = "flow-1-2";
            second.BundleId = "flow-1-2";
            first.TimeEnd = new DateTime(2026, 7, 9, 23, 59, 0, DateTimeKind.Utc);
            second.TimeEnd = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc);

            List<FlowCreationPayload> result = new FlowPayloadMerger().MergeBundled([first, second]);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(payload => payload.TimeEnd), Is.EquivalentTo(new[]
            {
                new DateTime(2026, 7, 9, 23, 59, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc)
            }));
            Assert.That(result.SelectMany(payload => payload.OriginRequestTaskIds), Is.EquivalentTo(new long[] { 1, 2 }));
        }

        [Test]
        public void MergeBundled_DoesNotMergePayloadsWithDifferentRuleActions()
        {
            FlowCreationPayload first = CreatePayload(1, "10.0.0.1", "10.0.1.1", 443);
            FlowCreationPayload second = CreatePayload(2, "10.0.0.1", "10.0.1.2", 443);
            first.BundleId = "flow-1-2";
            second.BundleId = "flow-1-2";
            second.RuleActionId = 99;

            List<FlowCreationPayload> result = new FlowPayloadMerger().MergeBundled([first, second]);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(payload => payload.RuleActionId), Is.EquivalentTo(new int?[] { 1, 99 }));
            Assert.That(result.SelectMany(payload => payload.OriginRequestTaskIds), Is.EquivalentTo(new long[] { 1, 2 }));
        }

        private static FlowCreationPayload CreatePayload(long taskId, string sourceIp, string destinationIp, int port)
        {
            return new()
            {
                TicketId = 7,
                OwnerId = 3,
                TaskType = WfTaskType.access.ToString(),
                TaskAction = RequestAction.create.ToString(),
                RuleActionId = 1,
                ManagementId = 2,
                OriginRequestTaskIds = [taskId],
                Sources = [CreateObject(ElemFieldType.source, sourceIp)],
                Destinations = [CreateObject(ElemFieldType.destination, destinationIp)],
                Services = [new() { ProtoId = 6, Port = port, PortEnd = port, RequestAction = RequestAction.create.ToString() }]
            };
        }

        private static FlowObjectSnapshot CreateObject(ElemFieldType field, string ip)
        {
            return new()
            {
                Field = field,
                Ip = ip,
                IpEnd = ip,
                RequestAction = RequestAction.create.ToString()
            };
        }
    }
}

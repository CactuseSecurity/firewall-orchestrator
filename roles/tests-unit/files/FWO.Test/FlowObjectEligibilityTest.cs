using FWO.Basics;
using FWO.Data.Flow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the predicate that decides which Flow catalog entries the request module may offer and
    /// which a workflow element may be attached to (SEC-09).
    /// </summary>
    [TestFixture]
    internal class FlowObjectEligibilityTest
    {
        private static FlowNwObject RequestableNwObject()
        {
            return new FlowNwObject { Id = 1, Name = "host", State = FlowState.Implemented, ShowInRequestModule = true };
        }

        private static FlowSvcObject RequestableSvcObject()
        {
            return new FlowSvcObject { Id = 2, Name = "https", ProtoId = 6, State = FlowState.Implemented, ShowInRequestModule = true };
        }

        private static FlowTimeObject RequestableTimeObject()
        {
            return new FlowTimeObject { Id = 3, Name = "window", State = FlowState.Implemented, ShowInRequestModule = true };
        }

        private static FlowNwGroup RequestableGroup()
        {
            return new FlowNwGroup { Id = 4, Name = "AR-Group", State = FlowState.Implemented, ShowInRequestModule = true };
        }

        [Test]
        public void IsRequestable_AcceptsVisibleLiveEntries()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsRequestable(RequestableNwObject()), Is.True);
                Assert.That(FlowObjectEligibility.IsRequestable(RequestableSvcObject()), Is.True);
                Assert.That(FlowObjectEligibility.IsRequestable(RequestableTimeObject()), Is.True);
                Assert.That(FlowObjectEligibility.IsRequestable(RequestableGroup()), Is.True);
            });
        }

        [Test]
        public void IsRequestable_AcceptsRequestedState()
        {
            FlowNwObject nwObject = RequestableNwObject();
            nwObject.State = FlowState.Requested;

            Assert.That(FlowObjectEligibility.IsRequestable(nwObject), Is.True);
        }

        [Test]
        public void IsRequestable_RejectsNull()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsRequestable((FlowNwObject?)null), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable((FlowSvcObject?)null), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable((FlowTimeObject?)null), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable((FlowGroup?)null), Is.False);
            });
        }

        [Test]
        public void IsRequestable_RejectsEntriesHiddenFromTheRequestModule()
        {
            FlowNwObject nwObject = RequestableNwObject();
            nwObject.ShowInRequestModule = false;
            FlowSvcObject svcObject = RequestableSvcObject();
            svcObject.ShowInRequestModule = false;
            FlowTimeObject timeObject = RequestableTimeObject();
            timeObject.ShowInRequestModule = false;
            FlowNwGroup group = RequestableGroup();
            group.ShowInRequestModule = false;

            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsRequestable(nwObject), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable(svcObject), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable(timeObject), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable(group), Is.False);
            });
        }

        [Test]
        public void IsRequestable_RejectsRetiredEntries()
        {
            FlowNwObject nwObject = RequestableNwObject();
            nwObject.RemovedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            FlowSvcObject svcObject = RequestableSvcObject();
            svcObject.RemovedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsRequestable(nwObject), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable(svcObject), Is.False);
            });
        }

        [Test]
        [TestCase(FlowState.Denied)]
        [TestCase(FlowState.Removed)]
        public void IsRequestable_RejectsEntriesInADeadState(string state)
        {
            FlowNwObject nwObject = RequestableNwObject();
            nwObject.State = state;
            FlowNwGroup group = RequestableGroup();
            group.State = state;

            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsRequestable(nwObject), Is.False);
                Assert.That(FlowObjectEligibility.IsRequestable(group), Is.False);
            });
        }

        [Test]
        public void IsRequestable_RejectsUnnamedGroup()
        {
            FlowNwGroup group = RequestableGroup();
            group.Name = "  ";

            Assert.That(FlowObjectEligibility.IsRequestable(group), Is.False);
        }

        [Test]
        public void IsRequestable_RejectsTheInternalAnyProtocolServiceButIsLiveAcceptsIt()
        {
            FlowSvcObject anyService = RequestableSvcObject();
            anyService.ProtoId = GlobalConst.kAnyIpProtocolId;

            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsRequestable(anyService), Is.False);
                Assert.That(FlowObjectEligibility.IsLive(anyService), Is.True);
            });
        }

        [Test]
        public void IsLive_RejectsTheInternalAnyProtocolServiceWhenItIsRetired()
        {
            FlowSvcObject anyService = RequestableSvcObject();
            anyService.ProtoId = GlobalConst.kAnyIpProtocolId;
            anyService.RemovedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.That(FlowObjectEligibility.IsLive(anyService), Is.False);
        }

        [Test]
        [TestCase(-1, false)]
        [TestCase(-99, false)]
        [TestCase(0, true)]
        [TestCase(6, true)]
        public void IsInternalProtocolId_ClassifiesNegativeIdsAsInternal(int protoId, bool expectedRequestable)
        {
            Assert.Multiple(() =>
            {
                Assert.That(FlowObjectEligibility.IsInternalProtocolId(protoId), Is.EqualTo(!expectedRequestable));
                Assert.That(FlowObjectEligibility.IsRequestableProtocolId(protoId), Is.EqualTo(expectedRequestable));
            });
        }

        [Test]
        public void IsRequestableProtocolId_AcceptsAnElementWithoutAProtocol()
        {
            Assert.That(FlowObjectEligibility.IsRequestableProtocolId(null), Is.True);
        }
    }
}

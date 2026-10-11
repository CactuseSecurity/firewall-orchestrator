using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfObjectTaskHelperTest
    {
        private const string kHostIp = "10.1.1.5";
        private const string kHostCidr = "10.1.1.5/32";
        private const string kNetworkCidr = "10.1.2.0/24";
        private const string kRangeInput = "10.1.3.10-10.1.3.20";
        private const int kTcp = 6;
        private const int kIcmp = 1;
        private static readonly List<long> kRemovedElementIds = [103];
        private static readonly List<int> kNetworkObjectTypeIds = [1, 3, 4, 12];
        private static readonly List<int> kServiceTypeIds = [1];

        [Test]
        public void IsObjectTask_RecognizesOnlyTheObjectTaskTypes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.IsObjectTask(WfTaskType.object_create), Is.True);
                Assert.That(WfObjectTaskHelper.IsObjectTask(WfTaskType.object_modify), Is.True);
                Assert.That(WfObjectTaskHelper.IsObjectTask(WfTaskType.group_create), Is.False);
                Assert.That(WfObjectTaskHelper.IsObjectTask(WfTaskType.object_modify.ToString()), Is.True);
                Assert.That(WfObjectTaskHelper.IsObjectTask(WfTaskType.access.ToString()), Is.False);
                Assert.That(WfObjectTaskHelper.IsObjectTask((string?)null), Is.False);
            });
        }

        [Test]
        public void ToNwObjectElement_ReferencesTheImportedObject()
        {
            NetworkObject networkObject = new() { Id = 4711, Name = "srv_web01", IP = kHostCidr, IpEnd = kHostCidr };

            NwObjectElement element = WfObjectTaskHelper.ToNwObjectElement(networkObject, RequestAction.unchanged.ToString(), 3);

            Assert.Multiple(() =>
            {
                Assert.That(element.NetworkId, Is.EqualTo(4711));
                Assert.That(element.Name, Is.EqualTo("srv_web01"));
                Assert.That(element.TaskId, Is.EqualTo(3));
                Assert.That(element.RequestAction, Is.EqualTo(RequestAction.unchanged.ToString()));
                Assert.That(WfObjectTaskHelper.FormatNetworkInput(element), Is.EqualTo(kHostIp));
            });
        }

        [Test]
        public void ToNwObjectElement_KeepsTheEndOfAnAddressRange()
        {
            NetworkObject networkObject = new() { Id = 1, Name = "range", IP = "10.1.3.10/32", IpEnd = "10.1.3.20/32" };

            NwObjectElement element = WfObjectTaskHelper.ToNwObjectElement(networkObject, RequestAction.unchanged.ToString(), 0);

            Assert.That(WfObjectTaskHelper.FormatNetworkInput(element), Is.EqualTo(kRangeInput));
        }

        [Test]
        public void ToNwServiceElement_ReferencesTheImportedService()
        {
            NetworkService service = new() { Id = 815, Name = "https_alt", ProtoId = kTcp, DestinationPort = 8443, DestinationPortEnd = 8443 };

            NwServiceElement element = WfObjectTaskHelper.ToNwServiceElement(service, RequestAction.unchanged.ToString(), 2);

            Assert.Multiple(() =>
            {
                Assert.That(element.ServiceId, Is.EqualTo(815));
                Assert.That(element.ProtoId, Is.EqualTo(kTcp));
                Assert.That(element.HasProtocol, Is.True);
                Assert.That(element.Port, Is.EqualTo(8443));
                Assert.That(element.TaskId, Is.EqualTo(2));
            });
        }

        [TestCase(kHostIp, kHostIp)]
        [TestCase(kNetworkCidr, kNetworkCidr)]
        [TestCase(kRangeInput, kRangeInput)]
        public void TryParseNetworkInput_RoundTripsThroughFormatNetworkInput(string input, string expected)
        {
            bool parsed = WfObjectTaskHelper.TryParseNetworkInput(input, 0, out NwObjectElement element);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(WfObjectTaskHelper.HasNetworkMinimum(element), Is.True);
                Assert.That(WfObjectTaskHelper.FormatNetworkInput(element), Is.EqualTo(expected));
            });
        }

        [TestCase("")]
        [TestCase("not an ip")]
        [TestCase("10.1.1.300")]
        public void TryParseNetworkInput_RejectsInvalidInput(string input)
        {
            Assert.That(WfObjectTaskHelper.TryParseNetworkInput(input, 0, out _), Is.False);
        }

        [Test]
        public void HasNetworkMinimum_IsFalseWithoutAddress()
        {
            Assert.That(WfObjectTaskHelper.HasNetworkMinimum(new NwObjectElement()), Is.False);
        }

        [TestCase("443", 443, null)]
        [TestCase("8000-8080", 8000, 8080)]
        [TestCase(" 22 - 23 ", 22, 23)]
        public void TryParsePortInput_ReadsPortAndRange(string input, int expectedPort, int? expectedPortEnd)
        {
            bool parsed = WfObjectTaskHelper.TryParsePortInput(input, out int? port, out int? portEnd);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(port, Is.EqualTo(expectedPort));
                Assert.That(portEnd, Is.EqualTo(expectedPortEnd));
            });
        }

        [TestCase("")]
        [TestCase("http")]
        [TestCase("80-x")]
        public void TryParsePortInput_RejectsInvalidInput(string input)
        {
            bool parsed = WfObjectTaskHelper.TryParsePortInput(input, out int? port, out _);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.False);
                Assert.That(port, Is.Null);
            });
        }

        [Test]
        public void FormatPortInput_WritesPortOrRange()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.FormatPortInput(null, null), Is.EqualTo(""));
                Assert.That(WfObjectTaskHelper.FormatPortInput(443, null), Is.EqualTo("443"));
                Assert.That(WfObjectTaskHelper.FormatPortInput(443, 443), Is.EqualTo("443"));
                Assert.That(WfObjectTaskHelper.FormatPortInput(8000, 8080), Is.EqualTo("8000-8080"));
            });
        }

        [Test]
        public void HasServiceMinimum_ChecksProtocolAndPorts()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp, Port = 443 }), Is.True);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp, Port = 8000, PortEnd = 8080 }), Is.True);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kIcmp }), Is.True);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp }), Is.False);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp, Port = 0 }), Is.False);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp, Port = 70000 }), Is.False);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp, Port = 8080, PortEnd = 8000 }), Is.False);
                Assert.That(WfObjectTaskHelper.HasServiceMinimum(new() { ProtoId = kTcp, Port = 80, HasProtocol = false }), Is.False);
            });
        }

        [Test]
        public void IsUnchanged_ComparesAddressesIndependentlyOfNotation()
        {
            NwObjectElement imported = WfObjectTaskHelper.ToNwObjectElement(new() { Id = 1, Name = "srv", IP = kHostCidr, IpEnd = kHostCidr }, RequestAction.unchanged.ToString(), 0);
            WfObjectTaskHelper.TryParseNetworkInput(kHostIp, 0, out NwObjectElement sameAddress);
            sameAddress.Name = "srv";
            WfObjectTaskHelper.TryParseNetworkInput("10.1.1.6", 0, out NwObjectElement otherAddress);
            otherAddress.Name = "srv";
            WfObjectTaskHelper.TryParseNetworkInput(kHostIp, 0, out NwObjectElement otherName);
            otherName.Name = "srv_new";

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.IsUnchanged(imported, sameAddress), Is.True);
                Assert.That(WfObjectTaskHelper.IsUnchanged(imported, otherAddress), Is.False);
                Assert.That(WfObjectTaskHelper.IsUnchanged(imported, otherName), Is.False);
            });
        }

        [Test]
        public void IsUnchanged_TreatsAMissingPortEndAsSinglePort()
        {
            NwServiceElement imported = new() { Name = "https", ProtoId = kTcp, Port = 443, PortEnd = 443 };

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.IsUnchanged(imported, new() { Name = "https", ProtoId = kTcp, Port = 443 }), Is.True);
                Assert.That(WfObjectTaskHelper.IsUnchanged(imported, new() { Name = "https", ProtoId = kTcp, Port = 8443 }), Is.False);
                Assert.That(WfObjectTaskHelper.IsUnchanged(imported, new() { Name = "https", ProtoId = 17, Port = 443 }), Is.False);
            });
        }

        [Test]
        public void HasValidElementStructure_AcceptsOneCreateElementForObjectCreate()
        {
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            task.Elements.Add(new() { Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.create.ToString() });

            Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);

            task.Elements.Add(new() { Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.create.ToString() });
            Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.False);
        }

        [Test]
        public void HasValidElementStructure_RequiresAnUnchangedAndAModifyElementOfTheSameObject()
        {
            WfReqTask task = CreateModifyTask(4711, 4711);
            WfReqTask otherObject = CreateModifyTask(4711, 4712);
            WfReqTask noReference = CreateModifyTask(null, null);
            WfReqTask wrongType = CreateModifyTask(4711, 4711);
            wrongType.TaskType = WfTaskType.group_modify.ToString();

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(otherObject), Is.False);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(noReference), Is.False);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(wrongType), Is.False);
                Assert.That(WfObjectTaskHelper.GetOriginalElement(task)?.RequestAction, Is.EqualTo(RequestAction.unchanged.ToString()));
                Assert.That(WfObjectTaskHelper.GetRequestedElement(task)?.RequestAction, Is.EqualTo(RequestAction.modify.ToString()));
            });
        }

        [Test]
        public void HasValidElementStructure_ComparesTheServiceReferenceForServices()
        {
            WfReqTask task = new() { TaskType = WfTaskType.object_modify.ToString() };
            task.Elements.Add(new() { Field = ElemFieldType.service.ToString(), RequestAction = RequestAction.unchanged.ToString(), ServiceId = 9 });
            task.Elements.Add(new() { Field = ElemFieldType.service.ToString(), RequestAction = RequestAction.modify.ToString(), ServiceId = 9 });

            Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
        }

        [Test]
        public void ReplaceElements_ReusesIdsPerRequestActionAndRemovesTheRest()
        {
            WfReqTask task = new() { Id = 12, TaskType = WfTaskType.object_modify.ToString() };
            task.Elements.Add(new() { Id = 101, RequestAction = RequestAction.unchanged.ToString() });
            task.Elements.Add(new() { Id = 102, RequestAction = RequestAction.modify.ToString() });
            task.Elements.Add(new() { Id = 103, RequestAction = RequestAction.create.ToString() });
            WfReqElement newOriginal = new() { RequestAction = RequestAction.unchanged.ToString() };
            WfReqElement newRequested = new() { RequestAction = RequestAction.modify.ToString() };

            List<WfReqElement> newElements = [newOriginal, newRequested];
            WfObjectTaskHelper.ReplaceElements(task, newElements);

            Assert.Multiple(() =>
            {
                Assert.That(task.Elements, Has.Count.EqualTo(2));
                Assert.That(newOriginal.Id, Is.EqualTo(101));
                Assert.That(newRequested.Id, Is.EqualTo(102));
                Assert.That(newRequested.TaskId, Is.EqualTo(12));
                Assert.That(task.RemovedElements.ConvertAll(element => element.Id), Is.EqualTo(kRemovedElementIds));
            });
        }

        [Test]
        public void ReplaceElements_LeavesNewElementsWithoutIdForANewTask()
        {
            WfReqTask task = new();
            WfReqElement requested = new() { RequestAction = RequestAction.create.ToString() };

            List<WfReqElement> newElements = [requested];
            WfObjectTaskHelper.ReplaceElements(task, newElements);

            Assert.Multiple(() =>
            {
                Assert.That(requested.Id, Is.EqualTo(0));
                Assert.That(task.RemovedElements, Is.Empty);
            });
        }

        [Test]
        public void SearchConstants_MatchTheRequesterPermission()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.kSearchLimit, Is.EqualTo(50));
                Assert.That(WfObjectTaskHelper.kMinSearchLength, Is.EqualTo(3));
                Assert.That(WfObjectTaskHelper.NetworkObjectTypeIds, Is.EquivalentTo(kNetworkObjectTypeIds));
                Assert.That(WfObjectTaskHelper.ServiceTypeIds, Is.EquivalentTo(kServiceTypeIds));
            });
        }

        private static WfReqTask CreateModifyTask(long? originalId, long? requestedId)
        {
            WfReqTask task = new() { TaskType = WfTaskType.object_modify.ToString() };
            WfObjectTaskHelper.TryParseNetworkInput(kHostIp, 0, out NwObjectElement original);
            original.NetworkId = originalId;
            original.RequestAction = RequestAction.unchanged.ToString();
            WfObjectTaskHelper.TryParseNetworkInput("10.1.1.6", 0, out NwObjectElement requested);
            requested.NetworkId = requestedId;
            requested.RequestAction = RequestAction.modify.ToString();
            task.Elements.Add(original.ToReqElement(ElemFieldType.source));
            task.Elements.Add(requested.ToReqElement(ElemFieldType.source));
            return task;
        }
    }
}

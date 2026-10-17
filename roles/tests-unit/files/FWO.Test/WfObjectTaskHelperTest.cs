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
        private static readonly DateTime kTimeStart = new(2026, 11, 1, 8, 0, 0);
        private static readonly DateTime kTimeEnd = new(2026, 11, 30, 18, 0, 0);

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
        public void IsManagementObjectTask_RecognizesGroupAndObjectTaskTypes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.group_create.ToString()), Is.True);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.group_modify.ToString()), Is.True);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.group_delete.ToString()), Is.True);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.object_create.ToString()), Is.True);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.object_modify.ToString()), Is.True);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.access.ToString()), Is.False);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(WfTaskType.rule_delete.ToString()), Is.False);
                Assert.That(WfObjectTaskHelper.IsManagementObjectTask(null), Is.False);
            });
        }

        [Test]
        public void OrdersServiceObject_DetectsServiceElementsAndServiceGroups()
        {
            WfReqTask networkTask = new();
            networkTask.Elements.Add(new WfReqElement { Field = ElemFieldType.source.ToString() });
            WfReqTask serviceTask = new();
            serviceTask.Elements.Add(new WfReqElement { Field = ElemFieldType.service.ToString() });
            WfReqTask serviceGroupTask = new();
            serviceGroupTask.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "12");

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.OrdersServiceObject(networkTask), Is.False);
                Assert.That(WfObjectTaskHelper.OrdersServiceObject(serviceTask), Is.True);
                Assert.That(WfObjectTaskHelper.OrdersServiceObject(serviceGroupTask), Is.True);
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

        [Test]
        public void GetCreationKind_PrefersTimeThenServiceThenNetwork()
        {
            WfReqTask timeTask = new();
            timeTask.Elements.Add(new WfReqElement { Field = ElemFieldType.time.ToString() });
            WfReqTask serviceGroupTask = new();
            serviceGroupTask.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "12");
            WfReqTask networkTask = new();
            networkTask.Elements.Add(new WfReqElement { Field = ElemFieldType.source.ToString() });

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.GetCreationKind(timeTask), Is.EqualTo(ObjectTaskKind.Time));
                Assert.That(WfObjectTaskHelper.GetCreationKind(serviceGroupTask), Is.EqualTo(ObjectTaskKind.Service));
                Assert.That(WfObjectTaskHelper.GetCreationKind(networkTask), Is.EqualTo(ObjectTaskKind.Network));
                Assert.That(WfObjectTaskHelper.GetCreationKind(new WfReqTask()), Is.EqualTo(ObjectTaskKind.Network));
            });
        }

        [Test]
        public void GetKind_DistinguishesNetworkServiceAndTimeElements()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.GetKind(new WfReqElement { Field = ElemFieldType.source.ToString() }), Is.EqualTo(ObjectTaskKind.Network));
                Assert.That(WfObjectTaskHelper.GetKind(new WfReqElement { Field = ElemFieldType.service.ToString() }), Is.EqualTo(ObjectTaskKind.Service));
                Assert.That(WfObjectTaskHelper.GetKind(new WfReqElement { Field = ElemFieldType.time.ToString() }), Is.EqualTo(ObjectTaskKind.Time));
                Assert.That(WfObjectTaskHelper.GetKind(null), Is.EqualTo(ObjectTaskKind.Network));
            });
        }

        [Test]
        public void ToTimeElement_ReferencesTheImportedTimeObject()
        {
            TimeObject timeObject = new() { Id = 77, Name = "maintenance", StartTime = kTimeStart, EndTime = kTimeEnd };

            WfReqElement element = WfObjectTaskHelper.ToTimeElement(timeObject, RequestAction.unchanged.ToString(), 12);

            Assert.Multiple(() =>
            {
                Assert.That(element.Field, Is.EqualTo(ElemFieldType.time.ToString()));
                Assert.That(element.TimeObjId, Is.EqualTo(77));
                Assert.That(element.Name, Is.EqualTo("maintenance"));
                Assert.That(element.StartTime, Is.EqualTo(kTimeStart));
                Assert.That(element.EndTime, Is.EqualTo(kTimeEnd));
                Assert.That(element.TaskId, Is.EqualTo(12));
                Assert.That(element.RequestAction, Is.EqualTo(RequestAction.unchanged.ToString()));
            });
        }

        [Test]
        public void HasTimeMinimum_RequiresNameAndABoundInTheRightOrder()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement("tw", kTimeStart, kTimeEnd)), Is.True);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement("tw", kTimeStart, null)), Is.True);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement("tw", null, kTimeEnd)), Is.True);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement("tw", kTimeStart, kTimeStart)), Is.True);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement("tw", kTimeEnd, kTimeStart)), Is.False);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement("tw", null, null)), Is.False);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement(" ", kTimeStart, kTimeEnd)), Is.False);
                Assert.That(WfObjectTaskHelper.HasTimeMinimum(CreateTimeElement(null, kTimeStart, kTimeEnd)), Is.False);
            });
        }

        [Test]
        public void IsUnchangedTimeObject_ComparesNameAndPointsInTime()
        {
            WfReqElement original = CreateTimeElement("tw", kTimeStart.ToUniversalTime(), kTimeEnd);
            DateTime localStart = DateTime.SpecifyKind(kTimeStart, DateTimeKind.Local);

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.IsUnchangedTimeObject(original, CreateTimeElement("tw", localStart, kTimeEnd)), Is.True);
                Assert.That(WfObjectTaskHelper.IsUnchangedTimeObject(original, CreateTimeElement("tw2", kTimeStart, kTimeEnd)), Is.False);
                Assert.That(WfObjectTaskHelper.IsUnchangedTimeObject(original, CreateTimeElement("tw", kTimeStart, kTimeEnd.AddMinutes(1))), Is.False);
                Assert.That(WfObjectTaskHelper.IsUnchangedTimeObject(original, CreateTimeElement("tw", kTimeStart, null)), Is.False);
                Assert.That(WfObjectTaskHelper.IsUnchangedTimeObject(CreateTimeElement("tw", null, null), CreateTimeElement("tw", null, null)), Is.True);
            });
        }

        [Test]
        public void HasValidElementStructure_ComparesTheTimeObjectReferenceForTimeObjects()
        {
            WfReqTask task = CreateTimeModifyTask(5, 5);
            WfReqTask otherObject = CreateTimeModifyTask(5, 6);
            WfReqTask noReference = CreateTimeModifyTask(null, null);

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(otherObject), Is.False);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(noReference), Is.False);
            });
        }

        [Test]
        public void FormatTimeRange_ShowsBothOrOpenBounds()
        {
            DateTime utcStart = new(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc);
            string expectedUtcStart = utcStart.ToLocalTime().ToString(WfObjectTaskHelper.kTimeFormat, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.FormatTimeRange(kTimeStart, kTimeEnd), Is.EqualTo("2026-11-01 08:00 - 2026-11-30 18:00"));
                Assert.That(WfObjectTaskHelper.FormatTimeRange(kTimeStart, null), Is.EqualTo(">= 2026-11-01 08:00"));
                Assert.That(WfObjectTaskHelper.FormatTimeRange(null, kTimeEnd), Is.EqualTo("<= 2026-11-30 18:00"));
                Assert.That(WfObjectTaskHelper.FormatTimeRange(null, null), Is.Empty);
                Assert.That(WfObjectTaskHelper.FormatTimeRange(utcStart, null), Is.EqualTo(">= " + expectedUtcStart));
            });
        }

        [Test]
        public void TryParseDateTimeInput_CombinesDateAndTime()
        {
            bool complete = WfObjectTaskHelper.TryParseDateTimeInput("2026-11-01", "08:00", WfObjectTaskHelper.kDefaultStartTimeInput, out DateTime? pointInTime);
            bool withSeconds = WfObjectTaskHelper.TryParseDateTimeInput("2026-11-30", "18:00:00", WfObjectTaskHelper.kDefaultEndTimeInput, out DateTime? withSecondsTime);
            bool dateOnly = WfObjectTaskHelper.TryParseDateTimeInput("2026-11-30", " ", WfObjectTaskHelper.kDefaultEndTimeInput, out DateTime? dateOnlyTime);

            Assert.Multiple(() =>
            {
                Assert.That(complete, Is.True);
                Assert.That(pointInTime, Is.EqualTo(kTimeStart));
                Assert.That(withSeconds, Is.True);
                Assert.That(withSecondsTime, Is.EqualTo(kTimeEnd));
                Assert.That(dateOnly, Is.True);
                Assert.That(dateOnlyTime, Is.EqualTo(new DateTime(2026, 11, 30, 23, 59, 0)));
            });
        }

        [TestCase("", "", true)]
        [TestCase("", "08:00", false)]
        [TestCase("01.11.2026", "08:00", false)]
        [TestCase("2026-11-01", "8 Uhr", false)]
        public void TryParseDateTimeInput_HandlesEmptyIncompleteAndInvalidInput(string dateInput, string timeInput, bool expectedValid)
        {
            bool valid = WfObjectTaskHelper.TryParseDateTimeInput(dateInput, timeInput, WfObjectTaskHelper.kDefaultStartTimeInput, out DateTime? pointInTime);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.EqualTo(expectedValid));
                Assert.That(pointInTime, Is.Null);
            });
        }

        [Test]
        public void FormatDateAndTimeInput_SplitAPointInTimeInLocalTime()
        {
            DateTime utcStart = new(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc);
            DateTime localStart = utcStart.ToLocalTime();

            Assert.Multiple(() =>
            {
                Assert.That(WfObjectTaskHelper.FormatDateInput(kTimeStart), Is.EqualTo("2026-11-01"));
                Assert.That(WfObjectTaskHelper.FormatTimeInput(kTimeStart), Is.EqualTo("08:00"));
                Assert.That(WfObjectTaskHelper.FormatDateInput(null), Is.Empty);
                Assert.That(WfObjectTaskHelper.FormatTimeInput(null), Is.Empty);
                Assert.That(WfObjectTaskHelper.FormatTimeInput(utcStart), Is.EqualTo(localStart.ToString(WfObjectTaskHelper.kTimeInputFormat, System.Globalization.CultureInfo.InvariantCulture)));
            });
        }

        private static WfReqElement CreateTimeElement(string? name, DateTime? startTime, DateTime? endTime)
        {
            return new() { Field = ElemFieldType.time.ToString(), Name = name, StartTime = startTime, EndTime = endTime };
        }

        private static WfReqTask CreateTimeModifyTask(long? originalId, long? requestedId)
        {
            WfReqTask task = new() { TaskType = WfTaskType.object_modify.ToString() };
            WfReqElement original = CreateTimeElement("tw", kTimeStart, kTimeEnd);
            original.TimeObjId = originalId;
            original.RequestAction = RequestAction.unchanged.ToString();
            WfReqElement requested = CreateTimeElement("tw", kTimeStart, kTimeEnd.AddDays(1));
            requested.TimeObjId = requestedId;
            requested.RequestAction = RequestAction.modify.ToString();
            task.Elements.Add(original);
            task.Elements.Add(requested);
            return task;
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

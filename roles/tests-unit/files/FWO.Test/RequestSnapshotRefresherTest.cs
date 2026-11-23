using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class RequestSnapshotRefresherTest
{
    private static readonly List<long> kStaleElementIds = [201, 301];
    private static readonly List<long> kChangedTaskIds = [20, 30];
    private static readonly List<string> kCurrentAddresses = ["10.0.1.10/32", "10.0.1.10/32"];

    [Test]
    public void CreatePlan_RefreshesEveryStaleExactReferenceInTicket()
    {
        WfTicket ticket = CreateNetworkTicket();

        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Replacements.Select(element => element.Id), Is.EqualTo(kStaleElementIds));
            Assert.That(plan.ChangedTaskIds, Is.EqualTo(kChangedTaskIds));
            Assert.That(plan.Replacements.Select(element => element.IpString),
                Is.EqualTo(kCurrentAddresses));
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.0.10/32"));
            Assert.That(Element(ticket, 301).IpString, Is.EqualTo("10.0.0.10/32"));
        });
    }

    [Test]
    public void CreatePlan_LeavesWholeGroupAndCatalogReferencesUnchanged()
    {
        WfTicket ticket = CreateNetworkTicket();

        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Replacements.Select(element => element.Id), Does.Contain(201));
            Assert.That(plan.Replacements.Select(element => element.Id), Does.Not.Contain(203));
            Assert.That(plan.Replacements.Select(element => element.Id), Does.Not.Contain(302));
        });
    }

    [TestCase(WfTaskType.group_create)]
    [TestCase(WfTaskType.group_modify)]
    public void CreatePlan_RejectsNestedTicketLocalGroupMember(WfTaskType taskType)
    {
        WfTicket ticket = CreateNetworkTicket();
        WfReqTask containingGroup = ticket.Tasks.Single(task => task.Id == 20);
        containingGroup.TaskType = taskType.ToString();
        WfReqTask referencedGroup = new()
        {
            Id = 21,
            TicketId = 100,
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = 211,
                    TaskId = 21,
                    NetworkId = 77,
                    Field = ElemFieldType.source.ToString()
                }
            ]
        };
        ticket.Tasks.Add(referencedGroup);
        Element(ticket, 201).ObjectTaskId = referencedGroup.Id;

        Assert.Throws<InvalidDataException>(() => RequestSnapshotRefresher.CreatePlan(ticket));
    }

    [TestCase(WfTaskType.group_create, null, null)]
    [TestCase(WfTaskType.group_create, 0L, null)]
    [TestCase(WfTaskType.group_create, -1L, null)]
    [TestCase(WfTaskType.group_create, null, 0L)]
    [TestCase(WfTaskType.group_create, null, -1L)]
    [TestCase(WfTaskType.group_modify, null, null)]
    [TestCase(WfTaskType.group_modify, 0L, null)]
    [TestCase(WfTaskType.group_modify, -1L, null)]
    [TestCase(WfTaskType.group_modify, null, 0L)]
    [TestCase(WfTaskType.group_modify, null, -1L)]
    public void CreatePlan_RejectsGroupMemberWithoutStableIdentity(WfTaskType taskType,
        long? objectTaskId, long? networkId)
    {
        WfTicket ticket = CreateNetworkTicket();
        WfReqTask group = ticket.Tasks.Single(task => task.Id == 20);
        group.TaskType = taskType.ToString();
        WfReqElement member = Element(ticket, 203);
        member.ObjectTaskId = objectTaskId;
        member.NetworkId = networkId;

        Assert.Throws<InvalidDataException>(() => RequestSnapshotRefresher.CreatePlan(ticket));
    }

    [Test]
    public void CreatePlan_AcceptsFlowBackedGroupMemberWithoutObjectTaskReference()
    {
        WfTicket ticket = CreateNetworkTicket();
        WfReqElement member = Element(ticket, 203);
        member.NetworkId = null;
        member.FlowNetworkObjectId = 77;

        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);

        Assert.That(plan.Replacements.Select(element => element.Id), Does.Not.Contain(member.Id));
    }

    [Test]
    public void CreatePlan_RefreshesNetworkServiceAndTimeValues()
    {
        DateTime start = new(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);
        WfReqTask network = ObjectTask(10, ElemFieldType.source, "network-current", "10.1.0.0/24");
        network.Elements[0].IpEnd = "10.1.0.255/32";
        WfReqTask service = ObjectTask(11, ElemFieldType.service, "service-current");
        service.Elements[0].ProtoId = 6;
        service.Elements[0].Port = 443;
        service.Elements[0].PortEnd = 444;
        service.Elements[0].GroupName = "service-group-current";
        WfReqTask time = ObjectTask(12, ElemFieldType.time, "time-current");
        time.Elements[0].StartTime = start;
        time.Elements[0].EndTime = end;
        WfReqTask consumer = new()
        {
            Id = 20,
            TicketId = 100,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                Reference(201, 20, 10, ElemFieldType.destination, "network-stale", "192.0.2.0/24"),
                Reference(202, 20, 11, ElemFieldType.service, "service-stale", port: 80, protoId: 17),
                Reference(203, 20, 12, ElemFieldType.time, "time-stale", startTime: start.AddHours(-1), endTime: end.AddHours(-1))
            ]
        };
        consumer.Elements[0].NetworkId = 501;
        consumer.Elements[0].FlowNetworkObjectId = 601;
        consumer.Elements[1].ServiceId = 502;
        consumer.Elements[1].FlowServiceObjectId = 602;
        WfTicket ticket = Ticket(network, service, time, consumer);

        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);

        WfReqElement networkReplacement = plan.Replacements.Single(element => element.Id == 201);
        WfReqElement serviceReplacement = plan.Replacements.Single(element => element.Id == 202);
        WfReqElement timeReplacement = plan.Replacements.Single(element => element.Id == 203);
        Assert.Multiple(() =>
        {
            Assert.That(networkReplacement.Name, Is.EqualTo("network-current"));
            Assert.That(networkReplacement.IpString, Is.EqualTo("10.1.0.0/24"));
            Assert.That(networkReplacement.IpEnd, Is.EqualTo("10.1.0.255/32"));
            Assert.That(networkReplacement.Field, Is.EqualTo(ElemFieldType.destination.ToString()));
            Assert.That(networkReplacement.RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(networkReplacement.ObjectTaskId, Is.EqualTo(10));
            Assert.That(networkReplacement.NetworkId, Is.EqualTo(501));
            Assert.That(networkReplacement.FlowNetworkObjectId, Is.EqualTo(601));
            Assert.That(serviceReplacement.Name, Is.EqualTo("service-current"));
            Assert.That(serviceReplacement.ProtoId, Is.EqualTo(6));
            Assert.That(serviceReplacement.Port, Is.EqualTo(443));
            Assert.That(serviceReplacement.PortEnd, Is.EqualTo(444));
            Assert.That(serviceReplacement.GroupName, Is.EqualTo("service-group-current"));
            Assert.That(serviceReplacement.ServiceId, Is.EqualTo(502));
            Assert.That(serviceReplacement.FlowServiceObjectId, Is.EqualTo(602));
            Assert.That(timeReplacement.Name, Is.EqualTo("time-current"));
            Assert.That(timeReplacement.StartTime, Is.EqualTo(start));
            Assert.That(timeReplacement.EndTime, Is.EqualTo(end));
        });
    }

    [Test]
    public void CreatePlan_TreatsEquivalentValuesAsCurrent()
    {
        DateTime instant = new(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc);
        WfReqTask network = ObjectTask(10, ElemFieldType.source, "same", "10.2.0.1/24");
        WfReqTask time = ObjectTask(11, ElemFieldType.time, "same-time");
        time.Elements[0].StartTime = instant;
        time.Elements[0].EndTime = instant.AddHours(1);
        WfReqTask consumer = new()
        {
            Id = 20,
            TicketId = 100,
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                Reference(201, 20, 10, ElemFieldType.source, "same", "10.2.0.0/24"),
                Reference(202, 20, 11, ElemFieldType.time, "same-time",
                    startTime: instant.ToLocalTime(), endTime: instant.AddHours(1).ToLocalTime())
            ]
        };
        WfTicket ticket = Ticket(network, time, consumer);

        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);

        Assert.That(plan.HasChanges, Is.False);
    }

    [TestCase(InvalidReference.MissingTask)]
    [TestCase(InvalidReference.NonObjectTask)]
    [TestCase(InvalidReference.InvalidStructure)]
    [TestCase(InvalidReference.IncompatibleField)]
    [TestCase(InvalidReference.NonPersistedTarget)]
    public void CreatePlan_RejectsInvalidExactReference(InvalidReference invalidReference)
    {
        WfTicket ticket = CreateNetworkTicket();
        WfReqElement target = Element(ticket, 201);
        switch (invalidReference)
        {
            case InvalidReference.MissingTask:
                target.ObjectTaskId = 999;
                break;
            case InvalidReference.NonObjectTask:
                ticket.Tasks.Single(task => task.Id == 10).TaskType = WfTaskType.access.ToString();
                break;
            case InvalidReference.InvalidStructure:
                ticket.Tasks.Single(task => task.Id == 10).Elements.Add(new WfReqElement());
                break;
            case InvalidReference.IncompatibleField:
                target.Field = ElemFieldType.service.ToString();
                break;
            case InvalidReference.NonPersistedTarget:
                target.Id = 0;
                break;
        }

        Assert.Throws<InvalidDataException>(() => RequestSnapshotRefresher.CreatePlan(ticket));
        Assert.That(target.IpString, Is.EqualTo("10.0.0.10/32"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CreatePlan_RejectsDuplicatePersistedElementIds(bool acrossTasks)
    {
        WfTicket ticket = CreateNetworkTicket();
        WfReqTask firstConsumer = ticket.Tasks.Single(task => task.Id == 20);
        if (acrossTasks)
        {
            Element(ticket, 301).Id = 201;
        }
        else
        {
            firstConsumer.Elements.Add(new WfReqElement(firstConsumer.Elements[0]));
        }

        Assert.Throws<InvalidDataException>(() => RequestSnapshotRefresher.CreatePlan(ticket));
    }

    [Test]
    public void CreatePlan_RejectsElementWhoseTaskIdDoesNotMatchItsOwner()
    {
        WfTicket ticket = CreateNetworkTicket();
        Element(ticket, 201).TaskId = 999;

        Assert.Throws<InvalidDataException>(() => RequestSnapshotRefresher.CreatePlan(ticket));
    }

    [Test]
    public void ApplyPlan_ChangesOnlyPlannedValueFields()
    {
        WfTicket ticket = CreateNetworkTicket();
        WfReqElement target = Element(ticket, 201);
        long id = target.Id;
        long taskId = target.TaskId;
        string field = target.Field;
        string requestAction = target.RequestAction;
        long? objectTaskId = target.ObjectTaskId;
        long? networkId = target.NetworkId;
        long? flowNetworkObjectId = target.FlowNetworkObjectId;
        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);

        RequestSnapshotRefresher.ApplyPlan(ticket, plan);

        Assert.Multiple(() =>
        {
            Assert.That(Element(ticket, 201), Is.SameAs(target));
            Assert.That(target.IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(target.Name, Is.EqualTo("server-a"));
            Assert.That(target.Id, Is.EqualTo(id));
            Assert.That(target.TaskId, Is.EqualTo(taskId));
            Assert.That(target.Field, Is.EqualTo(field));
            Assert.That(target.RequestAction, Is.EqualTo(requestAction));
            Assert.That(target.ObjectTaskId, Is.EqualTo(objectTaskId));
            Assert.That(target.NetworkId, Is.EqualTo(networkId));
            Assert.That(target.FlowNetworkObjectId, Is.EqualTo(flowNetworkObjectId));
        });
    }

    private static WfTicket CreateNetworkTicket()
    {
        WfReqTask firstObject = ObjectTask(10, ElemFieldType.source, "server-a", "10.0.1.10/32");
        WfReqTask secondObject = ObjectTask(11, ElemFieldType.source, "server-b", "10.0.1.11/32");
        WfReqTask group = new()
        {
            Id = 20,
            TicketId = 100,
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                Reference(201, 20, 10, ElemFieldType.source, "server-a-old", "10.0.0.10/32"),
                Reference(202, 20, 11, ElemFieldType.source, "server-b", "10.0.1.11/32"),
                new WfReqElement
                {
                    Id = 203,
                    TaskId = 20,
                    Field = ElemFieldType.source.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    NetworkId = 77,
                    Name = "catalog",
                    IpString = "192.0.2.10/32"
                }
            ]
        };
        group.Elements[0].NetworkId = 501;
        group.Elements[0].FlowNetworkObjectId = 601;
        WfReqTask access = new()
        {
            Id = 30,
            TicketId = 100,
            TaskType = WfTaskType.access.ToString(),
            Elements =
            [
                Reference(301, 30, 10, ElemFieldType.destination, "server-a-old", "10.0.0.10/32"),
                new WfReqElement
                {
                    Id = 302,
                    TaskId = 30,
                    Field = ElemFieldType.source.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    ObjectTaskId = 20,
                    GroupName = "ticket-group"
                }
            ]
        };
        return Ticket(firstObject, secondObject, group, access);
    }

    private static WfReqTask ObjectTask(long id, ElemFieldType field, string name, string? ip = null)
    {
        return new WfReqTask
        {
            Id = id,
            TicketId = 100,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = id * 10 + 1,
                    TaskId = id,
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = name,
                    IpString = ip
                }
            ]
        };
    }

    private static WfReqElement Reference(long id, long taskId, long objectTaskId, ElemFieldType field,
        string name, string? ip = null, int? port = null, int? protoId = null,
        DateTime? startTime = null, DateTime? endTime = null)
    {
        return new WfReqElement
        {
            Id = id,
            TaskId = taskId,
            ObjectTaskId = objectTaskId,
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = name,
            IpString = ip,
            Port = port,
            ProtoId = protoId,
            StartTime = startTime,
            EndTime = endTime
        };
    }

    private static WfTicket Ticket(params WfReqTask[] tasks)
    {
        WfTicket ticket = new() { Id = 100, Tasks = [.. tasks] };
        ticket.UpdateCidrsInTaskElements();
        return ticket;
    }

    private static WfReqElement Element(WfTicket ticket, long elementId)
    {
        return ticket.Tasks.SelectMany(task => task.Elements).Single(element => element.Id == elementId);
    }

    public enum InvalidReference
    {
        MissingTask,
        NonObjectTask,
        InvalidStructure,
        IncompatibleField,
        NonPersistedTarget
    }
}

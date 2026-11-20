using FWO.Basics;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class EffectiveRequestStateMaterializerTest
{
    public enum InvalidReferenceScenario
    {
        Missing,
        Duplicate,
        CrossTicket,
        Later,
        WrongType,
        InvalidRequestedElement
    }

    [Test]
    public void CreatePlan_ObjectReferenceUsesLatestNetworkStateAndPreservesReference()
    {
        WfTicket ticket = CreateNetworkReferenceTicket();

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(plan.HasChanges, Is.True);
            Assert.That(plan.ChangedElements, Has.Count.EqualTo(1));
            Assert.That(plan.ChangedElements[0].Id, Is.EqualTo(201));
            Assert.That(plan.ChangedElements[0].TaskId, Is.EqualTo(20));
            Assert.That(plan.ChangedElements[0].IpString, Is.EqualTo("10.0.1.10"));
            Assert.That(plan.ChangedElements[0].ObjectTaskId, Is.EqualTo(10));
            Assert.That(plan.ChangedElements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(plan.ChangedTaskIds, Is.EqualTo(Target(20)));
            Assert.That(ticket.Tasks.Single(task => task.Id == 20).Elements[0].IpString, Is.EqualTo("10.0.0.10"));
        });
    }

    [Test]
    public void CreatePlan_ServiceReferenceUsesLatestServiceState()
    {
        WfReqTask objectTask = ObjectTask(11, 1, ElemFieldType.service, "https", null);
        objectTask.Elements[0].ProtoId = 17;
        objectTask.Elements[0].Port = 8443;
        objectTask.Elements[0].PortEnd = 8444;
        WfReqElement member = ReferencingElement(211, 21, ElemFieldType.service, 11);
        member.ProtoId = 6;
        member.Port = 443;
        member.PortEnd = 443;
        WfReqTask groupTask = GroupTask(21, 2, WfTaskType.group_create, member);
        WfTicket ticket = Ticket(objectTask, groupTask);

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(groupTask.Id));

        Assert.Multiple(() =>
        {
            Assert.That(plan.ChangedElements, Has.Count.EqualTo(1));
            Assert.That(plan.ChangedElements[0].ProtoId, Is.EqualTo(17));
            Assert.That(plan.ChangedElements[0].Port, Is.EqualTo(8443));
            Assert.That(plan.ChangedElements[0].PortEnd, Is.EqualTo(8444));
            Assert.That(plan.ChangedElements[0].ObjectTaskId, Is.EqualTo(11));
        });
    }

    [Test]
    public void CreatePlan_AccessGroupReferenceMaterializesWholeGroupMemberRows()
    {
        WfTicket ticket = CreateNetworkReferenceTicket();
        WfReqElement groupReference = ReferencingElement(301, 30, ElemFieldType.source, 20);
        WfReqTask accessTask = new()
        {
            Id = 30,
            TicketId = 100,
            TaskNumber = 3,
            TaskType = WfTaskType.access.ToString(),
            Elements = [groupReference]
        };
        ticket.Tasks.Add(accessTask);

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(accessTask.Id));

        Assert.Multiple(() =>
        {
            Assert.That(plan.ChangedElements, Has.Count.EqualTo(1));
            Assert.That(plan.ChangedElements[0].Id, Is.EqualTo(201));
            Assert.That(plan.ChangedElements[0].TaskId, Is.EqualTo(20));
            Assert.That(plan.ChangedElements[0].IpString, Is.EqualTo("10.0.1.10"));
            Assert.That(plan.ChangedTaskIds, Is.EqualTo(Target(20)));
        });
    }

    [Test]
    public void CreatePlan_GroupModifyPreservesDeltaActionsAndDoesNotLoadBaseline()
    {
        WfTicket ticket = CreateNetworkReferenceTicket();
        WfReqTask groupTask = ticket.Tasks.Single(task => task.Id == 20);
        groupTask.TaskType = WfTaskType.group_modify.ToString();
        groupTask.NetworkGroupId = 7;
        groupTask.Elements.Insert(0, new WfReqElement
        {
            Id = 200,
            TaskId = 20,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.unchanged.ToString(),
            NetworkId = 77,
            Name = "catalog-member",
            IpString = "192.0.2.10"
        });
        groupTask.Elements[1].RequestAction = RequestAction.addAfterCreation.ToString();

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(groupTask.Id));

        Assert.Multiple(() =>
        {
            Assert.That(plan.ChangedElements, Has.Count.EqualTo(1));
            Assert.That(plan.ChangedElements[0].Id, Is.EqualTo(201));
            Assert.That(plan.ChangedElements[0].RequestAction, Is.EqualTo(RequestAction.addAfterCreation.ToString()));
            Assert.That(plan.ChangedElements, Has.None.Property(nameof(WfReqElement.Id)).EqualTo(200));
        });
    }

    [Test]
    public void CreatePlan_UnrelatedTasksRemainUnchanged()
    {
        WfTicket ticket = CreateNetworkReferenceTicket();
        WfReqTask unrelatedObject = ObjectTask(40, 3, ElemFieldType.source, "other", "10.0.2.10");
        WfReqTask unrelatedGroup = GroupTask(50, 4, WfTaskType.group_create,
            ReferencingElement(501, 50, ElemFieldType.source, 40));
        ticket.Tasks.Add(unrelatedObject);
        ticket.Tasks.Add(unrelatedGroup);

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(plan.ChangedElements.Select(element => element.Id), Is.EqualTo(Target(201)));
            Assert.That(unrelatedGroup.Elements[0].IpString, Is.EqualTo("10.0.0.10"));
        });
    }

    [Test]
    public void CreatePlan_AlreadyMaterializedReferenceProducesNoChanges()
    {
        WfTicket ticket = CreateNetworkReferenceTicket();
        WfReqElement materializedMember = ticket.Tasks.Single(task => task.Id == 20).Elements[0];
        materializedMember.Name = "server";
        materializedMember.IpString = "10.0.1.10";

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(plan.HasChanges, Is.False);
            Assert.That(plan.ChangedElements, Is.Empty);
            Assert.That(plan.ChangedTaskIds, Is.Empty);
        });
    }

    [Test]
    public void CreatePlan_LegacyTicketLocalMemberWithoutReferenceFailsClosed()
    {
        WfReqElement member = new()
        {
            Id = 201,
            TaskId = 20,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = "legacy",
            IpString = "10.0.0.10"
        };
        WfTicket ticket = Ticket(GroupTask(20, 2, WfTaskType.group_create, member));

        EffectiveRequestStateMaterializationException exception = Assert.Throws<EffectiveRequestStateMaterializationException>(
            () => EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20)))!;

        Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.UnidentifiableReference));
    }

    [Test]
    public void CreatePlan_StableCatalogMemberWithoutReferenceRemainsUnchanged()
    {
        WfReqElement member = new()
        {
            Id = 201,
            TaskId = 20,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.unchanged.ToString(),
            NetworkId = 77,
            Name = "catalog",
            IpString = "10.0.0.10"
        };
        WfTicket ticket = Ticket(GroupTask(20, 2, WfTaskType.group_modify, member));

        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20));

        Assert.That(plan.HasChanges, Is.False);
    }

    [Test]
    public void CreatePlan_NestedTicketLocalGroupMemberFailsClosed()
    {
        WfReqElement stableMember = new()
        {
            Id = 151,
            TaskId = 15,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.create.ToString(),
            NetworkId = 77
        };
        WfReqTask referencedGroup = GroupTask(15, 1, WfTaskType.group_create, stableMember);
        WfReqTask targetGroup = GroupTask(20, 2, WfTaskType.group_create,
            ReferencingElement(201, 20, ElemFieldType.source, 15));
        WfTicket ticket = Ticket(referencedGroup, targetGroup);

        EffectiveRequestStateMaterializationException exception = Assert.Throws<EffectiveRequestStateMaterializationException>(
            () => EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20)))!;

        Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.NestedGroupReference));
    }

    [TestCase(InvalidReferenceScenario.Missing, EffectiveRequestStateFailureCategory.MissingReference)]
    [TestCase(InvalidReferenceScenario.Duplicate, EffectiveRequestStateFailureCategory.DuplicateTask)]
    [TestCase(InvalidReferenceScenario.CrossTicket, EffectiveRequestStateFailureCategory.CrossTicketReference)]
    [TestCase(InvalidReferenceScenario.Later, EffectiveRequestStateFailureCategory.LaterReference)]
    [TestCase(InvalidReferenceScenario.WrongType, EffectiveRequestStateFailureCategory.WrongReferenceType)]
    [TestCase(InvalidReferenceScenario.InvalidRequestedElement, EffectiveRequestStateFailureCategory.InvalidRequestedElement)]
    public void CreatePlan_InvalidExactReferenceFailsClosed(InvalidReferenceScenario scenario,
        EffectiveRequestStateFailureCategory expectedCategory)
    {
        WfTicket ticket = CreateNetworkReferenceTicket();
        ApplyInvalidReferenceScenario(ticket, scenario);

        EffectiveRequestStateMaterializationException exception = Assert.Throws<EffectiveRequestStateMaterializationException>(
            () => EffectiveRequestStateMaterializer.CreatePlan(ticket, Target(20)))!;

        Assert.That(exception.Category, Is.EqualTo(expectedCategory));
    }

    private static WfTicket CreateNetworkReferenceTicket()
    {
        WfReqTask objectTask = ObjectTask(10, 1, ElemFieldType.source, "server", "10.0.1.10");
        WfReqElement member = ReferencingElement(201, 20, ElemFieldType.source, objectTask.Id);
        WfReqTask groupTask = GroupTask(20, 2, WfTaskType.group_create, member);
        return Ticket(objectTask, groupTask);
    }

    private static WfReqTask ObjectTask(long id, int taskNumber, ElemFieldType field, string name, string? value)
    {
        WfReqElement requested = new()
        {
            Id = id * 10 + 1,
            TaskId = id,
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = name,
            IpString = value
        };
        return new WfReqTask
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [requested]
        };
    }

    private static WfReqTask GroupTask(long id, int taskNumber, WfTaskType type, params WfReqElement[] elements)
    {
        return new WfReqTask
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            TaskType = type.ToString(),
            Elements = [.. elements]
        };
    }

    private static WfReqElement ReferencingElement(long id, long taskId, ElemFieldType field, long objectTaskId)
    {
        return new WfReqElement
        {
            Id = id,
            TaskId = taskId,
            Field = field.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ObjectTaskId = objectTaskId,
            Name = "stale",
            IpString = "10.0.0.10"
        };
    }

    private static WfTicket Ticket(params WfReqTask[] tasks)
    {
        return new WfTicket { Id = 100, Tasks = [.. tasks] };
    }

    private static List<long> Target(long id)
    {
        return [id];
    }

    private static void ApplyInvalidReferenceScenario(WfTicket ticket, InvalidReferenceScenario scenario)
    {
        WfReqTask objectTask = ticket.Tasks.Single(task => task.Id == 10);
        WfReqElement member = ticket.Tasks.Single(task => task.Id == 20).Elements[0];
        switch (scenario)
        {
            case InvalidReferenceScenario.Missing:
                member.ObjectTaskId = 999;
                break;
            case InvalidReferenceScenario.Duplicate:
                ticket.Tasks.Add(new WfReqTask { Id = 10, TicketId = 100, TaskNumber = 0 });
                break;
            case InvalidReferenceScenario.CrossTicket:
                objectTask.TicketId = 200;
                break;
            case InvalidReferenceScenario.Later:
                objectTask.TaskNumber = 3;
                break;
            case InvalidReferenceScenario.WrongType:
                objectTask.TaskType = WfTaskType.access.ToString();
                break;
            case InvalidReferenceScenario.InvalidRequestedElement:
                objectTask.Elements[0].RequestAction = RequestAction.unchanged.ToString();
                break;
        }
    }
}

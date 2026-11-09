using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class UiApprovalTaskDetailsEditorServiceTest
{
    private static readonly long[] ExpectedBundledOwnerIds = [2, 2];

    [Test]
    public void ElementsEditor_CopiesObjectSnapshotWithoutChangingReferenceIdentity()
    {
        WfReqElement target = new()
        {
            Id = 12,
            TaskId = 20,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.modify.ToString(),
            ObjectTaskId = 42,
            Name = "old",
            IpString = "10.0.0.1"
        };
        WfReqElement source = new()
        {
            Id = 99,
            TaskId = 77,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ObjectTaskId = 100,
            Name = "new",
            IpString = "10.0.0.2",
            IpEnd = "10.0.0.9",
            ProtoId = 6
        };

        ApprovalTaskDetailsElementsEditor.CopyObjectSnapshot(target, source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Name, Is.EqualTo("new"));
            Assert.That(target.IpString, Is.EqualTo("10.0.0.2"));
            Assert.That(target.IpEnd, Is.EqualTo("10.0.0.9"));
            Assert.That(target.ProtoId, Is.EqualTo(6));
            Assert.That(target.Id, Is.EqualTo(12));
            Assert.That(target.TaskId, Is.EqualTo(20));
            Assert.That(target.RequestAction, Is.EqualTo(RequestAction.modify.ToString()));
            Assert.That(target.ObjectTaskId, Is.EqualTo(42));
        });
    }

    [Test]
    public void ElementsEditor_FindsNetworkAndServiceGroupMembersByReference()
    {
        WfReqElement networkAccess = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkObjectId = 10
        };
        WfReqElement networkMember = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkObjectId = 10,
            Name = "server"
        };
        WfReqElement serviceAccess = new()
        {
            Field = ElemFieldType.service.ToString(),
            FlowServiceGroupId = 20
        };
        WfReqElement serviceMember = new()
        {
            Field = ElemFieldType.service.ToString(),
            FlowServiceGroupId = 20,
            Name = "https"
        };
        List<WfReqElement> networkMembers = [networkMember];
        List<WfReqElement> serviceMembers = [serviceMember];
        List<WfReqElement> noMembers = [];

        Assert.Multiple(() =>
        {
            Assert.That(ApprovalTaskDetailsElementsEditor.FindGroupMemberByReference(networkAccess, networkMembers),
                Is.SameAs(networkMember));
            Assert.That(ApprovalTaskDetailsElementsEditor.FindGroupMemberByReference(serviceAccess, serviceMembers),
                Is.SameAs(serviceMember));
            Assert.That(ApprovalTaskDetailsElementsEditor.FindGroupMemberByReference(networkAccess, noMembers), Is.Null);
            Assert.That(ApprovalTaskDetailsElementsEditor.SamePositiveId(10, 10), Is.True);
            Assert.That(ApprovalTaskDetailsElementsEditor.SamePositiveId(0, 0), Is.False);
        });
    }

    [Test]
    public void ElementsEditor_PreservesRemovedGroupMemberAsDeleteRequest()
    {
        WfReqElement member = new()
        {
            Id = 17,
            TaskId = 21,
            Field = ElemFieldType.source.ToString(),
            FlowNetworkObjectId = 42,
            RequestAction = RequestAction.create.ToString(),
            Name = "server"
        };

        WfReqElement deleted = ApprovalTaskDetailsElementsEditor.CreateDeletedGroupMember(member);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.Not.SameAs(member));
            Assert.That(deleted.Id, Is.EqualTo(member.Id));
            Assert.That(deleted.FlowNetworkObjectId, Is.EqualTo(member.FlowNetworkObjectId));
            Assert.That(deleted.RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
        });
    }

    [Test]
    public async Task ElementsEditor_OpensRelevantEditorsAndRestoresParentEditor()
    {
        WfReqTask accessTask = new()
        {
            Id = 20,
            StateId = 4,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1" }]
        };
        WfTicket ticket = new() { Id = 5, Tasks = [accessTask] };
        WfHandler handler = new() { ActTicket = ticket };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        int refreshCount = 0;
        context.Configure(handler, new SimulatedUserConfig(), () =>
        {
            refreshCount++;
            return Task.CompletedTask;
        });
        ApprovalTaskDetailsElementsEditor editor = new(context, state, () => context.RefreshUi());

        await editor.OpenGroupMembersEditor(accessTask);
        Assert.That(state.EditingElementsField, Is.EqualTo(WorkflowEditableFieldKeys.Source));
        Assert.That(state.DisplayElementsEditor, Is.True);

        await editor.OpenObjectTaskEditor();
        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayElementsEditor, Is.False);
            Assert.That(state.DisplayObjectTaskEditor, Is.True);
            Assert.That(state.NewObjectTaskKind, Is.EqualTo(ObjectTaskKind.Network));
            Assert.That(state.NewObjectTask.TicketId, Is.EqualTo(5));
            Assert.That(state.NewObjectTask.StateId, Is.EqualTo(4));
            Assert.That(state.NewObjectTask.ManagementId, Is.EqualTo(accessTask.ManagementId));
        });

        await editor.HandleObjectTaskEditorDisplayChanged(false);
        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayElementsEditor, Is.True);
            Assert.That(state.DisplayObjectTaskEditor, Is.False);
            Assert.That(refreshCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ElementsEditor_OpensExistingServiceTaskAndHandlesMissingParentTask()
    {
        WfReqTask serviceTask = new()
        {
            Id = 21,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.service.ToString(),
                    Name = "https",
                    ServiceId = 9
                }
            ]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 6, Tasks = [serviceTask] } };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsElementsEditor editor = new(context, state, () => Task.CompletedTask);

        await editor.OpenExistingObjectTaskEditor(serviceTask);
        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayObjectTaskEditor, Is.True);
            Assert.That(state.ObjectTaskIsExisting, Is.True);
            Assert.That(state.ObjectTaskIsModify, Is.True);
            Assert.That(state.NewObjectTaskKind, Is.EqualTo(ObjectTaskKind.Service));
        });

        state.EditingElementsTask = null;
        state.DisplayElementsEditor = false;
        await editor.OpenObjectTaskEditor();
        Assert.That(state.DisplayElementsEditor, Is.False);
    }

    [Test]
    public void ElementsEditor_DetectsObjectTaskReferencesInTicket()
    {
        WfReqTask objectTask = new() { Id = 30, TaskType = WfTaskType.object_create.ToString() };
        WfReqTask accessTask = new()
        {
            Id = 31,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), ObjectTaskId = 30 }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [objectTask, accessTask] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 1 });
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsElementsEditor editor = new(context, state, () => Task.CompletedTask);

        Assert.That(editor.IsObjectTaskReferenced(objectTask), Is.True);
        accessTask.Elements.Clear();
        Assert.That(editor.IsObjectTaskReferenced(objectTask), Is.False);
    }

    [Test]
    public async Task TimeObjectEditor_OpensExistingObjectAndCreateEditors()
    {
        WfReqTask accessTask = new()
        {
            Id = 40,
            StateId = 3,
            TaskType = WfTaskType.access.ToString(),
            TargetBeginDate = new DateTime(2026, 11, 1, 8, 0, 0),
            TargetEndDate = new DateTime(2026, 11, 1, 17, 0, 0),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                FlowTimeObjectId = 77,
                Name = "maintenance"
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 6, Tasks = [accessTask] } };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new()
        {
            DisplayElementsEditor = true,
            EditingElementsTask = new WfReqTask { TaskType = WfTaskType.group_modify.ToString() }
        };
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);

        await editor.OpenTargetDatesEditor(accessTask, WorkflowEditableFieldKeys.ValidTo);
        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayTargetDatesEditor, Is.True);
            Assert.That(state.DisplayElementsEditor, Is.False);
            Assert.That(state.EditingElementsTask, Is.Null);
            Assert.That(state.TargetDatesEditorModel.FlowTimeObjectId, Is.EqualTo(77));
            Assert.That(state.TargetDatesEditorModel.FlowTimeObjectName, Is.EqualTo("maintenance"));
            Assert.That(state.TargetDatesEditorModel.BeginDate, Is.EqualTo(accessTask.TargetBeginDate));
        });

        Assert.That(editor.CanCreateTimeObjectTask, Is.True);
        await editor.OpenCreateTimeObjectTaskEditor();
        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayTimeObjectTaskEditor, Is.True);
            Assert.That(state.NewTimeObjectTaskModel.BeginDate, Is.EqualTo(accessTask.TargetBeginDate));
            Assert.That(state.NewTimeObjectTaskModel.EndDate, Is.EqualTo(accessTask.TargetEndDate));
        });
    }

    [Test]
    public void TimeObjectEditor_AppliesFlowAndTicketReferencesAndCanRemoveTimeObject()
    {
        WfReqTask accessTask = new()
        {
            Id = 50,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Id = 501, Field = ElemFieldType.time.ToString(), FlowTimeObjectId = 11 }]
        };
        WfReqTask ticketTimeTask = new()
        {
            Id = 60,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Id = 601, Field = ElemFieldType.time.ToString(), Name = "ticket-time" }]
        };
        WfHandler handler = new()
        {
            ActReqTask = accessTask,
            ActTicket = new WfTicket { Tasks = [accessTask, ticketTimeTask] }
        };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);
        DateTime begin = new(2026, 12, 1, 8, 0, 0);
        DateTime end = new(2026, 12, 1, 18, 0, 0);

        editor.ApplyTimeObjectModel(new TargetDatesEditorModel
        {
            FlowTimeObjectId = -601,
            TicketElementId = 601,
            FlowTimeObjectName = "ticket-time",
            BeginDate = begin,
            EndDate = end
        });
        Assert.Multiple(() =>
        {
            Assert.That(accessTask.TargetBeginDate, Is.EqualTo(begin));
            Assert.That(TimeObjectElementHelper.Find(accessTask.Elements)?.ObjectTaskId, Is.EqualTo(60));
            Assert.That(TimeObjectElementHelper.Find(accessTask.Elements)?.Name, Is.EqualTo("ticket-time"));
        });

        editor.ApplyTimeObjectModel(new TargetDatesEditorModel
        {
            FlowTimeObjectId = 22,
            FlowTimeObjectName = "flow-time",
            BeginDate = begin,
            EndDate = end
        });
        Assert.That(TimeObjectElementHelper.Find(accessTask.Elements)?.FlowTimeObjectId, Is.EqualTo(22));

        WfReqElement existing = TimeObjectElementHelper.Find(accessTask.Elements)!;
        editor.ApplyTimeObjectModel(new TargetDatesEditorModel { BeginDate = begin, EndDate = end });
        Assert.Multiple(() =>
        {
            Assert.That(TimeObjectElementHelper.Find(accessTask.Elements), Is.Null);
            Assert.That(accessTask.RemovedElements, Does.Contain(existing));
        });
    }

    [Test]
    public async Task TimeObjectEditor_ResolvesTicketLocalTimeObjectWhenOpeningEditor()
    {
        WfReqTask timeTask = new()
        {
            Id = 60,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = 601,
                    Field = ElemFieldType.time.ToString(),
                    Name = "ticket-time",
                    StartTime = new DateTime(2026, 12, 1, 8, 0, 0),
                    EndTime = new DateTime(2026, 12, 1, 18, 0, 0)
                }
            ]
        };
        WfReqTask accessTask = new()
        {
            Id = 61,
            TaskType = WfTaskType.access.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.time.ToString(),
                    ObjectTaskId = 60
                }
            ]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [timeTask, accessTask] } };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);

        await editor.OpenTargetDatesEditor(accessTask, WorkflowEditableFieldKeys.ValidFrom);

        Assert.Multiple(() =>
        {
            Assert.That(state.TargetDatesEditorModel.FlowTimeObjectId, Is.EqualTo(-601));
            Assert.That(state.TargetDatesEditorModel.TicketElementId, Is.EqualTo(601));
            Assert.That(state.TargetDatesEditorModel.FlowTimeObjectName, Is.EqualTo("ticket-time"));
            Assert.That(state.FormerTimeObjectTask, Is.SameAs(timeTask));
        });
    }

    [Test]
    public void TimeObjectEditor_HandlesBundledTasksAndFormerObjectDeletionEligibility()
    {
        SimulatedUserConfig userConfig = new()
        {
            ReqDisplayBundledTasksAsOne = true,
            ReqConsiderBundling = true
        };
        WfReqTask formerObjectTask = new() { Id = 70, TaskType = WfTaskType.object_create.ToString() };
        WfReqTask selectedTask = new() { Id = 71, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfReqTask bundledTask = new() { Id = 72, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfReqTask differentStateTask = new() { Id = 73, StateId = 3, TaskType = WfTaskType.access.ToString() };
        selectedTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1");
        bundledTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1");
        differentStateTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1");
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Tasks = [formerObjectTask, selectedTask, bundledTask, differentStateTask] }
        };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new()
        {
            EditingTargetDatesTask = selectedTask,
            FormerTimeObjectTask = formerObjectTask,
            FormerTimeObjectSelectorId = 10
        };
        context.Configure(handler, userConfig, () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);

        List<long> expectedTaskIds = [71, 72];
        Assert.That(editor.GetTasksToUpdate(selectedTask).Select(task => task.Id), Is.EquivalentTo(expectedTaskIds));
        Assert.That(editor.ShouldOfferFormerTimeObjectDeletion(new TargetDatesEditorModel { FlowTimeObjectId = 11 }),
            Is.True);
        selectedTask.Elements.Add(new WfReqElement
        {
            Field = ElemFieldType.time.ToString(),
            ObjectTaskId = formerObjectTask.Id
        });
        Assert.That(editor.UsesFormerTimeObject(selectedTask), Is.True);
        WfReqTask otherAccessTask = new()
        {
            Id = 74,
            StateId = 2,
            TaskType = WfTaskType.access.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.time.ToString(),
                    ObjectTaskId = formerObjectTask.Id
                }
            ]
        };
        handler.ActTicket.Tasks.Add(otherAccessTask);
        Assert.That(editor.ShouldOfferFormerTimeObjectDeletion(new TargetDatesEditorModel { FlowTimeObjectId = 11 }),
            Is.False);
    }

    [Test]
    public void TimeObjectEditor_InitializesNewTaskStateAndChoosesEditableField()
    {
        WfReqTask task = new()
        {
            StateId = 1,
            Approvals = [new WfApproval { StateId = 1 }]
        };
        WfHandler handler = new() { ActStateMatrix = new StateMatrix { LowestInputState = 9 } };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (tasks, field) =>
            field == WorkflowEditableFieldKeys.ValidTo, () => Task.CompletedTask);

        editor.InitializeNewObjectTaskState(task);

        Assert.Multiple(() =>
        {
            Assert.That(task.StateId, Is.EqualTo(9));
            Assert.That(task.Approvals[0].StateId, Is.EqualTo(9));
            Assert.That(editor.CanEditTimeObject([task]), Is.True);
            Assert.That(editor.EditableTimeObjectField([task]), Is.EqualTo(WorkflowEditableFieldKeys.ValidTo));
        });
    }

    [Test]
    public void TimeObjectEditor_ReturnsFalseWhenNoTaskCanBeCreatedOrFormerTaskIsStillSelected()
    {
        WfHandler handler = new() { ActTicket = new WfTicket() };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => false,
            () => Task.CompletedTask);
        List<WfReqTask> noTasks = [];

        Assert.Multiple(() =>
        {
            Assert.That(editor.CanCreateTimeObjectTask, Is.False);
            Assert.That(editor.CanEditTimeObject(noTasks), Is.False);
            Assert.That(editor.ShouldOfferFormerTimeObjectDeletion(new TargetDatesEditorModel()), Is.False);
            Assert.That(editor.UsesFormerTimeObject(new WfReqTask()), Is.False);
        });
    }

    [Test]
    public async Task ElementsEditor_RefreshesAccessSnapshotsAfterObjectChange()
    {
        WfReqTask objectTask = new()
        {
            Id = 80,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = "new-server",
                IpString = "192.0.2.80",
                RequestAction = RequestAction.modify.ToString()
            }]
        };
        WfReqTask accessTask = new()
        {
            Id = 81,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                ObjectTaskId = objectTask.Id,
                Name = "old-server",
                IpString = "192.0.2.8"
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [objectTask, accessTask] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 1 });
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsElementsEditor editor = new(context, state, () => Task.CompletedTask);

        await editor.RefreshAccessTasksUsingObject(objectTask);

        Assert.Multiple(() =>
        {
            Assert.That(accessTask.Elements[0].Name, Is.EqualTo("new-server"));
            Assert.That(accessTask.Elements[0].IpString, Is.EqualTo("192.0.2.80"));
            Assert.That(accessTask.Elements[0].ObjectTaskId, Is.EqualTo(objectTask.Id));
        });
    }

    [Test]
    public async Task ElementsEditor_RefreshesAccessSnapshotsAfterGroupChange()
    {
        WfReqTask groupTask = new()
        {
            Id = 90,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                GroupName = "web-group",
                Name = "new-member",
                IpString = "192.0.2.90",
                FlowNetworkObjectId = 900
            }]
        };
        WfReqTask accessTask = new()
        {
            Id = 91,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                ObjectTaskId = groupTask.Id,
                Name = "old-member",
                IpString = "192.0.2.9",
                FlowNetworkObjectId = 900
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupTask, accessTask] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 1 });
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsElementsEditor editor = new(context, state, () => Task.CompletedTask);

        await editor.RefreshAccessTasksUsingGroup(groupTask);

        Assert.That(accessTask.Elements[0].Name, Is.EqualTo("new-member"));
        Assert.That(accessTask.Elements[0].IpString, Is.EqualTo("192.0.2.90"));
    }

    [Test]
    public async Task ElementsEditor_DeletesPendingObjectTaskAndClearsEditorState()
    {
        WfReqTask objectTask = new() { Id = 100, TaskType = WfTaskType.object_create.ToString() };
        WfTicket ticket = new() { Id = 10, Tasks = [objectTask] };
        WfHandler handler = new() { ActTicket = ticket };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.object_create.ToString(), new StateMatrix { LowestInputState = 1 });
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new()
        {
            PendingObjectTask = objectTask,
            DisplayDeleteObjectTask = true
        };
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsElementsEditor editor = new(context, state, () => Task.CompletedTask);

        await editor.DeleteObjectTaskCore();

        Assert.Multiple(() =>
        {
            Assert.That(ticket.Tasks, Is.Empty);
            Assert.That(state.PendingObjectTask, Is.Null);
            Assert.That(state.DisplayDeleteObjectTask, Is.False);
        });
    }

    [Test]
    public async Task TimeObjectEditor_ReturnsWithoutChangingStateWhenNoTargetTaskExists()
    {
        WfHandler handler = new() { ActTicket = new WfTicket() };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => false,
            () => Task.CompletedTask);

        await editor.OpenCreateTimeObjectTaskEditor();
        await editor.SaveTargetDatesCore(new TargetDatesEditorModel { FlowTimeObjectId = 5 });
        await editor.CreateTimeObjectTaskCore(new TargetDatesEditorModel { FlowTimeObjectId = 5 });

        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayTimeObjectTaskEditor, Is.False);
            Assert.That(handler.ActTicket.Tasks, Is.Empty);
        });
    }

    [Test]
    public void TimeObjectEditor_UsesSingleTaskWhenBundlingIsDisabled()
    {
        WfReqTask task = new() { Id = 110, TaskType = WfTaskType.access.ToString() };
        WfReqTask bundled = new() { Id = 111, TaskType = WfTaskType.access.ToString() };
        task.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle");
        bundled.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle");
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task, bundled] } };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new();
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);

        Assert.That(editor.GetTasksToUpdate(task), Is.EqualTo(new List<WfReqTask> { task }));
    }

    [Test]
    public async Task TimeObjectEditor_SavesTargetDatesForAllTasksInTheBundle()
    {
        WfReqTask first = new()
        {
            Id = 130,
            TaskNumber = 1,
            StateId = 2,
            TaskType = WfTaskType.access.ToString()
        };
        WfReqTask second = new()
        {
            Id = 131,
            TaskNumber = 2,
            StateId = 2,
            TaskType = WfTaskType.access.ToString()
        };
        first.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "time-bundle");
        second.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "time-bundle");
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 13, Tasks = [first, second] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 1 });
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new() { EditingTargetDatesTask = first };
        SimulatedUserConfig config = new() { ReqConsiderBundling = true, ReqDisplayBundledTasksAsOne = true };
        context.Configure(handler, config, () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);
        DateTime begin = new(2026, 12, 1, 8, 0, 0);
        DateTime end = new(2026, 12, 31, 18, 0, 0);

        await editor.SaveTargetDatesCore(new TargetDatesEditorModel
        {
            BeginDate = begin,
            EndDate = end
        });

        WfReqTask savedFirst = handler.ActTicket.Tasks.Single(task => task.Id == first.Id);
        WfReqTask savedSecond = handler.ActTicket.Tasks.Single(task => task.Id == second.Id);
        Assert.Multiple(() =>
        {
            Assert.That(savedFirst.TargetBeginDate, Is.EqualTo(begin));
            Assert.That(savedFirst.TargetEndDate, Is.EqualTo(end));
            Assert.That(savedSecond.TargetBeginDate, Is.EqualTo(begin));
            Assert.That(savedSecond.TargetEndDate, Is.EqualTo(end));
            Assert.That(state.EditingTargetDatesTask, Is.Null);
            Assert.That(state.DisplayDeleteFormerTimeObjectTask, Is.False);
        });
    }

    [Test]
    public async Task MetadataEditor_SavesOwnerChangeForAllTasksInTheBundle()
    {
        FwoOwner oldOwner = new() { Id = 1, Name = "old" };
        FwoOwner newOwner = new() { Id = 2, Name = "new" };
        WfReqTask first = BundledApprovalTask(150, 1, oldOwner);
        WfReqTask second = BundledApprovalTask(151, 2, oldOwner);
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 15, Tasks = [first, second] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 1 });
        SimulatedUserConfig config = new() { ReqConsiderBundling = true, ReqDisplayBundledTasksAsOne = true };
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new() { EditingOwnerTask = first };
        context.Configure(handler, config, () => Task.CompletedTask);
        ApprovalTaskDetailsMetadataEditor editor = new(
            context,
            state,
            () => Task.CompletedTask,
            task => ApprovalTaskBundleHelper.GetTasksToUpdate(handler.ActTicket.Tasks, task, config));

        await editor.SaveOwnerCore(newOwner);

        Assert.Multiple(() =>
        {
            Assert.That(handler.ActTicket.Tasks.SelectMany(task => task.NewOwners).Select(owner => owner.Id),
                Is.EquivalentTo(ExpectedBundledOwnerIds));
            Assert.That(handler.ActTicket.Tasks.All(task => task.RemovedOwners.Count == 1
                && task.RemovedOwners[0].Id == oldOwner.Id), Is.True);
            Assert.That(state.EditingOwnerTask, Is.Null);
        });
    }

    private static WfReqTask BundledApprovalTask(long id, int taskNumber, FwoOwner owner)
    {
        WfReqTask task = new()
        {
            Id = id,
            TicketId = 15,
            TaskNumber = taskNumber,
            StateId = 2,
            TaskType = WfTaskType.access.ToString(),
            Owners = [new FwoOwnerDataHelper { Owner = owner }]
        };
        task.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "metadata-bundle");
        return task;
    }

    [Test]
    public async Task TimeObjectEditor_CreatesTicketTimeObjectTaskAndReturnsToDateEditor()
    {
        WfReqTask accessTask = new()
        {
            Id = 140,
            TaskNumber = 1,
            StateId = 2,
            TaskType = WfTaskType.access.ToString(),
            ManagementId = 7,
            Approvals = [new WfApproval { StateId = 2, ApproverGroup = "approvers" }]
        };
        WfTicket ticket = new() { Id = 14, Tasks = [accessTask], TenantId = 3 };
        WfHandler handler = new()
        {
            ActTicket = ticket,
            ActStateMatrix = new StateMatrix { LowestInputState = 2 }
        };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 2 });
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.object_create.ToString(), new StateMatrix { LowestInputState = 2 });
        ApprovalTaskDetailsEditorContext context = new();
        ApprovalTaskDetailsEditorState state = new() { EditingTargetDatesTask = accessTask };
        context.Configure(handler, new SimulatedUserConfig(), () => Task.CompletedTask);
        ApprovalTaskDetailsLookupService lookup = new(context, state);
        ApprovalTaskDetailsTimeObjectEditor editor = new(context, state, lookup, (_, _) => true,
            () => Task.CompletedTask);
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectName = "maintenance",
            BeginDate = new DateTime(2026, 11, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 11, 30, 18, 0, 0)
        };

        await editor.CreateTimeObjectTaskCore(model);

        WfReqTask createdTask = ticket.Tasks.Single(task => task.TaskType == WfTaskType.object_create.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(createdTask.Title, Does.Contain("maintenance"));
            Assert.That(createdTask.StateId, Is.EqualTo(2));
            Assert.That(createdTask.Elements.Single().Name, Does.Contain("maintenance"));
            Assert.That(state.DisplayTimeObjectTaskEditor, Is.False);
            Assert.That(state.DisplayTargetDatesEditor, Is.True);
            Assert.That(state.TargetDatesEditorModel.FlowTimeObjectName, Does.Contain("maintenance"));
            Assert.That(handler.DisplayReqTaskMode, Is.False);
            Assert.That(handler.EditReqTaskMode, Is.False);
            Assert.That(handler.AddReqTaskMode, Is.False);
        });
    }

    [Test]
    public async Task EditorService_TracksProgressAndRefreshesAroundAnOperation()
    {
        ApprovalTaskDetailsEditorService service = new();
        int refreshCount = 0;
        service.Configure(new WfHandler(), new SimulatedUserConfig(), () =>
        {
            refreshCount++;
            return Task.CompletedTask;
        });

        await service.RunWithProgress(() =>
        {
            Assert.That(service.workInProgress, Is.True);
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(service.workInProgress, Is.False);
            Assert.That(refreshCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task EditorService_SkipsConcurrentProgressOperationAndCyclesDisplayModes()
    {
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(new WfHandler(), new SimulatedUserConfig(), () => Task.CompletedTask);
        bool executed = false;
        service.workInProgress = true;

        await service.RunWithProgress(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.That(executed, Is.False);
        service.workInProgress = false;
        Assert.Multiple(() =>
        {
            Assert.That(service.accessElementDisplayMode, Is.EqualTo(ApprovalTaskElementDisplayMode.Resolved));
            Assert.That(service.NextDisplayModeTextKey(), Is.EqualTo("technical_view"));
        });
        service.ToggleAccessElementDisplay();
        Assert.That(service.NextDisplayModeTextKey(), Is.EqualTo("unresolved_groups_view"));
        service.ToggleAccessElementDisplay();
        Assert.That(service.NextDisplayModeTextKey(), Is.EqualTo("resolved_view"));
        service.ToggleAccessElementDisplay();
        Assert.That(service.accessElementDisplayMode, Is.EqualTo(ApprovalTaskElementDisplayMode.Resolved));
    }

    [Test]
    public void EditorService_ExposesConfiguredStateAndTaskHelpers()
    {
        WfReqTask accessTask = new() { Id = 120, TaskType = WfTaskType.access.ToString() };
        WfReqTask objectTask = new() { Id = 121, TaskType = WfTaskType.object_create.ToString() };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [accessTask, objectTask] } };
        SimulatedUserConfig config = new();
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(handler, config, () => Task.CompletedTask);

        service.networkZones = [new NetworkZone { Id = 1, Name = "zone" }];
        service.ipProtocols = [new IpProtocol { Id = 6, Name = "tcp" }];
        service.selectableIpProtocols = service.ipProtocols;
        service.ruleActions = [new RuleAction { Id = 2, Name = "allow" }];
        service.trackings = [new Tracking { Id = 3, Name = "log" }];
        service.accessElementDisplayMode = ApprovalTaskElementDisplayMode.Technical;
        service.displayOwnerEditor = true;
        service.editingOwnerTask = accessTask;
        service.selectedOwner = new FwoOwner { Id = 4, Name = "owner" };
        service.displayRuleActionEditor = true;
        service.editingRuleActionTask = accessTask;
        service.selectedRuleAction = service.ruleActions[0];
        service.displayTrackingEditor = true;
        service.editingTrackingTask = accessTask;
        service.selectedTracking = service.trackings[0];
        service.displayManagementEditor = true;
        service.editingManagementTask = accessTask;
        service.selectedManagement = new Management { Id = 5, Name = "management" };
        service.displayTargetDatesEditor = true;
        service.editingTargetDatesTask = accessTask;
        service.formerTimeObjectTask = objectTask;
        service.formerTimeObjectSelectorId = -10;
        service.formerTimeObjectElementId = 10;
        service.targetDatesEditorModel = new TargetDatesEditorModel { FlowTimeObjectId = 10 };
        service.displayTimeObjectTaskEditor = true;
        service.newTimeObjectTaskModel = new TargetDatesEditorModel { FlowTimeObjectId = 11 };
        service.displayDeleteFormerTimeObjectTask = true;
        service.pendingFormerTimeObjectTask = objectTask;
        service.displayTextEditor = true;
        service.displayElementsEditor = true;
        service.editingElementsTask = accessTask;
        service.editingElementsField = WorkflowEditableFieldKeys.Source;
        service.displayObjectTaskEditor = true;
        service.objectTaskIsExisting = true;
        service.objectTaskIsModify = true;
        service.newObjectTask = objectTask;
        service.newObjectTaskKind = ObjectTaskKind.Network;
        service.displayDeleteObjectTask = true;
        service.pendingObjectTask = objectTask;
        service.workInProgress = true;
        service.editingTextTask = accessTask;
        service.textEditorKey = WorkflowEditableFieldKeys.Title;
        service.textEditorValue = "text";
        service.textEditorMultiline = true;

        ApprovalTaskViewContext viewContext = service.CreateTaskViewContext();

        Assert.Multiple(() =>
        {
            Assert.That(service.WfHandler, Is.SameAs(handler));
            Assert.That(service.userConfig, Is.SameAs(config));
            Assert.That(service.networkZones, Has.Count.EqualTo(1));
            Assert.That(service.ipProtocols[0].Name, Is.EqualTo("tcp"));
            Assert.That(service.ruleActions[0].Name, Is.EqualTo("allow"));
            Assert.That(service.trackings[0].Name, Is.EqualTo("log"));
            Assert.That(service.editingElementsTask, Is.SameAs(accessTask));
            Assert.That(service.textEditorValue, Is.EqualTo("text"));
            Assert.That(service.accessElementDisplayMode, Is.EqualTo(ApprovalTaskElementDisplayMode.Technical));
            Assert.That(service.displayOwnerEditor, Is.True);
            Assert.That(service.selectedOwner.Name, Is.EqualTo("owner"));
            Assert.That(service.displayRuleActionEditor, Is.True);
            Assert.That(service.selectedRuleAction, Is.SameAs(service.ruleActions[0]));
            Assert.That(service.displayTrackingEditor, Is.True);
            Assert.That(service.selectedTracking, Is.SameAs(service.trackings[0]));
            Assert.That(service.displayManagementEditor, Is.True);
            Assert.That(service.selectedManagement?.Id, Is.EqualTo(5));
            Assert.That(service.displayTargetDatesEditor, Is.True);
            Assert.That(service.formerTimeObjectTask, Is.SameAs(objectTask));
            Assert.That(service.formerTimeObjectSelectorId, Is.EqualTo(-10));
            Assert.That(service.formerTimeObjectElementId, Is.EqualTo(10));
            Assert.That(service.targetDatesEditorModel.FlowTimeObjectId, Is.EqualTo(10));
            Assert.That(service.displayTimeObjectTaskEditor, Is.True);
            Assert.That(service.newTimeObjectTaskModel.FlowTimeObjectId, Is.EqualTo(11));
            Assert.That(service.displayDeleteFormerTimeObjectTask, Is.True);
            Assert.That(service.pendingFormerTimeObjectTask, Is.SameAs(objectTask));
            Assert.That(service.displayTextEditor, Is.True);
            Assert.That(service.displayElementsEditor, Is.True);
            Assert.That(service.displayObjectTaskEditor, Is.True);
            Assert.That(service.objectTaskIsExisting, Is.True);
            Assert.That(service.objectTaskIsModify, Is.True);
            Assert.That(service.newObjectTask, Is.SameAs(objectTask));
            Assert.That(service.newObjectTaskKind, Is.EqualTo(ObjectTaskKind.Network));
            Assert.That(service.displayDeleteObjectTask, Is.True);
            Assert.That(service.pendingObjectTask, Is.SameAs(objectTask));
            Assert.That(service.workInProgress, Is.True);
            Assert.That(service.editingTextTask, Is.SameAs(accessTask));
            Assert.That(service.textEditorKey, Is.EqualTo(WorkflowEditableFieldKeys.Title));
            Assert.That(service.textEditorMultiline, Is.True);
            Assert.That(viewContext.DisplayLogic, Is.Not.Null);
            Assert.That(viewContext.EditOwner, Is.Not.Null);
            Assert.That(ApprovalTaskDetailsEditorService.IsAccessTask(accessTask), Is.True);
            Assert.That(ApprovalTaskDetailsEditorService.IsGroupTask(objectTask), Is.False);
            Assert.That(ApprovalTaskDetailsEditorService.DetailsOpen(accessTask), Is.EqualTo("open"));
            Assert.That(ApprovalTaskDetailsEditorService.DetailsOpen(objectTask), Is.Null);
            Assert.That(ApprovalTaskDetailsEditorService.TaskIds([accessTask, objectTask]), Is.EqualTo("120, 121"));
        });
    }
}

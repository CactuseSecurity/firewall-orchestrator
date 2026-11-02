using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiSharedEditorTest
{
    [Test]
    public async Task OwnerEditor_SavesSelectedOwner()
    {
        await using BunitContext context = CreateContext();
        FwoOwner? saved = null;
        FwoOwner owner = new() { Id = 7, Name = "Owner" };

        IRenderedComponent<OwnerEditor> cut = context.Render<OwnerEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedOwner, owner)
            .Add(p => p.Owners, [owner])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(7));
    }

    [Test]
    public async Task OwnerEditor_OwnerChangedUpdatesDraft()
    {
        OwnerEditor component = new();
        FwoOwner owner = new() { Id = 8, Name = "Changed" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OwnerChanged", owner);

        Assert.That(UiRequestWorkflowTest.GetMember<FwoOwner>(component, "selectedOwner"), Is.SameAs(owner));
    }

    [Test]
    public async Task RuleActionEditor_SavesSelectedAction()
    {
        await using BunitContext context = CreateContext();
        RuleAction? saved = null;
        RuleAction action = new() { Id = 3, Name = "allow" };

        IRenderedComponent<RuleActionEditor> cut = context.Render<RuleActionEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedRuleAction, action)
            .Add(p => p.RuleActions, [action])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(3));
    }

    [Test]
    public async Task RuleActionEditor_RuleActionChangedUpdatesDraft()
    {
        RuleActionEditor component = new();
        RuleAction action = new() { Id = 9, Name = "deny" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "RuleActionChanged", action);

        Assert.That(UiRequestWorkflowTest.GetMember<RuleAction?>(component, "selectedRuleAction"), Is.SameAs(action));
    }

    [Test]
    public async Task TrackingEditor_SavesSelectedTracking()
    {
        await using BunitContext context = CreateContext();
        Tracking? saved = null;
        Tracking tracking = new() { Id = 4, Name = "log" };

        IRenderedComponent<TrackingEditor> cut = context.Render<TrackingEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedTracking, tracking)
            .Add(p => p.Trackings, [tracking])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(4));
    }

    [Test]
    public async Task TrackingEditor_TrackingChangedUpdatesDraft()
    {
        TrackingEditor component = new();
        Tracking tracking = new() { Id = 10, Name = "changed-tracking" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "TrackingChanged", tracking);

        Assert.That(UiRequestWorkflowTest.GetMember<Tracking?>(component, "selectedTracking"), Is.SameAs(tracking));
    }

    [Test]
    public async Task ManagementEditor_SavesSelectedManagement()
    {
        await using BunitContext context = CreateContext();
        Management? saved = null;
        Management management = new() { Id = 5, Name = "management" };

        IRenderedComponent<ManagementEditor> cut = context.Render<ManagementEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedManagement, management)
            .Add(p => p.Managements, [management])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(5));
    }

    [Test]
    public async Task ManagementEditor_ManagementChangedUpdatesDraft()
    {
        ManagementEditor component = new();
        Management management = new() { Id = 11, Name = "changed-management" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "ManagementChanged", management);

        Assert.That(UiRequestWorkflowTest.GetMember<Management?>(component, "selectedManagement"), Is.SameAs(management));
    }

    [Test]
    public async Task TextEditor_SavesChangedMultilineText()
    {
        await using BunitContext context = CreateContext();
        string? saved = null;

        IRenderedComponent<TextEditor> cut = context.Render<TextEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Text, "before")
            .Add(p => p.TextKey, "reason")
            .Add(p => p.Multiline, true)
            .Add(p => p.Saved, value => saved = value));

        cut.Find("textarea").Change("after");
        cut.Find("button.btn-primary").Click();

        Assert.That(saved, Is.EqualTo("after"));
    }

    [Test]
    public async Task TimeObjectOriginSuffixes_SavesConfiguredValues()
    {
        await using BunitContext context = CreateContext();
        string? saved = null;

        IRenderedComponent<TimeObjectOriginSuffixes> cut = context.Render<TimeObjectOriginSuffixes>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.ConfigValue, "{\"requester\":\"_old\",\"approver\":\"\"}")
            .Add(p => p.ConfigValueChanged, value => saved = value));

        cut.FindAll("input")[0].Change("_user");
        cut.FindAll("input")[1].Change("_approver");
        cut.Find("button.btn-primary").Click();

        Assert.That(TimeObjectNamingHelper.ParseSuffixes(saved), Is.EqualTo(new Dictionary<string, string>
        {
            ["requester"] = "_user",
            ["approver"] = "_approver"
        }));
    }

    [Test]
    public async Task TimeObjectTaskEditor_SavesNamedDraft()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel? saved = null;
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectName = "maintenance_approval",
            BeginDate = new DateTime(2026, 11, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 11, 30, 18, 0, 0)
        };

        IRenderedComponent<TimeObjectTaskEditor> cut = context.Render<TimeObjectTaskEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model)
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.FlowTimeObjectName, Is.EqualTo("maintenance_approval"));
        Assert.That(saved?.BeginDate, Is.EqualTo(model.BeginDate));
    }

    [Test]
    public async Task TimeObjectTaskDeleteConfirmation_YesConfirmsAndCloses()
    {
        await using BunitContext context = CreateContext();
        bool confirmed = false;
        bool? displayChanged = null;

        IRenderedComponent<TimeObjectTaskDeleteConfirmation> cut = context.Render<TimeObjectTaskDeleteConfirmation>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Title, "Delete time object")
            .Add(p => p.Message, "Delete this task?")
            .Add(p => p.DisplayChanged, value => displayChanged = value)
            .Add(p => p.Confirmed, () => confirmed = true));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Delete this task?"));
            Assert.That(cut.FindAll("button.btn-danger"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("button.btn-secondary"), Has.Count.EqualTo(1));
        });

        await cut.InvokeAsync(() => cut.Find("button.btn-danger").Click());

        Assert.Multiple(() =>
        {
            Assert.That(confirmed, Is.True);
            Assert.That(displayChanged, Is.False);
            Assert.That(cut.Instance.Display, Is.False);
        });
    }

    [Test]
    public async Task TimeObjectTaskDeleteConfirmation_NoClosesWithoutConfirming()
    {
        await using BunitContext context = CreateContext();
        bool confirmed = false;
        bool? displayChanged = null;

        IRenderedComponent<TimeObjectTaskDeleteConfirmation> cut = context.Render<TimeObjectTaskDeleteConfirmation>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Message, "Delete this task?")
            .Add(p => p.DisplayChanged, value => displayChanged = value)
            .Add(p => p.Confirmed, () => confirmed = true));

        await cut.InvokeAsync(() => cut.Find("button.btn-secondary").Click());

        Assert.Multiple(() =>
        {
            Assert.That(confirmed, Is.False);
            Assert.That(displayChanged, Is.False);
            Assert.That(cut.Instance.Display, Is.False);
        });
    }

    [Test]
    public async Task TargetDatesEditor_RendersTimeObjectSelectorAndDateEditor()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel model = new()
        {
            BeginDate = new DateTime(2026, 8, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 8, 1, 17, 0, 0),
            FlowTimeObjectId = 12,
            FlowTimeObjectName = "Business hours"
        };

        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindComponent<TimeObjectSelector>(), Is.Not.Null);
            Assert.That(cut.FindComponent<DisplayTaskTargetDates>(), Is.Not.Null);
        });
    }

    [Test]
    public async Task TargetDatesEditor_CreateTimeObjectTaskButtonRaisesCallback()
    {
        await using BunitContext context = CreateContext();
        bool requested = false;
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.AllowCreateTimeObjectTask, true)
            .Add(p => p.CreateTimeObjectTaskRequested,
                EventCallback.Factory.Create(this, () => requested = true)));

        cut.Find("button.btn-warning").Click();

        Assert.That(requested, Is.True);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RunWithProgressResetsProgressAfterAction()
    {
        ApprovalTaskDetailsView component = new();
        bool wasInProgress = false;
        Func<Task> action = () =>
        {
            wasInProgress = UiRequestWorkflowTest.GetMember<bool>(component, "workInProgress");
            return Task.CompletedTask;
        };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "RunWithProgress", action);

        Assert.Multiple(() =>
        {
            Assert.That(wasInProgress, Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "workInProgress"), Is.False);
        });
    }

    [Test]
    public async Task TargetDatesEditor_DateChangeClearsSelectedObject()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel model = new()
        {
            BeginDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 2),
            FlowTimeObjectId = 12,
            FlowTimeObjectName = "Business hours"
        };
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        DateTime changed = new(2026, 8, 3);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTargetBeginDateChanged", changed);
        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");

        Assert.Multiple(() =>
        {
            Assert.That(editingModel.BeginDate, Is.EqualTo(changed));
            Assert.That(editingModel.FlowTimeObjectId, Is.Null);
            Assert.That(editingModel.FlowTimeObjectName, Is.Null);
            Assert.That(editingModel.TicketElementId, Is.Null);
        });
    }

    [Test]
    public async Task TargetDatesEditor_SelectingTicketObjectSetsElementIdAndDates()
    {
        await using BunitContext context = CreateContext();
        FlowTimeObject ticketObject = new() { Id = -7, Name = "Ticket window" };
        TargetDatesEditorModel model = new()
        {
            TicketTimeObjects = [ticketObject],
            TicketTimeObjectElementIds = new Dictionary<long, long> { [-7] = 77 }
        };
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnSelectedTimeObjectIdChanged", -7L);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTimeObjectSelected",
            new FlowTimeObject
            {
                Id = -7,
                StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 17, 0, 0)
            });
        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");

        Assert.Multiple(() =>
        {
            Assert.That(editingModel.FlowTimeObjectId, Is.EqualTo(-7));
            Assert.That(editingModel.TicketElementId, Is.EqualTo(77));
            Assert.That(editingModel.BeginDate, Is.EqualTo(new DateTime(2026, 8, 1, 8, 0, 0)));
            Assert.That(editingModel.EndDate, Is.EqualTo(new DateTime(2026, 8, 1, 17, 0, 0)));
        });
    }

    [Test]
    public async Task TargetDatesEditor_SavesSelectedTimeObjectModel()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel? saved = null;
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, new TargetDatesEditorModel { FlowTimeObjectId = 42 })
            .Add(p => p.Saved, value => saved = value));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "Save");

        Assert.That(saved?.FlowTimeObjectId, Is.EqualTo(42));
    }

    [Test]
    public async Task TargetDatesEditor_ValidationAndNameAndEndDateCallbacksUpdateModel()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, new TargetDatesEditorModel
            {
                FlowTimeObjectId = 12,
                FlowTimeObjectName = "old-name",
                EndDate = new DateTime(2026, 8, 1)
            }));

        UiRequestWorkflowTest.GetPrivateMethod(typeof(TargetDatesEditor), "SetTargetDatesValid")
            .Invoke(cut.Instance, [false]);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnSelectedTimeObjectNameChanged", "new-name");
        DateTime end = new(2026, 8, 2);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTargetEndDateChanged", end);

        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");
        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "targetDatesValid"), Is.False);
            Assert.That(editingModel.FlowTimeObjectName, Is.Null);
            Assert.That(editingModel.EndDate, Is.EqualTo(end));
            Assert.That(editingModel.FlowTimeObjectId, Is.Null);
        });
    }

    [Test]
    public void ApprovalTaskDetailsView_GroupsOnlyMatchingBundledTasks()
    {
        SimulatedUserConfig userConfig = new()
        {
            ReqConsiderBundling = true,
            ReqDisplayBundledTasksAsOne = true
        };
        WfReqTask first = BundledAccessTask(1, 7, 2);
        WfReqTask second = BundledAccessTask(2, 7, 2);
        WfReqTask differentState = BundledAccessTask(3, 7, 3);
        WfReqTask unbundled = new() { Id = 4, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [first, second, differentState, unbundled] } };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "userConfig", userConfig);

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "GetTaskGroups");
        List<List<WfReqTask>> groups = [.. (IEnumerable<List<WfReqTask>>)method.Invoke(component, [])!];

        Assert.Multiple(() =>
        {
            Assert.That(groups, Has.Count.EqualTo(3));
            Assert.That(groups.Single(group => group.Any(task => task.Id == 1)), Has.Count.EqualTo(2));
            Assert.That(groups.Single(group => group.Any(task => task.Id == 3)), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ApprovalTaskDetailsView_AppliesFlowTimeObjectModelToTask()
    {
        WfReqTask task = new() { Id = 10, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = new() { ActReqTask = task, ActTicket = new WfTicket { Tasks = [task] } };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectId = 42,
            FlowTimeObjectName = "Business hours",
            BeginDate = new DateTime(2026, 8, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 8, 1, 17, 0, 0)
        };

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "ApplyTimeObjectModel");
        method.Invoke(component, [model]);

        WfReqElement? element = TimeObjectElementHelper.Find(task.Elements);
        Assert.Multiple(() =>
        {
            Assert.That(element?.FlowTimeObjectId, Is.EqualTo(42));
            Assert.That(element?.Name, Is.EqualTo("Business hours"));
            Assert.That(element?.StartTime, Is.EqualTo(model.BeginDate));
            Assert.That(element?.EndTime, Is.EqualTo(model.EndDate));
        });
    }

    [Test]
    public void ApprovalTaskDetailsView_SelectingTicketTimeObjectUsesExplicitElement()
    {
        WfReqTask accessTask = new()
        {
            Id = 10,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                ObjectTaskId = 50
            }]
        };
        WfReqTask oldTimeTask = new()
        {
            Id = 50,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Id = 101, Field = ElemFieldType.time.ToString(), Name = "old" }]
        };
        WfReqTask newTimeTask = new()
        {
            Id = 51,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Id = 102, Field = ElemFieldType.time.ToString(), Name = "new" }]
        };
        WfHandler handler = new() { ActReqTask = accessTask, ActTicket = new WfTicket { Tasks = [accessTask, oldTimeTask, newTimeTask] } };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectId = -102,
            TicketElementId = 102,
            FlowTimeObjectName = "new"
        };

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "ApplyTimeObjectModel");
        method.Invoke(component, [model]);

        Assert.That(TimeObjectElementHelper.Find(accessTask.Elements)?.ObjectTaskId, Is.EqualTo(51));
    }

    [Test]
    public void ApprovalTaskDetailsView_DisplayHelpersResolveTaskMetadata()
    {
        WfReqTask task = new()
        {
            Id = 10,
            TaskType = WfTaskType.access.ToString(),
            RuleAction = 3,
            Tracking = 4,
            CurrentHandler = new UiUser { Name = "handler" },
            AssignedGroup = "assigned-group",
            ManagementId = 8,
            Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 1, Name = "owner" } }],
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                Name = "maintenance",
                StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 17, 0, 0)
            }]
        };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler),
            new WfHandler { ActTicket = new WfTicket { Tasks = [task] } });
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());
        UiRequestWorkflowTest.SetMember(component, "ruleActions", new List<RuleAction> { new() { Id = 3, Name = "allow" } });
        UiRequestWorkflowTest.SetMember(component, "trackings", new List<Tracking> { new() { Id = 4, Name = "log" } });
        ApprovalTaskDisplayLogic displayLogic = new(
            new WfHandler { ActTicket = new WfTicket { Tasks = [task] } },
            new SimulatedUserConfig(),
            [new RuleAction { Id = 3, Name = "allow" }],
            [new Tracking { Id = 4, Name = "log" }],
            [],
            [],
            false);

        Assert.Multiple(() =>
        {
            Assert.That(InvokeApprovalHelper<string>(component, "TaskIds", new[] { task }), Is.EqualTo("10"));
            Assert.That(displayLogic.Handlers([task]), Is.EqualTo("handler"));
            Assert.That(displayLogic.AssignedGroups([task]), Is.EqualTo("assigned-group"));
            Assert.That(displayLogic.ManagementIds([task]), Is.EqualTo("8"));
            Assert.That(displayLogic.Owners([task]), Is.EqualTo("owner"));
            Assert.That(displayLogic.RuleActions([task]), Is.EqualTo("allow"));
            Assert.That(displayLogic.Trackings([task]), Is.EqualTo("log"));
            Assert.That(displayLogic.TimeObjectDisplay([task], new AccessTaskFieldVisibilityConfig()), Does.StartWith("maintenance ("));
        });
    }

    [Test]
    public void ApprovalTaskDetailsView_DoesNotOfferEditsAfterFlowDbReference()
    {
        WfReqTask task = new()
        {
            Id = 11,
            StateId = 2,
            TaskType = WfTaskType.access.ToString(),
            FlowAccessId = 901
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };
        StateMatrixDict matrices = new();
        matrices.Matrices[WfTaskType.access.ToString()] = new StateMatrix { LowestInputState = 1, LowestEndState = 10 };
        UiRequestWorkflowTest.SetMember(handler, "stateMatrixDict", matrices);

        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        ApproverAllowedChangesConfig allowedChanges = new();
        allowedChanges.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Services, true);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig
        {
            ReqAllowedChangesByApprover = allowedChanges.ToConfigValue()
        });

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "CanEdit");
        bool canEdit = (bool)method.Invoke(component,
            new object[] { new List<WfReqTask> { task }, WorkflowEditableFieldKeys.Services })!;

        Assert.That(canEdit, Is.False);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RendersAccessTaskAndInitializesLookupData()
    {
        await using BunitContext context = CreateContext();
        UiRequestWorkflowTest.RequestWorkflowApiConn apiConnection =
            (UiRequestWorkflowTest.RequestWorkflowApiConn)context.Services.GetRequiredService<ApiConnection>();
        apiConnection.Managements = [new Management { Id = 8, Name = "Management" }];

        ApproverAllowedChangesConfig allowedChanges = new();
        foreach (string field in new[]
        {
            WorkflowEditableFieldKeys.Title,
            WorkflowEditableFieldKeys.Owner,
            WorkflowEditableFieldKeys.Management,
            WorkflowEditableFieldKeys.RuleAction,
            WorkflowEditableFieldKeys.Track,
            WorkflowEditableFieldKeys.ValidFrom,
            WorkflowEditableFieldKeys.ValidTo,
            WorkflowEditableFieldKeys.Reason,
            WorkflowEditableFieldKeys.FreeText,
            WorkflowEditableFieldKeys.Source,
            WorkflowEditableFieldKeys.Destination,
            WorkflowEditableFieldKeys.Services
        })
        {
            allowedChanges.SetTaskField(WfTaskType.access, field, true);
        }

        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.ReqAllowedChangesByApprover = allowedChanges.ToConfigValue();
        userConfig.ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
        {
            AccessTask = new AccessTaskFieldVisibilityConfig { ShowFreeText = true }
        }.ToConfigValue();

        WfReqTask task = new()
        {
            Id = 21,
            StateId = 2,
            Title = "Access task",
            TaskType = WfTaskType.access.ToString(),
            CurrentHandler = new UiUser { Name = "handler" },
            AssignedGroup = "approvers",
            RuleAction = 3,
            Tracking = 4,
            ManagementId = 8,
            Reason = "reason",
            FreeText = "free text",
            TargetBeginDate = new DateTime(2026, 8, 1, 8, 0, 0),
            TargetEndDate = new DateTime(2026, 8, 1, 17, 0, 0),
            Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7, Name = "owner" } }],
            Elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1", Name = "source" },
                new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.0.2", Name = "destination" },
                new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "https" },
                new WfReqElement
                {
                    Field = ElemFieldType.time.ToString(),
                    Name = "maintenance",
                    StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                    EndTime = new DateTime(2026, 8, 1, 17, 0, 0)
                }
            ]
        };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Tasks = [task] },
            Managements = apiConnection.Managements,
            AllOwners = [new FwoOwner { Id = 7, Name = "owner" }]
        };
        bool? displayChanged = null;

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.DisplayChanged, value => displayChanged = value)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Planning" } }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".card"), Has.Count.EqualTo(1));
            Assert.That(cut.Markup, Does.Contain("Access task"));
            Assert.That(cut.Markup, Does.Contain("handler"));
            Assert.That(cut.Markup, Does.Contain("source"));
            Assert.That(cut.Markup, Does.Contain("destination"));
            Assert.That(cut.Markup, Does.Contain("https"));
            Assert.That(cut.Markup, Does.Contain("maintenance"));
            Assert.That(apiConnection.Queries, Does.Contain(StmQueries.getRuleActions));
            Assert.That(apiConnection.Queries, Does.Contain(StmQueries.getTracking));
            Assert.That(apiConnection.Queries, Does.Contain(StmQueries.getIpProtocols));
        });

        cut.Find("button.btn-secondary").Click();
        Assert.That(cut.Markup, Does.Contain("unresolved_groups_view"));

        cut.Find("button.btn-primary").Click();
        Assert.That(displayChanged, Is.False);
    }

    [Test]
    public void ApprovalTaskDetailsView_DisplayHelpersCoverModesAndEmptyGroups()
    {
        ApprovalTaskDetailsView component = new();
        WfReqTask accessTask = new() { Id = 71, TaskType = WfTaskType.access.ToString() };
        WfReqTask objectTask = new() { Id = 72, TaskType = WfTaskType.object_create.ToString() };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [accessTask, objectTask] } };
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());

        MethodInfo groupsMethod = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "GetTaskGroups");
        List<List<WfReqTask>> groups = [.. (IEnumerable<List<WfReqTask>>)groupsMethod.Invoke(component, [])!];
        MethodInfo detailsOpenMethod = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "DetailsOpen");
        MethodInfo nextModeMethod = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "NextDisplayModeTextKey");

        Assert.Multiple(() =>
        {
            Assert.That(groups, Has.Count.EqualTo(2));
            Assert.That(detailsOpenMethod.Invoke(null, [accessTask]), Is.EqualTo("open"));
            Assert.That(detailsOpenMethod.Invoke(null, [objectTask]), Is.Null);
            Assert.That(nextModeMethod.Invoke(component, []), Is.EqualTo("technical_view"));
        });

        UiRequestWorkflowTest.SetMember(component, "accessElementDisplayMode", ApprovalTaskElementDisplayMode.Technical);
        Assert.That(nextModeMethod.Invoke(component, []), Is.EqualTo("unresolved_groups_view"));
        UiRequestWorkflowTest.SetMember(component, "accessElementDisplayMode", ApprovalTaskElementDisplayMode.UnresolvedGroups);
        Assert.That(nextModeMethod.Invoke(component, []), Is.EqualTo("resolved_view"));
    }

    [Test]
    public void ApprovalTaskDetailsView_GetTasksToUpdateFollowsBundleAndState()
    {
        SimulatedUserConfig userConfig = new()
        {
            ReqConsiderBundling = true,
            ReqDisplayBundledTasksAsOne = true
        };
        WfReqTask selected = BundledAccessTask(81, 9, 2);
        WfReqTask sameBundle = BundledAccessTask(82, 9, 2);
        WfReqTask differentState = BundledAccessTask(83, 9, 3);
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [selected, sameBundle, differentState] } };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "userConfig", userConfig);

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "GetTasksToUpdate");
        List<WfReqTask> result = [.. (IEnumerable<WfReqTask>)method.Invoke(component, [selected])!];

        Assert.That(result.Select(task => task.Id), Is.EqualTo(new[] { 81L, 82L }));
    }

    [Test]
    public void ApprovalTaskDetailsView_FormerTimeObjectDeletionOnlyOfferedWhenUnused()
    {
        WfReqTask formerObjectTask = new()
        {
            Id = 91,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Id = 901, Field = ElemFieldType.time.ToString(), Name = "old" }]
        };
        WfReqTask selectedAccessTask = new()
        {
            Id = 92,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                ObjectTaskId = formerObjectTask.Id,
                Name = "old"
            }]
        };
        WfHandler handler = new()
        {
            ActReqTask = selectedAccessTask,
            ActTicket = new WfTicket { Tasks = [formerObjectTask, selectedAccessTask] }
        };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "formerTimeObjectTask", formerObjectTask);
        UiRequestWorkflowTest.SetMember(component, "formerTimeObjectSelectorId", -901L);
        UiRequestWorkflowTest.SetMember(component, "editingTargetDatesTask", selectedAccessTask);

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "ShouldOfferFormerTimeObjectDeletion");
        TargetDatesEditorModel changedModel = new() { FlowTimeObjectId = 42 };

        Assert.That(method.Invoke(component, [changedModel]), Is.True);

        WfReqTask otherAccessTask = new()
        {
            Id = 93,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                ObjectTaskId = formerObjectTask.Id
            }]
        };
        handler.ActTicket.Tasks.Add(otherAccessTask);

        Assert.That(method.Invoke(component, [changedModel]), Is.False);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_HidesConfiguredFieldsAndEditButtons()
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
        {
            AccessTask = new AccessTaskFieldVisibilityConfig
            {
                ShowOwner = false,
                ShowManagement = false,
                ShowValidFrom = false,
                ShowValidTo = false,
                ShowReason = false,
                ShowFreeText = false
            }
        }.ToConfigValue();

        WfReqTask task = new() { Id = 22, StateId = 1, Title = "Hidden metadata", TaskType = WfTaskType.access.ToString() };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Not.Contain("owner:"));
            Assert.That(cut.Markup, Does.Not.Contain("management:"));
            Assert.That(cut.Markup, Does.Not.Contain("time_object:"));
            Assert.That(cut.Markup, Does.Not.Contain("reason:"));
            Assert.That(cut.Markup, Does.Not.Contain("free_text:"));
            Assert.That(cut.FindAll("button.btn-warning"), Is.Empty);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_OpensAllFieldEditors()
    {
        WfReqTask task = new()
        {
            Id = 23,
            TaskType = WfTaskType.access.ToString(),
            Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7, Name = "owner" } }],
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                Name = "window",
                StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 17, 0, 0)
            }]
        };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Tasks = [task] },
            AllOwners = [new FwoOwner { Id = 7, Name = "owner" }],
            Managements = [new Management { Id = 8, Name = "management" }]
        };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenOwnerEditor", task);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenRuleActionEditor", task);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenTrackingEditor", task);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenManagementEditor", task);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenTargetDatesEditor", task, WorkflowEditableFieldKeys.ValidTo);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenTextEditor", task, WorkflowEditableFieldKeys.Reason, "reason");

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayOwnerEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayRuleActionEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTrackingEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayManagementEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTargetDatesEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTextEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<string>(component, "textEditorKey"), Is.EqualTo(WorkflowEditableFieldKeys.Reason));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "textEditorMultiline"), Is.True);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_HandlesEmptySaveCallbacksAndCloseToggle()
    {
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.Display), true);

        await InvokeApprovalTaskWithNull(component, "SaveOwner", new FwoOwner());
        await InvokeApprovalTaskWithNull(component, "SaveRuleAction", null);
        await InvokeApprovalTaskWithNull(component, "SaveTracking", null);
        await InvokeApprovalTaskWithNull(component, "SaveManagement", null);
        await InvokeApprovalTaskWithNull(component, "SaveTargetDates", new TargetDatesEditorModel());
        await InvokeApprovalTaskWithNull(component, "SaveText", "text");

        MethodInfo toggleMethod = typeof(ApprovalTaskDetailsView).GetMethod("ToggleAccessElementDisplay", BindingFlags.NonPublic | BindingFlags.Instance)!;
        MethodInfo closeMethod = typeof(ApprovalTaskDetailsView).GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance)!;
        toggleMethod.Invoke(component, []);
        ApprovalTaskElementDisplayMode displayMode = UiRequestWorkflowTest.GetMember<ApprovalTaskElementDisplayMode>(component, "accessElementDisplayMode");
        closeMethod.Invoke(component, []);

        Assert.Multiple(() =>
        {
            Assert.That(displayMode, Is.EqualTo(ApprovalTaskElementDisplayMode.Technical));
            Assert.That(component.Display, Is.False);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_OpenObjectTaskEditorUsesSelectedFieldKind()
    {
        WfReqTask accessTask = new()
        {
            Id = 91,
            TaskType = WfTaskType.access.ToString(),
            ManagementId = 8
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 12, Tasks = [accessTask] } };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, "editingElementsTask", accessTask);
        UiRequestWorkflowTest.SetMember(component, "editingElementsField", WorkflowEditableFieldKeys.Services);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenObjectTaskEditor");

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayObjectTaskEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayElementsEditor"), Is.False);
            Assert.That(UiRequestWorkflowTest.GetMember<ObjectTaskKind>(component, "newObjectTaskKind"), Is.EqualTo(ObjectTaskKind.Service));
            Assert.That(UiRequestWorkflowTest.GetMember<WfReqTask>(component, "newObjectTask").TaskNumber, Is.EqualTo(1));
            Assert.That(UiRequestWorkflowTest.GetMember<WfReqTask>(component, "newObjectTask").ManagementId, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ClosingObjectTaskEditorRestoresElementsEditor()
    {
        await using BunitContext context = CreateContext();
        WfReqTask accessTask = new() { Id = 94, TaskType = WfTaskType.access.ToString() };
        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, new WfHandler { ActTicket = new WfTicket { Tasks = [accessTask] } }));

        UiRequestWorkflowTest.SetMember(cut.Instance, "editingElementsTask", accessTask);
        UiRequestWorkflowTest.SetMember(cut.Instance, "displayObjectTaskEditor", true);
        UiRequestWorkflowTest.SetMember(cut.Instance, "displayElementsEditor", false);

        await cut.InvokeAsync(async () => await UiRequestWorkflowTest.InvokePrivateTask(
            cut.Instance, "HandleObjectTaskEditorDisplayChanged", false));

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayObjectTaskEditor"), Is.False);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayElementsEditor"), Is.True);
        });
    }

    [Test]
    public void ApprovalTaskDetailsView_ObjectTaskReferenceCheckDistinguishesUsedAndUnusedTasks()
    {
        WfReqTask objectTask = new() { Id = 95, TaskType = WfTaskType.object_create.ToString() };
        WfReqTask usingTask = new()
        {
            Id = 96,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), TaskId = objectTask.Id }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [objectTask, usingTask] } };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "IsObjectTaskReferenced");

        Assert.That(method.Invoke(component, [objectTask]), Is.True);

        usingTask.Elements.Clear();
        Assert.That(method.Invoke(component, [objectTask]), Is.False);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_DisplayFalseRendersNoTaskCards()
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new() { Id = 97, Title = "hidden", TaskType = WfTaskType.access.ToString() };
        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, false)
            .Add(p => p.WfHandler, new WfHandler { ActTicket = new WfTicket { Tasks = [task] } }));

        Assert.That(cut.Markup, Does.Not.Contain("hidden"));
    }

    [Test]
    public async Task ApprovalTaskDetailsView_LookupFailureFallsBackToEmptyDisplayData()
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.DesignatedZoneMatrixId = 7;
        WfReqTask task = new() { Id = 97, Title = "fallback", TaskType = WfTaskType.access.ToString() };

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, new WfHandler { ActTicket = new WfTicket { Tasks = [task] } }));

        Assert.That(cut.Markup, Does.Contain("fallback"));
        Assert.That(UiRequestWorkflowTest.GetMember<List<RuleAction>>(cut.Instance, "ruleActions"), Is.Empty);
        Assert.That(UiRequestWorkflowTest.GetMember<List<Tracking>>(cut.Instance, "trackings"), Is.Empty);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_CoversGuardBranchesAndNonBundledGroups()
    {
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig
        {
            ReqConsiderBundling = false,
            ReqDisplayBundledTasksAsOne = false
        });
        WfReqTask accessTask = new() { Id = 98, TaskType = WfTaskType.access.ToString() };
        WfReqTask objectTask = new() { Id = 99, TaskType = WfTaskType.object_create.ToString() };
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler),
            new WfHandler { ActTicket = new WfTicket { Tasks = [accessTask, objectTask] } });

        MethodInfo groupsMethod = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "GetTaskGroups");
        List<List<WfReqTask>> groups = [.. (IEnumerable<List<WfReqTask>>)groupsMethod.Invoke(component, [])!];
        Assert.That(groups, Has.Count.EqualTo(2));

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEdit", (object)Array.Empty<WfReqTask>(), WorkflowEditableFieldKeys.Title), Is.False);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditTimeObject", (object)Array.Empty<WfReqTask>()), Is.False);
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "EditableTimeObjectField")
                .Invoke(component, [Array.Empty<WfReqTask>()]), Is.EqualTo(WorkflowEditableFieldKeys.ValidTo));
        });

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenObjectTaskEditor");
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenCreateTimeObjectTaskEditor");
        await UiRequestWorkflowTest.InvokePrivateTask(component, "DeleteObjectTask");
        await UiRequestWorkflowTest.InvokePrivateTask(component, "DeleteFormerTimeObjectTask");
        await UiRequestWorkflowTest.InvokePrivateTask(component, "SaveElements", WorkflowEditableFieldKeys.Source,
            Array.Empty<NwObjectElement>(), Array.Empty<NwObjectElement>(), Array.Empty<NwServiceElement>());

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenOwnerEditor", accessTask);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenRuleActionEditor", accessTask);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenTrackingEditor", accessTask);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenManagementEditor", accessTask);
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenTextEditor", accessTask, WorkflowEditableFieldKeys.Title, "title");
        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenElementsEditor", accessTask, WorkflowEditableFieldKeys.Destination);

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayOwnerEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayRuleActionEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTrackingEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayManagementEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTextEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayElementsEditor"), Is.True);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RunWithProgressIgnoresConcurrentInvocation()
    {
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, "workInProgress", true);
        bool executed = false;

        await UiRequestWorkflowTest.InvokePrivateTask(component, "RunWithProgress", new Func<Task>(() =>
        {
            executed = true;
            return Task.CompletedTask;
        }));

        Assert.That(executed, Is.False);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_SavesAllMetadataThroughTheWorkflowHandler()
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new()
        {
            Id = 100,
            TicketId = 10,
            TaskNumber = 1,
            TaskType = WfTaskType.access.ToString(),
            Title = "old title",
            Reason = "old reason",
            FreeText = "old text"
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 10, Tasks = [task] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 1 });
        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler));

        RuleAction action = new() { Id = 3, Name = "allow" };
        Tracking tracking = new() { Id = 4, Name = "log" };
        Management management = new() { Id = 5, Name = "management" };
        FwoOwner owner = new() { Id = 6, Name = "owner" };

        UiRequestWorkflowTest.SetMember(cut.Instance, "editingRuleActionTask", task);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveRuleActionCore", action);
        task = handler.ActTicket.Tasks.Single(currentTask => currentTask.Id == task.Id);
        UiRequestWorkflowTest.SetMember(cut.Instance, "editingTrackingTask", task);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveTrackingCore", tracking);
        task = handler.ActTicket.Tasks.Single(currentTask => currentTask.Id == task.Id);
        UiRequestWorkflowTest.SetMember(cut.Instance, "editingManagementTask", task);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveManagementCore", management);
        task = handler.ActTicket.Tasks.Single(currentTask => currentTask.Id == task.Id);
        UiRequestWorkflowTest.SetMember(cut.Instance, "editingOwnerTask", task);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveOwnerCore", owner);
        task = handler.ActTicket.Tasks.Single(currentTask => currentTask.Id == task.Id);

        foreach (string key in new[] { WorkflowEditableFieldKeys.Title, WorkflowEditableFieldKeys.Reason, WorkflowEditableFieldKeys.FreeText })
        {
            UiRequestWorkflowTest.SetMember(cut.Instance, "editingTextTask", task);
            UiRequestWorkflowTest.SetMember(cut.Instance, "textEditorKey", key);
            await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveTextCore", $"changed-{key}");
            task = handler.ActTicket.Tasks.Single(currentTask => currentTask.Id == task.Id);
        }

        WfReqTask savedTask = task;
        Assert.Multiple(() =>
        {
            Assert.That(savedTask.RuleAction, Is.EqualTo(3));
            Assert.That(savedTask.Tracking, Is.EqualTo(4));
            Assert.That(savedTask.ManagementId, Is.EqualTo(5));
            Assert.That(savedTask.NewOwners.Single().Id, Is.EqualTo(6));
            Assert.That(savedTask.Title, Is.EqualTo($"changed-{WorkflowEditableFieldKeys.Title}"));
            Assert.That(savedTask.Reason, Is.EqualTo($"changed-{WorkflowEditableFieldKeys.Reason}"));
            Assert.That(savedTask.FreeText, Is.EqualTo($"changed-{WorkflowEditableFieldKeys.FreeText}"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_SavesNewObjectTaskAndReturnsToElementEditor()
    {
        await using BunitContext context = CreateContext();
        WfReqTask accessTask = new() { Id = 101, TaskNumber = 1, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 11, Tasks = [accessTask] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 2 });
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.object_create.ToString(), new StateMatrix { LowestInputState = 2 });
        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler));

        WfReqTask objectTask = new()
        {
            Id = 102,
            TicketId = 11,
            TaskNumber = 2,
            StateId = 2,
            TaskType = WfTaskType.object_create.ToString(),
            Title = "draft",
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "new-source" }]
        };
        UiRequestWorkflowTest.SetMember(cut.Instance, "editingElementsTask", accessTask);
        UiRequestWorkflowTest.SetMember(cut.Instance, "displayObjectTaskEditor", true);
        UiRequestWorkflowTest.SetMember(cut.Instance, "displayElementsEditor", false);

        await cut.InvokeAsync(async () => await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveObjectTaskCore", objectTask));

        WfReqTask savedObjectTask = handler.ActTicket.Tasks.Single(currentTask => currentTask.Id == objectTask.Id);
        Assert.Multiple(() =>
        {
            Assert.That(savedObjectTask, Is.Not.SameAs(objectTask));
            Assert.That(savedObjectTask.Title, Is.EqualTo("new-source"));
            Assert.That(savedObjectTask.StateId, Is.EqualTo(2));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayObjectTaskEditor"), Is.False);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayElementsEditor"), Is.True);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_SavingElementsOffersUnusedFormerObjectTaskForDeletion()
    {
        await using BunitContext context = CreateContext();
        WfReqTask objectTask = new() { Id = 103, TaskNumber = 1, TaskType = WfTaskType.object_create.ToString() };
        WfReqTask accessTask = new()
        {
            Id = 104,
            TaskNumber = 2,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Id = 1041, Field = ElemFieldType.source.ToString(), TaskId = objectTask.Id, Name = "old" }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 12, Tasks = [objectTask, accessTask] } };
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { LowestInputState = 2 });
        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler));
        UiRequestWorkflowTest.SetMember(cut.Instance, "editingElementsTask", accessTask);

        await cut.InvokeAsync(async () => await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveElementsCore",
            WorkflowEditableFieldKeys.Source, Array.Empty<NwObjectElement>(), Array.Empty<NwObjectElement>(), Array.Empty<NwServiceElement>()));

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayDeleteObjectTask"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<WfReqTask>(cut.Instance, "pendingObjectTask"), Is.SameAs(objectTask));
            Assert.That(UiRequestWorkflowTest.GetMember<object?>(cut.Instance, "editingElementsTask"), Is.Null);
        });
    }

    [Test]
    public void ApprovalTaskDetailsView_NewObjectTaskUsesOneInitialStateForTaskAndApproval()
    {
        WfHandler handler = new()
        {
            ActStateMatrix = new StateMatrix { LowestInputState = 55 }
        };
        WfReqTask task = new()
        {
            StateId = 49,
            Approvals = new List<WfApproval> { new() { StateId = 49 } }
        };
        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);

        MethodInfo method = typeof(ApprovalTaskDetailsView).GetMethod("InitializeNewObjectTaskState", BindingFlags.NonPublic | BindingFlags.Instance)!;
        List<object> arguments = new() { task };
        method.Invoke(component, arguments.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(task.StateId, Is.EqualTo(55));
            Assert.That(task.Approvals.Single().StateId, Is.EqualTo(55));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RendersObjectCreateAndModifyTasks()
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        ApproverAllowedChangesConfig allowedChanges = new();
        allowedChanges.SetTaskField(WfTaskType.object_create, WorkflowEditableFieldKeys.Title, true);
        allowedChanges.SetTaskField(WfTaskType.object_create, WorkflowEditableFieldKeys.Management, true);
        allowedChanges.SetTaskField(WfTaskType.object_create, WorkflowEditableFieldKeys.Reason, true);
        allowedChanges.SetTaskField(WfTaskType.object_modify, WorkflowEditableFieldKeys.Title, true);
        allowedChanges.SetTaskField(WfTaskType.object_modify, WorkflowEditableFieldKeys.Reason, true);
        userConfig.ReqAllowedChangesByApprover = allowedChanges.ToConfigValue();

        WfReqTask createTask = new()
        {
            Id = 31,
            StateId = 2,
            Title = "Create server object",
            TaskType = WfTaskType.object_create.ToString(),
            ManagementId = 8,
            Reason = "new server",
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = "server-new",
                IpString = "10.20.0.10",
                RequestAction = RequestAction.create.ToString()
            }]
        };
        WfReqTask modifyTask = new()
        {
            Id = 32,
            StateId = 2,
            Title = "Modify server object",
            TaskType = WfTaskType.object_modify.ToString(),
            Reason = "change address",
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "server-old",
                    IpString = "10.20.0.10",
                    NetworkId = 77,
                    RequestAction = RequestAction.unchanged.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "server-new",
                    IpString = "10.20.0.11",
                    NetworkId = 77,
                    RequestAction = RequestAction.modify.ToString()
                }
            ]
        };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Tasks = [createTask, modifyTask] },
            Managements = [new Management { Id = 8, Name = "Management" }]
        };
        StateMatrixDict matrices = new();
        matrices.Matrices[WfTaskType.object_create.ToString()] = new StateMatrix { LowestInputState = 1, LowestEndState = 10 };
        matrices.Matrices[WfTaskType.object_modify.ToString()] = new StateMatrix { LowestInputState = 1, LowestEndState = 10 };
        UiRequestWorkflowTest.SetMember(handler, "stateMatrixDict", matrices);

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".card"), Has.Count.EqualTo(2));
            Assert.That(cut.Markup, Does.Contain("Create server object"));
            Assert.That(cut.Markup, Does.Contain("Modify server object"));
            Assert.That(cut.Markup, Does.Contain("server-new"));
            Assert.That(cut.Markup, Does.Contain("server-old"));
            Assert.That(cut.Markup, Does.Contain("requested_state"));
            Assert.That(cut.FindAll("button.btn-warning"), Has.Count.GreaterThanOrEqualTo(4));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RendersGroupTasksOnSeparateCards()
    {
        await using BunitContext context = CreateContext();
        WfReqTask groupCreate = new()
        {
            Id = 41,
            StateId = 2,
            Title = "Create web group",
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    GroupName = "web-net",
                    Name = "server-a",
                    IpString = "10.30.0.10",
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };
        WfReqTask groupModify = new()
        {
            Id = 42,
            StateId = 2,
            Title = "Modify web group",
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    GroupName = "web-net",
                    Name = "server-old",
                    IpString = "10.30.0.11",
                    RequestAction = RequestAction.delete.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    GroupName = "web-net",
                    Name = "server-new",
                    IpString = "10.30.0.12",
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };
        WfReqTask groupDelete = new()
        {
            Id = 43,
            StateId = 2,
            Title = "Delete web group",
            TaskType = WfTaskType.group_delete.ToString()
        };
        groupDelete.SetAddInfo(AdditionalInfoKeys.GrpName, "old-web-net");

        WfReqTask serviceGroupCreate = new()
        {
            Id = 44,
            StateId = 2,
            Title = "Create service group",
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.service.ToString(),
                    GroupName = "web-services",
                    Name = "https-member",
                    Port = 443,
                    ProtoId = 6,
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };

        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupCreate, groupModify, groupDelete, serviceGroupCreate] } };
        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".card"), Has.Count.EqualTo(4));
            Assert.That(cut.Markup, Does.Contain("Create web group"));
            Assert.That(cut.Markup, Does.Contain("Modify web group"));
            Assert.That(cut.Markup, Does.Contain("Delete web group"));
            Assert.That(cut.Markup, Does.Contain("Create service group"));
            Assert.That(cut.Markup, Does.Contain("web-net"));
            Assert.That(cut.Markup, Does.Contain("server-old"));
            Assert.That(cut.Markup, Does.Contain("server-new"));
            Assert.That(cut.Markup, Does.Contain("old-web-net"));
            Assert.That(cut.Markup, Does.Contain("add_member"));
            Assert.That(cut.Markup, Does.Contain("remove_member"));
            Assert.That(cut.Markup, Does.Contain("https-member"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ToggleIsAboveFirstAccessCardAndCyclesViews()
    {
        await using BunitContext context = CreateContext();
        WfReqTask objectTask = new()
        {
            Id = 51,
            StateId = 2,
            Title = "Create object first",
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "object", IpString = "10.40.0.1" }]
        };
        WfReqTask accessTask = new()
        {
            Id = 52,
            StateId = 2,
            Title = "Access task second",
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "source", IpString = "10.40.0.2" }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [objectTask, accessTask] } };

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        int togglePosition = cut.Markup.IndexOf("technical_view", StringComparison.Ordinal);
        int accessPosition = cut.Markup.IndexOf("Access task second", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(togglePosition, Is.GreaterThanOrEqualTo(0));
            Assert.That(togglePosition, Is.LessThan(accessPosition));
        });

        cut.Find("button.btn-secondary").Click();
        Assert.That(cut.Markup, Does.Contain("unresolved_groups_view"));
        cut.Find("button.btn-secondary").Click();
        Assert.That(cut.Markup, Does.Contain("resolved_view"));
        cut.Find("button.btn-secondary").Click();
        Assert.That(cut.Markup, Does.Contain("technical_view"));
    }

    [Test]
    public async Task ApprovalTaskDetailsView_DoesNotOfferEditorsForCompletedTask()
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        ApproverAllowedChangesConfig allowedChanges = new();
        allowedChanges.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Title, true);
        allowedChanges.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Owner, true);
        allowedChanges.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Reason, true);
        userConfig.ReqAllowedChangesByApprover = allowedChanges.ToConfigValue();

        WfReqTask task = new()
        {
            Id = 61,
            StateId = 10,
            Title = "Already approved",
            TaskType = WfTaskType.access.ToString(),
            Reason = "reason",
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.50.0.1" }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };
        StateMatrixDict matrices = new();
        matrices.Matrices[WfTaskType.access.ToString()] = new StateMatrix { LowestInputState = 1, LowestEndState = 10 };
        UiRequestWorkflowTest.SetMember(handler, "stateMatrixDict", matrices);

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [10] = "Approved" } }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Already approved"));
            Assert.That(cut.FindAll("button.btn-warning"), Is.Empty);
        });
    }

    [Test]
    public async Task DisplayObjectTaskElement_ReadOnlyShowsTimeNameAndValidityTogether()
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new()
        {
            Id = 33,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.time.ToString(),
                    Name = "maintenance",
                    StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                    EndTime = new DateTime(2026, 8, 1, 17, 0, 0),
                    RequestAction = RequestAction.unchanged.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.time.ToString(),
                    StartTime = new DateTime(2026, 8, 2, 8, 0, 0),
                    EndTime = new DateTime(2026, 8, 2, 17, 0, 0),
                    RequestAction = RequestAction.modify.ToString()
                }
            ]
        };

        IRenderedComponent<DisplayObjectTaskElement> cut = context.Render<DisplayObjectTaskElement>(parameters => parameters
            .Add(p => p.ReqTask, task)
            .Add(p => p.IsModify, true)
            .Add(p => p.EditMode, false));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("maintenance"));
            Assert.That(cut.Markup, Does.Contain("2026-08-02 08:00"));
            Assert.That(cut.Markup, Does.Contain("2026-08-02 17:00"));
            Assert.That(cut.Markup, Does.Contain("previous_state"));
            Assert.That(cut.Markup, Does.Contain("requested_state"));
        });
    }

    private static T InvokeApprovalHelper<T>(ApprovalTaskDetailsView component, string methodName, WfReqTask[] tasks)
    {
        MethodInfo method = typeof(ApprovalTaskDetailsView).GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!;
        return (T)method.Invoke(method.IsStatic ? null : component, [tasks])!;
    }

    private static async Task InvokeApprovalTaskWithNull(ApprovalTaskDetailsView component, string methodName, object? argument)
    {
        MethodInfo method = typeof(ApprovalTaskDetailsView).GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task task = (Task)method.Invoke(component, new object?[] { argument })!;
        await task;
    }

    private static WfReqTask BundledAccessTask(long id, long bundleId, int stateId)
    {
        WfReqTask task = new() { Id = id, StateId = stateId, TaskType = WfTaskType.access.ToString() };
        task.SetAddInfo(AdditionalInfoKeys.FlowBundleId, bundleId.ToString());
        return task;
    }

    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
        return context;
    }
}

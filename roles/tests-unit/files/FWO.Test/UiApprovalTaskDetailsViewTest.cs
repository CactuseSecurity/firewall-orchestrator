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
internal class UiApprovalTaskDetailsViewTest : UiSharedEditorTestBase
{
    private static readonly long[] kExpectedBundledTaskIds = [81L, 82L];

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
    public async Task ApprovalTaskDetailsView_TaskPromotionCallbacksOpenCloseAndCancelPromotion()
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new() { Id = 7, TaskType = WfTaskType.access.ToString(), Title = "Access" };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };
        UiRequestWorkflowTest.SetMatrix(handler, task.TaskType, new StateMatrix());
        IRenderedComponent<ApprovalTaskDetailsView> rendered = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler));
        ApprovalTaskDetailsView component = rendered.Instance;

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenTaskPromotion", task);
        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTaskPromotion"), Is.True);
            Assert.That(handler.ActReqTask.Id, Is.EqualTo(task.Id));
        });

        await UiRequestWorkflowTest.InvokePrivateTask(component, "CloseTaskPromotion");
        Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTaskPromotion"), Is.False);

        UiRequestWorkflowTest.SetMember(component, "displayTaskPromotion", true);
        bool cancelled = UiRequestWorkflowTest.InvokePrivateBool(component, "CancelTaskPromotion");
        Assert.Multiple(() =>
        {
            Assert.That(cancelled, Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTaskPromotion"), Is.False);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_PromoteSelectedTaskPromotesAndClosesPopup()
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new() { Id = 8, TaskType = WfTaskType.access.ToString(), StateId = 0 };
        WfHandler handler = new()
        {
            ActReqTask = task,
            ActTicket = new WfTicket { Id = 1, Tasks = [task] },
            ActStateMatrix = new StateMatrix { LowestStartedState = 1, LowestEndState = 10 }
        };
        IRenderedComponent<ApprovalTaskDetailsView> rendered = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler));
        ApprovalTaskDetailsView component = rendered.Instance;
        UiRequestWorkflowTest.SetMember(component, "displayTaskPromotion", true);

        await UiRequestWorkflowTest.InvokePrivateTask(component, "PromoteSelectedTask", new WfStatefulObject { StateId = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(task.StateId, Is.EqualTo(2));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayTaskPromotion"), Is.False);
        });
    }

    [Test]
    public void ApprovalGroupTaskBody_DisplaysMemberNameWithNetworkDetails()
    {
        ApprovalGroupTaskBody component = new();
        NwObjectElement member = new("192.0.2.10", 7)
        {
            Name = "web-server"
        };

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalGroupTaskBody), "DisplayObject");
        string result = (string)method.Invoke(component, [member])!;

        Assert.That(result, Is.EqualTo("web-server (192.0.2.10)"));
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
    public void ApprovalTaskDetailsView_AllowsAccessTaskEditsAfterFlowDbReference()
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

        Assert.That(canEdit, Is.True);
    }

    [Test]
    public void ApprovalTaskDetailsView_AllowsEditingGroupContainingFlowDbObjects()
    {
        WfReqTask groupTask = new()
        {
            Id = 12,
            StateId = 2,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    FlowNetworkObjectId = 901,
                    Name = "server"
                }
            ]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupTask] } };
        StateMatrixDict matrices = new();
        matrices.Matrices[WfTaskType.group_modify.ToString()] = new StateMatrix
        {
            LowestInputState = 1,
            LowestEndState = 10
        };
        UiRequestWorkflowTest.SetMember(handler, "stateMatrixDict", matrices);

        ApprovalTaskDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler), handler);
        ApproverAllowedChangesConfig allowedChanges = new();
        allowedChanges.SetTaskField(WfTaskType.group_modify, WorkflowEditableFieldKeys.GroupMembers, true);
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig
        {
            ReqAllowedChangesByApprover = allowedChanges.ToConfigValue()
        });

        MethodInfo method = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "CanEdit");
        bool canEdit = (bool)method.Invoke(component,
            new object[] { new List<WfReqTask> { groupTask }, WorkflowEditableFieldKeys.GroupMembers })!;

        Assert.That(canEdit, Is.True);
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

        cut.Find("button.approval-task-display-toggle").Click();
        Assert.That(cut.Markup, Does.Contain("unresolved_groups_view"));

        cut.Find("button.approval-task-details-close").Click();
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
        MethodInfo nextModeMethod = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskDetailsView), "NextDisplayModeTextKey");

        Assert.Multiple(() =>
        {
            Assert.That(groups, Has.Count.EqualTo(2));
            Assert.That(ApprovalTaskDetailsEditorService.DetailsOpen(accessTask), Is.EqualTo("open"));
            Assert.That(ApprovalTaskDetailsEditorService.DetailsOpen(objectTask), Is.Null);
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

        Assert.That(result.Select(task => task.Id), Is.EqualTo(kExpectedBundledTaskIds));
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
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), ObjectTaskId = objectTask.Id }]
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
            Elements = [new WfReqElement { Id = 1041, Field = ElemFieldType.source.ToString(), TaskId = 104, ObjectTaskId = objectTask.Id, Name = "old" }]
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
}

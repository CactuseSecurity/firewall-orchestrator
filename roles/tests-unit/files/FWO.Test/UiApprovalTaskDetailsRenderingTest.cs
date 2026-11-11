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
internal class UiApprovalTaskDetailsRenderingTest : UiSharedEditorTestBase
{
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
            Assert.That(cut.Markup, Does.Contain("Create Object: Create server object"));
            Assert.That(cut.Markup, Does.Contain("Modify server object"));
            Assert.That(cut.Markup, Does.Contain("server-new"));
            Assert.That(cut.Markup, Does.Contain("server-old"));
            Assert.That(cut.Markup, Does.Contain("change_name"));
            Assert.That(cut.FindAll("button.btn-secondary"), Has.Count.GreaterThanOrEqualTo(4));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RendersGenericTasksWithReasonAndFreeText()
    {
        await using BunitContext context = CreateContext();
        WfReqTask genericTask = new()
        {
            Id = 36,
            StateId = 2,
            Title = "Generic task",
            TaskType = WfTaskType.generic.ToString(),
            Reason = "generic reason",
            FreeText = "generic details"
        };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Id = 354, Tasks = [genericTask] }
        };

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Generic task"));
            Assert.That(cut.Markup, Does.Contain("generic reason"));
            Assert.That(cut.Markup, Does.Contain("generic details"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RendersInterfaceTasksWithRequestingOwner()
    {
        await using BunitContext context = CreateContext();
        WfReqTask interfaceTask = new()
        {
            Id = 37,
            StateId = 2,
            Title = "Request interface",
            TaskType = WfTaskType.new_interface.ToString(),
            Reason = "connect application"
        };
        interfaceTask.SetAddInfo(AdditionalInfoKeys.ReqOwner, "19");
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Id = 355, Tasks = [interfaceTask] },
            AllOwners = [new FwoOwner { Id = 19, Name = "Requesting application" }]
        };

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Request interface"));
            Assert.That(cut.Markup, Does.Contain("Requesting application"));
            Assert.That(cut.Markup, Does.Contain("connect application"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_UsesTicketTitleIdAndStateInHeadline()
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new()
        {
            Id = 35,
            StateId = 2,
            Title = "Access task",
            TaskType = WfTaskType.access.ToString()
        };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket
            {
                Id = 353,
                Title = "Remote administration",
                StateId = 2,
                Requester = new UiUser { Name = "alice" },
                Tasks = [task]
            }
        };

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();
        Assert.That(cut.Markup, Does.Contain(
            $"Ticket: Remote administration (Id: 353, State: Approval, {userConfig.GetText("requester")}: alice)"));
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

        cut.Find("button.approval-task-display-toggle").Click();
        Assert.That(cut.Markup, Does.Contain("unresolved_groups_view"));
        cut.Find("button.approval-task-display-toggle").Click();
        Assert.That(cut.Markup, Does.Contain("resolved_view"));
        cut.Find("button.approval-task-display-toggle").Click();
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
    public async Task ApprovalTaskDetailsView_OfferedActionsRespectReadOnlyModeAndExecuteAllowedAction()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = new()
        {
            ReadOnlyMode = true,
            ActTicket = new WfTicket { Editable = true }
        };
        WfStateAction readOnlyAction = new()
        {
            ActionType = StateActionTypes.TrafficPathAnalysis.ToString(),
            ButtonText = "Inspect"
        };
        WfStateAction changingAction = new()
        {
            ActionType = StateActionTypes.SetAlert.ToString(),
            ButtonText = "Change"
        };
        List<WfStateAction> offeredActions = [readOnlyAction, changingAction];
        List<WfStateAction> performedActions = [];

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.OfferedActions, offeredActions)
            .Add(p => p.PerformTicketAction, (WfStateAction action) =>
            {
                performedActions.Add(action);
                return Task.CompletedTask;
            }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Inspect"));
            Assert.That(cut.Markup, Does.Not.Contain(">Change<"));
            Assert.That(cut.FindAll("button.btn-dark"), Has.Count.EqualTo(1));
        });

        await cut.Find("button.btn-dark").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(performedActions, Has.Count.EqualTo(1));
            Assert.That(performedActions[0], Is.SameAs(readOnlyAction));
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
            Assert.That(cut.Markup, Does.Contain("02.08.2026 08:00"));
            Assert.That(cut.Markup, Does.Contain("02.08.2026 17:00"));
            Assert.That(cut.Markup, Does.Contain("change_name"));
            Assert.That(cut.Markup, Does.Contain("change_content"));
        });
    }
}

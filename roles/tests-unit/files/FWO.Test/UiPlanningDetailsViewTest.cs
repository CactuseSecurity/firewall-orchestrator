using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal sealed class UiPlanningDetailsViewTest
{
    private static readonly object[] kNoArguments = [];

    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
        return context;
    }

    [Test]
    public static void PlanningDetailsView_FiltersReadOnlyAndStateChangingActions()
    {
        WfHandler handler = new()
        {
            ReadOnlyMode = true,
            ActTicket = new WfTicket { Editable = true }
        };
        PlanningDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.WfHandler), handler);
        WfStateAction readOnlyAction = new()
        {
            ActionType = StateActionTypes.TrafficPathAnalysis.ToString()
        };
        WfStateAction changingAction = new()
        {
            ActionType = StateActionTypes.SetAlert.ToString()
        };

        bool readOnlyVisible = UiRequestWorkflowTest.InvokePrivateBool(component, "CanShowOfferedAction", readOnlyAction);
        bool changingVisible = UiRequestWorkflowTest.InvokePrivateBool(component, "CanShowOfferedAction", changingAction);

        Assert.Multiple(() =>
        {
            Assert.That(readOnlyVisible, Is.True);
            Assert.That(changingVisible, Is.False);
        });
    }

    [Test]
    public static void PlanningDetailsView_InitializesRequestDetailsCollapsedAndTogglesIndependently()
    {
        WfReqTask first = new() { Id = 11, TaskType = WfTaskType.access.ToString() };
        WfReqTask second = new() { Id = 12, TaskType = WfTaskType.generic.ToString() };
        PlanningDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.Display), true);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.WfHandler), new WfHandler
        {
            ActTicket = new WfTicket { Id = 7, Tasks = [first, second] }
        });

        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningDetailsView), "OnParametersSet").Invoke(component, kNoArguments);

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "RequestTaskDetailsExpanded", first), Is.False);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "RequestTaskDetailsExpanded", second), Is.False);
        });

        object[] firstTaskArgument = [first];
        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningDetailsView), "ToggleRequestTaskDetails").Invoke(component, firstTaskArgument);

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "RequestTaskDetailsExpanded", first), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "RequestTaskDetailsExpanded", second), Is.False);
        });
    }

    [Test]
    public static async Task PlanningDetailsView_PreparesImplementationCreationAndDeleteActions()
    {
        WfImplTask existing = new() { Id = 21, TaskNumber = 3, TaskType = WfTaskType.access.ToString() };
        WfReqTask requestTask = new()
        {
            Id = 11,
            TaskType = WfTaskType.access.ToString(),
            ManagementId = 4,
            ImplementationTasks = [existing]
        };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningDetailsView> rendered = context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, false)
            .Add(parameter => parameter.WfHandler, handler));
        PlanningDetailsView component = rendered.Instance;

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenImplementationTaskCreation", requestTask);
        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "displayImplementationTaskCreation"), Is.True);
            Assert.That(handler.ActImplTask.TaskNumber, Is.EqualTo(4));
            Assert.That(handler.ActImplTask.ManagementId, Is.EqualTo(4));
        });

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenDeleteConfirmation", existing);
        Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);
    }

    [Test]
    public static async Task PlanningDetailsView_HidesMutationActionsOutsideActionableStates()
    {
        string taskType = WfTaskType.access.ToString();
        WfImplTask implementationTask = new() { Id = 21, TaskType = taskType, StateId = 1 };
        WfReqTask requestTask = new()
        {
            Id = 11,
            TaskType = taskType,
            StateId = 1,
            ImplementationTasks = [implementationTask]
        };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            taskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningDetailsView> rendered = context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, false)
            .Add(parameter => parameter.WfHandler, handler));
        PlanningDetailsView component = rendered.Instance;

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanMutateRequestTask", requestTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanMutateImplementationTask", implementationTask), Is.True);
        });

        requestTask.StateId = 10;
        Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanMutateRequestTask", requestTask), Is.False);

        requestTask.StateId = 1;
        implementationTask.StateId = 10;
        Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanMutateImplementationTask", implementationTask), Is.False);
    }

    [Test]
    public static void PlanningDetailsView_HeaderUsesTicketMetadata()
    {
        WfReqTask task = new() { Id = 11, Title = "Allow web", StateId = 2, TaskType = WfTaskType.access.ToString() };
        PlanningDetailsView component = new();
        SimulatedUserConfig userConfig = new();
        UiRequestWorkflowTest.SetMember(component, "userConfig", userConfig);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.States), new WfStateDict());
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Id = 7, Title = "Firewall change", Tasks = [task] }
        };
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.WfHandler), handler);

        string planningTitle = UiRequestWorkflowTest.GetMember<string>(component, "PlanningViewTitle");

        Assert.Multiple(() =>
        {
            Assert.That(planningTitle, Does.Contain("7"));
            Assert.That(planningTitle, Does.Contain("Firewall change"));
        });
    }

    [Test]
    public static async Task PlanningDetailsView_RendersRequestAndImplementationHeadersWithDetailsCollapsed()
    {
        WfReqTask requestTask = new()
        {
            Id = 11,
            Title = "Allow web",
            TaskType = WfTaskType.generic.ToString(),
            ImplementationTasks =
            [
                new WfImplTask { Id = 21, TaskType = WfTaskType.generic.ToString(), FreeText = "implementation" }
            ]
        };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Id = 7, Title = "Firewall change", Tasks = [requestTask] }
        };

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningDetailsView> cut = context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Allow web"));
            Assert.That(cut.Markup, Does.Contain("implementation"));
            Assert.That(cut.Markup, Does.Contain("btn-light"));
        });
    }

    [Test]
    public static async Task PlanningDetailsView_RendersAllRequestTaskBodyVariantsAfterExpansion()
    {
        List<WfReqTask> tasks =
        [
            new WfReqTask { Id = 1, Title = "access task", TaskType = WfTaskType.access.ToString() },
            new WfReqTask { Id = 2, Title = "group task", TaskType = WfTaskType.group_create.ToString() },
            new WfReqTask { Id = 3, Title = "generic task", TaskType = WfTaskType.generic.ToString() },
            new WfReqTask { Id = 4, Title = "interface task", TaskType = WfTaskType.new_interface.ToString() },
            new WfReqTask { Id = 5, Title = "object task", TaskType = WfTaskType.object_create.ToString() }
        ];
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Id = 7, Title = "Firewall change", Tasks = tasks }
        };

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningDetailsView> cut = context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler));

        foreach (WfReqTask task in tasks)
        {
            object[] taskArguments = [task];
            UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningDetailsView), "ToggleRequestTaskDetails")
                .Invoke(cut.Instance, taskArguments);
        }
        cut.Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("access task"));
            Assert.That(cut.Markup, Does.Contain("group task"));
            Assert.That(cut.Markup, Does.Contain("generic task"));
            Assert.That(cut.Markup, Does.Contain("interface task"));
            Assert.That(cut.Markup, Does.Contain("object task"));
        });
    }

    [Test]
    public static async Task PlanningDetailsView_CloseInvokesBothParentCallbacks()
    {
        bool? displayChanged = null;
        bool closed = false;
        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningDetailsView> cut = context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.Closed, () =>
            {
                closed = true;
                return Task.CompletedTask;
            }));

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(displayChanged, Is.False);
            Assert.That(closed, Is.True);
        });
    }

    [Test]
    public static async Task PlanningDetailsView_ResetClearsImplementationActionsAndRefreshesParent()
    {
        WfHandler handler = new()
        {
            DisplayImplTaskMode = true,
            EditImplTaskMode = true,
            AddImplTaskMode = true,
            DisplayDeleteImplTaskMode = true
        };
        bool parentReset = false;
        PlanningDetailsView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningDetailsView.ResetParent), () =>
        {
            parentReset = true;
            return Task.CompletedTask;
        });

        await UiRequestWorkflowTest.InvokePrivateTask(component, "Reset");

        Assert.Multiple(() =>
        {
            Assert.That(handler.DisplayImplTaskMode, Is.False);
            Assert.That(handler.EditImplTaskMode, Is.False);
            Assert.That(handler.AddImplTaskMode, Is.False);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.False);
            Assert.That(parentReset, Is.True);
        });
    }

}

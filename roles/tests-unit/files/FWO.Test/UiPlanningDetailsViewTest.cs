using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
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
    private static readonly string[] kGroupContents = ["one", "two", "three", "four", "five", "six"];

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
    public async Task ConfiguredTaskActions_RendersAllowedActionsAndInvokesExternalCallback()
    {
        WfStateAction offeredAction = new() { ButtonText = "Offer" };
        WfStateAction hiddenAction = new() { ButtonText = "Hidden" };
        List<WfStateAction> actions = [offeredAction, hiddenAction];
        WfStateAction? clickedAction = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<ConfiguredTaskActions> cut = context.Render<ConfiguredTaskActions>(parameters => parameters
            .Add(parameter => parameter.OfferedActions, actions)
            .Add(parameter => parameter.ActionVisibility, action => action == offeredAction)
            .Add(parameter => parameter.ActionRequested, action =>
            {
                clickedAction = action;
                return Task.CompletedTask;
            }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("button"), Has.Count.EqualTo(1));
            Assert.That(cut.Markup, Does.Contain("Offer"));
            Assert.That(cut.Markup, Does.Not.Contain("Hidden"));
        });

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(clickedAction, Is.SameAs(offeredAction));
    }

    [Test]
    public async Task WorkflowTaskDetailsFooter_RendersPromotionAndCancelControls()
    {
        WfStateAction offeredAction = new() { ButtonText = "Offer" };
        bool promotionRequested = false;
        bool closeRequested = false;

        await using BunitContext context = CreateContext();
        IRenderedComponent<WorkflowTaskDetailsFooter> cut = context.Render<WorkflowTaskDetailsFooter>(parameters => parameters
            .Add(parameter => parameter.OfferedActions, new List<WfStateAction> { offeredAction })
            .Add(parameter => parameter.CanPromoteTicket, true)
            .Add(parameter => parameter.PromoteTicket, () =>
            {
                promotionRequested = true;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.ShowCancel, true)
            .Add(parameter => parameter.Close, () =>
            {
                closeRequested = true;
                return Task.CompletedTask;
            }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("button.btn-dark").TextContent, Does.Contain("Offer"));
            Assert.That(cut.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
            Assert.That(cut.Find("button.workflow-task-details-close").ClassList, Does.Contain("btn-secondary"));
        });

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find("button.workflow-task-details-close").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(promotionRequested, Is.True);
            Assert.That(closeRequested, Is.True);
        });
    }

    [Test]
    public async Task WorkflowTaskDetailsFooter_RendersOkButtonAndCustomCloseClass()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<WorkflowTaskDetailsFooter> cut = context.Render<WorkflowTaskDetailsFooter>(parameters => parameters
            .Add(parameter => parameter.ShowCancel, false)
            .Add(parameter => parameter.CloseButtonClass, "custom-close"));

        IElement closeButton = cut.Find("button.custom-close");

        Assert.Multiple(() =>
        {
            Assert.That(closeButton.ClassList, Does.Contain("btn-primary"));
            Assert.That(closeButton.TextContent, Does.Not.Empty);
            Assert.That(cut.FindAll("button.btn-dark"), Is.Empty);
            Assert.That(cut.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void PlanningDetailsView_FiltersReadOnlyAndStateChangingActions()
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
    public async Task Collapse_StartsCollapsedAndUsesConfiguredHeaderAndContentClasses()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<Collapse> cut = context.Render<Collapse>(parameters => parameters
            .Add(parameter => parameter.StartToggled, true)
            .Add(parameter => parameter.HeaderClass, "bg-secondary-subtle text-dark")
            .Add(parameter => parameter.ContentClass, "p-1")
            .AddChildContent("implementation content"));

        IElement header = cut.Find("button");
        IElement content = cut.Find("div.collapse");

        Assert.Multiple(() =>
        {
            Assert.That(header.ClassList, Does.Contain("bg-secondary-subtle"));
            Assert.That(header.ClassList, Does.Contain("text-dark"));
            Assert.That(content.ClassList, Does.Contain("p-1"));
            Assert.That(content.GetAttribute("class"), Does.Contain("collapse"));
        });
    }

    [Test]
    public async Task Collapse_ToggleShowsConfiguredContent()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<Collapse> cut = context.Render<Collapse>(parameters => parameters
            .Add(parameter => parameter.StartToggled, true)
            .AddChildContent("implementation content"));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("collapse.show p-2"));
        Assert.That(cut.Markup, Does.Contain("implementation content"));
    }

    [Test]
    public async Task DeviceEditor_InitializesWithTheSelectedDevice()
    {
        Device selected = new() { Id = 2, Name = "Firewall B" };
        List<Device> devices =
        [
            new Device { Id = 1, Name = "Firewall A" },
            selected
        ];

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.SelectedDevice, selected)
            .Add(parameter => parameter.Devices, devices));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Firewall B"));
    }

    [Test]
    public async Task DeviceEditor_CancelClosesWithoutSaving()
    {
        Device? saved = null;
        bool? displayChanged = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Devices, new List<Device> { new() { Id = 1, Name = "Firewall A" } })
            .Add(parameter => parameter.Saved, device =>
            {
                saved = device;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.FindAll("button.btn-secondary").Single().ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.Null);
            Assert.That(displayChanged, Is.False);
        });
    }

    [Test]
    public async Task DeviceEditor_DoesNotRenderWhenHidden()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, false)
            .Add(parameter => parameter.Devices, new List<Device> { new() { Id = 1, Name = "Firewall A" } }));

        Assert.That(cut.Markup, Is.Empty);
    }

    [Test]
    public async Task DeviceEditor_AllowsSelectingAndSavingAnotherDevice()
    {
        Device first = new() { Id = 1, Name = "Firewall A" };
        Device second = new() { Id = 2, Name = "Firewall B" };
        Device? saved = null;
        bool? displayChanged = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Devices, new List<Device> { first, second })
            .Add(parameter => parameter.Saved, device =>
            {
                saved = device;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("input").Focus());
        IElement secondDeviceButton = cut.FindAll("button.dropdown-item")
            .Single(button => button.TextContent.Contains("Firewall B", StringComparison.Ordinal));
        await secondDeviceButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.SameAs(second));
            Assert.That(displayChanged, Is.False);
        });
    }

    [Test]
    public async Task DeviceEditor_SavesNullWhenNoDeviceIsSelected()
    {
        Device? saved = new Device { Id = 1 };

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Devices, new List<Device> { new() { Id = 1, Name = "Firewall A" } })
            .Add(parameter => parameter.Saved, device =>
            {
                saved = device;
                return Task.CompletedTask;
            }));

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(saved, Is.Null);
    }

    [Test]
    public async Task DeviceEditor_ReinitializesAfterClosingAndReopening()
    {
        Device first = new() { Id = 1, Name = "Firewall A" };
        Device second = new() { Id = 2, Name = "Firewall B" };
        bool? displayChanged = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.SelectedDevice, first)
            .Add(parameter => parameter.Devices, new List<Device> { first, second })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.FindAll("button.btn-secondary").Single().ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.That(displayChanged, Is.False);

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(DeviceEditor.Display)] = true,
                [nameof(DeviceEditor.SelectedDevice)] = second,
                [nameof(DeviceEditor.Devices)] = new List<Device> { first, second }
            })));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Firewall B"));
    }

    [Test]
    public async Task PlanningImplementationTaskCreationView_ClosesAndResetsActions()
    {
        bool? displayChanged = null;
        WfHandler handler = new();

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskCreationView> cut = context.Render<PlanningImplementationTaskCreationView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.FindAll("button.btn-secondary")[^1].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(displayChanged, Is.False);
    }

    [Test]
    public void PlanningImplementationTaskCreationView_TracksTheDisplayedTask()
    {
        WfImplTask implementationTask = new() { Id = 21, TaskType = WfTaskType.generic.ToString() };
        PlanningImplementationTaskCreationView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskCreationView.Display), true);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskCreationView.ImplementationTask), implementationTask);

        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskCreationView), "OnParametersSet")
            .Invoke(component, kNoArguments);

        List<WfImplTask> tasks = UiRequestWorkflowTest.GetMember<List<WfImplTask>>(component, "implementationTasks");
        Assert.That(tasks, Is.EqualTo(new List<WfImplTask> { implementationTask }));
    }

    [Test]
    public void PlanningImplementationTaskCreationView_HandlesDisplayAndParameterGuards()
    {
        WfImplTask first = new() { Id = 21, TaskType = WfTaskType.generic.ToString() };
        WfImplTask second = new() { Id = 22, TaskType = WfTaskType.generic.ToString() };
        PlanningImplementationTaskCreationView component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskCreationView.ImplementationTask), first);

        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskCreationView), "OnParametersSet").Invoke(component, kNoArguments);
        Assert.That(UiRequestWorkflowTest.GetMember<List<WfImplTask>>(component, "implementationTasks"), Is.Empty);

        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskCreationView.Display), true);
        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskCreationView), "OnParametersSet").Invoke(component, kNoArguments);
        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskCreationView), "OnParametersSet").Invoke(component, kNoArguments);
        Assert.That(UiRequestWorkflowTest.GetMember<List<WfImplTask>>(component, "implementationTasks"), Has.Count.EqualTo(1));

        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskCreationView.ImplementationTask), second);
        UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskCreationView), "OnParametersSet").Invoke(component, kNoArguments);
        Assert.That(UiRequestWorkflowTest.GetMember<List<WfImplTask>>(component, "implementationTasks").Single(), Is.SameAs(second));
    }

    [Test]
    public async Task PlanningImplementationTaskCreationView_SavesTaskAndRefreshesParent()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false) { TaskNumber = 1 };
        handler.SelectImplTask(implementationTask, ObjAction.add);
        bool? displayChanged = null;
        bool parentReset = false;

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskCreationView> cut = context.Render<PlanningImplementationTaskCreationView>(parameters => parameters
            .Add(parameter => parameter.Display, false)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.ResetParent, () =>
            {
                parentReset = true;
                return Task.CompletedTask;
            }));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "Save");

        Assert.Multiple(() =>
        {
            Assert.That(requestTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(displayChanged, Is.False);
            Assert.That(parentReset, Is.True);
        });
    }

    [Test]
    public void PlanningDetailsView_InitializesRequestDetailsCollapsedAndTogglesIndependently()
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
    public async Task PlanningDetailsView_PreparesImplementationCreationAndDeleteActions()
    {
        WfImplTask existing = new() { Id = 21, TaskNumber = 3, TaskType = WfTaskType.access.ToString() };
        WfReqTask requestTask = new()
        {
            Id = 11,
            TaskType = WfTaskType.access.ToString(),
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
        });

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OpenDeleteConfirmation", existing);
        Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);
    }

    [Test]
    public void PlanningDetailsView_HeaderUsesTicketMetadata()
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
    public async Task PlanningDetailsView_RendersRequestAndImplementationHeadersWithDetailsCollapsed()
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
    public async Task PlanningDetailsView_RendersAllRequestTaskBodyVariantsAfterExpansion()
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
    public async Task PlanningDetailsView_CloseInvokesBothParentCallbacks()
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
    public async Task PlanningDetailsView_ResetClearsImplementationActionsAndRefreshesParent()
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

    [Test]
    public void PlanningImplementationTaskDetails_SelectsOnlyRequestedObjectElement()
    {
        List<WfImplElement> elements =
        [
            new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "existing", ImplAction = RequestAction.unchanged.ToString() },
            new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "created", ImplAction = RequestAction.create.ToString() },
            new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "second-created", ImplAction = RequestAction.create.ToString() }
        ];
        WfImplTask task = new() { TaskType = WfTaskType.object_create.ToString(), ImplElements = elements };

        object[] elementArguments = [task, ElemFieldType.source];
        IEnumerable<WfImplElement> result = (IEnumerable<WfImplElement>)UiRequestWorkflowTest.GetPrivateMethod(
            typeof(PlanningImplementationTaskDetails), "ElementsForDisplay").Invoke(null, elementArguments)!;

        Assert.That(result.Select(element => element.Name), Is.EqualTo(new List<string> { "created" }));
    }

    [Test]
    public void PlanningImplementationTaskDetails_TruncatesGroupContent()
    {
        object[] groupArguments = [kGroupContents];
        string result = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "TruncatedGroupContent")
            .Invoke(null, groupArguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.StartWith("one, two, three, four, five"));
            Assert.That(result, Does.Contain("(+1)"));
        });
    }

    [Test]
    public void PlanningImplementationTaskDetails_ResolvesRuleActionAndTrackingIds()
    {
        PlanningImplementationTaskDetails component = new();
        UiRequestWorkflowTest.SetMember(component, "ruleActions", new List<RuleAction> { new() { Id = 4, Name = "allow" } });
        UiRequestWorkflowTest.SetMember(component, "trackings", new List<Tracking> { new() { Id = 9, Name = "log" } });
        WfImplTask task = new() { RuleAction = 4, Tracking = 9 };

        object[] taskArguments = [task];
        string action = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "RuleActionName")
            .Invoke(component, taskArguments)!;
        string tracking = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "TrackingName")
            .Invoke(component, taskArguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(action, Is.EqualTo("allow"));
            Assert.That(tracking, Is.EqualTo("log"));
        });
    }

    [Test]
    public void PlanningImplementationTaskDetails_UsesImplementationDeviceInTitle()
    {
        PlanningImplementationTaskDetails component = new();
        SimulatedUserConfig userConfig = new();
        WfHandler handler = new()
        {
            Devices = [new Device { Id = 4, Name = "Firewall B" }]
        };
        UiRequestWorkflowTest.SetMember(component, "userConfig", userConfig);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.WfHandler), handler);
        WfImplTask task = new() { TaskType = WfTaskType.access.ToString(), DeviceId = 4 };

        object[] taskArguments = [task];
        string title = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "ImplementationTaskTitle")
            .Invoke(component, taskArguments)!;

        Assert.That(title, Is.EqualTo("Firewall B"));
    }

    [Test]
    public void PlanningImplementationTaskDetails_CoversTaskTypeAndDisplayHelpers()
    {
        PlanningImplementationTaskDetails component = new();
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());
        WfReqTask requestTask = new() { TaskType = WfTaskType.group_create.ToString() };
        requestTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.RequestTask), requestTask);

        WfImplTask groupTask = new()
        {
            TaskType = WfTaskType.group_create.ToString(),
            FreeText = "group text",
            ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "fallback-group" }]
        };
        WfImplTask ruleDeleteTask = new()
        {
            TaskType = WfTaskType.rule_delete.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-1" }]
        };
        WfImplTask genericTask = new() { TaskType = WfTaskType.generic.ToString(), FreeText = "text" };
        WfImplTask timeTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            TargetBeginDate = new DateTime(2027, 12, 31),
            TargetEndDate = new DateTime(2028, 1, 1),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
        };

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "IsRuleImplementationTask", ruleDeleteTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "IsRuleTask", new WfImplTask { TaskType = WfTaskType.rule_modify.ToString() }), Is.True);
            Assert.That(InvokeImplementationTaskBool(component, "IsGroupTask", groupTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "HasFreeText", genericTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditFreeText", new WfImplTask { TaskType = WfTaskType.access.ToString() }), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditFreeText", new WfImplTask { TaskType = WfTaskType.object_create.ToString() }), Is.False);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "IsTimeObjectTask", timeTask), Is.True);
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "GroupName")
                .Invoke(component, [groupTask]), Is.EqualTo("web-group"));
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "RuleUid")
                .Invoke(null, [ruleDeleteTask]), Is.EqualTo("rule-1"));
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "ObjectName")
                .Invoke(component, [timeTask]), Is.EqualTo("maintenance"));
        });
    }

    [Test]
    public async Task PlanningImplementationTaskDetails_RendersGenericTaskAndPlannerHeaderStyle()
    {
        WfImplTask task = new()
        {
            Id = 21,
            TaskType = WfTaskType.generic.ToString(),
            FreeText = "implementation details"
        };

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, new List<WfImplTask> { task })
            .Add(parameter => parameter.RequestTask, new WfReqTask { TaskType = WfTaskType.generic.ToString() })
            .Add(parameter => parameter.WfHandler, new WfHandler()));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("implementation details"));
            Assert.That(cut.Markup, Does.Contain("bg-secondary-subtle"));
            Assert.That(cut.Markup, Does.Contain("free_text"));
        });
    }

    private static bool InvokeImplementationTaskBool(PlanningImplementationTaskDetails component, string methodName, WfImplTask task)
    {
        MethodInfo method = typeof(PlanningImplementationTaskDetails).GetMethods(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Single(candidate => candidate.Name == methodName
                && candidate.GetParameters().Length == 1
                && candidate.GetParameters()[0].ParameterType == typeof(WfImplTask));
        return (bool)method.Invoke(component, new object[] { task })!;
    }

    [Test]
    public async Task PlanningImplementationTaskDetails_RendersRuleGroupObjectAndTimeBranches()
    {
        List<WfImplTask> tasks =
        [
            new WfImplTask
            {
                Id = 1,
                TaskType = WfTaskType.access.ToString(),
                RuleAction = 7,
                Tracking = 8,
                ImplElements =
                [
                    new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "source", IpString = "10.0.0.1" },
                    new WfImplElement { Field = ElemFieldType.destination.ToString(), Name = "destination", IpString = "10.0.0.2" },
                    new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
                ]
            },
            new WfImplTask
            {
                Id = 2,
                TaskType = WfTaskType.rule_delete.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-2" }]
            },
            new WfImplTask
            {
                Id = 3,
                TaskType = WfTaskType.group_create.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "web-group", Name = "member" }]
            },
            new WfImplTask
            {
                Id = 4,
                TaskType = WfTaskType.object_create.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "network-object", IpString = "10.0.0.3" }]
            },
            new WfImplTask
            {
                Id = 5,
                TaskType = WfTaskType.object_create.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
            }
        ];

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, tasks)
            .Add(parameter => parameter.RequestTask, new WfReqTask { TaskType = WfTaskType.access.ToString() })
            .Add(parameter => parameter.WfHandler, new WfHandler { ActTicket = new WfTicket() }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("source"));
            Assert.That(cut.Markup, Does.Contain("destination"));
            Assert.That(cut.Markup, Does.Contain("https"));
            Assert.That(cut.Markup, Does.Contain("rule-2"));
            Assert.That(cut.Markup, Does.Contain("web-group"));
            Assert.That(cut.Markup, Does.Contain("network-object"));
            Assert.That(cut.Markup, Does.Contain("maintenance"));
        });
    }

    [Test]
    public async Task PlanningImplementationTaskDetails_SavesEditableFieldsInCreationMode()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false) { Id = 21, TaskNumber = 1 };
        handler.SelectImplTask(implementationTask, ObjAction.add);
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A" };
        List<WfImplTask> implementationTasks = [implementationTask];

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, implementationTasks)
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveFreeText", "planner text");
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveManagement", management);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveDevice", device);

        WfImplTask savedTask = implementationTasks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(savedTask.FreeText, Is.EqualTo("planner text"));
            Assert.That(savedTask.ManagementId, Is.EqualTo(4));
            Assert.That(savedTask.DeviceId, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task PlanningImplementationTaskDetails_OpensManagementFreeTextAndDeviceEditors()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A" };
        handler.Managements.Add(management);
        handler.Devices.Add(device);
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false)
        {
            Id = 21,
            TaskNumber = 1,
            ManagementId = management.Id,
            DeviceId = device.Id,
            FreeText = "existing text"
        };
        handler.SelectImplTask(implementationTask, ObjAction.edit);

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, new List<WfImplTask> { implementationTask })
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenManagementEditor", implementationTask);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenFreeTextEditor", implementationTask);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenDeviceEditor", implementationTask);

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayManagementEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<Management>(cut.Instance, "selectedManagement"), Is.SameAs(management));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayTextEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<string>(cut.Instance, "textEditorValue"), Is.EqualTo("existing text"));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayDeviceEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<Device>(cut.Instance, "selectedDevice"), Is.SameAs(device));
        });
    }

    [Test]
    public async Task PlanningImplementationTaskDetails_SavesGroupAndTimeObjectNames()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.group_create.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask groupTask = new(requestTask, false)
        {
            TaskNumber = 1,
            TaskType = WfTaskType.group_create.ToString(),
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "old-group" },
                new WfImplElement { Field = ElemFieldType.service.ToString(), GroupName = "old-group" }
            ]
        };
        List<WfImplTask> groupTasks = [groupTask];
        handler.SelectImplTask(groupTask, ObjAction.edit);

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, groupTasks)
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveName", "new-group");
        Assert.That(groupTasks.Single().ImplElements.All(element => element.GroupName == "new-group"), Is.True);

        WfImplTask timeTask = new(requestTask, false)
        {
            TaskNumber = 2,
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "old-time" }]
        };
        List<WfImplTask> timeTasks = [timeTask];
        UiRequestWorkflowTest.SetMatrix(handler, timeTask.TaskType, new StateMatrix
        {
            LowestInputState = 0,
            LowestStartedState = 2,
            LowestEndState = 10
        });
        handler.SelectImplTask(timeTask, ObjAction.edit);
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(PlanningImplementationTaskDetails.ImplementationTasks)] = timeTasks,
                [nameof(PlanningImplementationTaskDetails.RequestTask)] = requestTask,
                [nameof(PlanningImplementationTaskDetails.WfHandler)] = handler,
                [nameof(PlanningImplementationTaskDetails.CreationMode)] = true
            })));
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveName", "new-time");

        Assert.That(timeTasks.Single().ImplElements.Single().Name, Is.EqualTo("new-time"));
    }

    [Test]
    public void PlanningImplementationTaskDetails_ResolvesServiceRowsAndManagementFallbacks()
    {
        PlanningImplementationTaskDetails component = new();
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A", Management = management };
        WfReqTask requestTask = new() { ManagementId = 3 };
        WfHandler handler = new() { Devices = [device] };
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.RequestTask), requestTask);
        UiRequestWorkflowTest.SetMember(component, "ipProtocols", new List<IpProtocol> { new() { Id = 6, Name = "tcp" } });

        WfImplTask serviceTask = new()
        {
            ImplElements = [new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }]
        };
        object[] serviceArguments = [serviceTask];
        List<(string Name, string Content, string Action)> rows = (List<(string Name, string Content, string Action)>)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "GroupServiceDisplayRows")
            .Invoke(component, serviceArguments)!;
        object explicitManagement = UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "EffectiveManagementId")
            .Invoke(component, [new WfImplTask { ManagementId = 9 }])!;
        object deviceManagement = UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "EffectiveManagementId")
            .Invoke(component, [new WfImplTask { DeviceId = 8 }])!;
        object requestManagement = UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "EffectiveManagementId")
            .Invoke(component, [new WfImplTask()])!;

        Assert.Multiple(() =>
        {
            Assert.That(rows.Single().Name, Is.EqualTo("https"));
            Assert.That(rows.Single().Content, Does.Contain("443"));
            Assert.That(explicitManagement, Is.EqualTo(9));
            Assert.That(deviceManagement, Is.EqualTo(4));
            Assert.That(requestManagement, Is.EqualTo(3));
        });
    }

    [Test]
    public void PlanningImplementationTaskDetails_ConvertsServiceElements()
    {
        WfImplTask task = new()
        {
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "source" },
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "source-2" },
                new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
            ]
        };

        WfImplElement serviceElement = task.ImplElements.Single(element => element.Field == ElemFieldType.service.ToString());
        NwServiceElement converted = (NwServiceElement)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "ToServiceElement")
            .Invoke(null, [serviceElement])!;
        Assert.Multiple(() =>
        {
            Assert.That(converted.Name, Is.EqualTo("https"));
            Assert.That(converted.Port, Is.EqualTo(443));
        });
    }

    [Test]
    public void ApprovalTaskDetailsEditorService_AllowsOnlyOwnerChangesForPlannerInReadOnlyMode()
    {
        WfReqTask task = new() { Id = 11, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, task.TaskType, new WfTicket { Tasks = [task] });
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(handler, new SimulatedUserConfig(), DefaultInit.DoNothing);
        service.ReadOnlyDisplay = true;
        service.PlannerOwnerChangesAllowed = true;
        ApprovalTaskViewContext viewContext = service.CreateTaskViewContext();

        List<WfReqTask> tasks = [task];
        Assert.Multiple(() =>
        {
            Assert.That(service.CanEdit(tasks, WorkflowEditableFieldKeys.Owner), Is.True);
            Assert.That(service.CanEdit(tasks, WorkflowEditableFieldKeys.Title), Is.False);
            Assert.That(service.CanEdit(tasks, WorkflowEditableFieldKeys.Source), Is.False);
            Assert.That(viewContext.CanEditTimeObject(tasks), Is.False);
        });
    }

    [Test]
    public void ApprovalTaskDetailsEditorService_DoesNotAllowPlannerOwnerChangesOutsidePlanning()
    {
        WfReqTask task = new() { Id = 11, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.approval, task.TaskType, new WfTicket { Tasks = [task] });
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(handler, new SimulatedUserConfig(), DefaultInit.DoNothing);
        service.ReadOnlyDisplay = true;
        service.PlannerOwnerChangesAllowed = true;

        Assert.That(service.CanEdit(new List<WfReqTask> { task }, WorkflowEditableFieldKeys.Owner), Is.False);
    }
}

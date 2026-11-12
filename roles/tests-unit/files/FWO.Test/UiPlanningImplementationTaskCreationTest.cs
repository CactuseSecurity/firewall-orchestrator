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
internal sealed class UiPlanningImplementationTaskCreationTest
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
    public static async Task PlanningImplementationTaskCreationView_ClosesAndResetsActions()
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
    public static void PlanningImplementationTaskCreationView_TracksTheDisplayedTask()
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
    public static void PlanningImplementationTaskCreationView_HandlesDisplayAndParameterGuards()
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
    public static async Task PlanningImplementationTaskCreationView_SavesTaskAndRefreshesParent()
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
    public static async Task PlanningImplementationTaskCreationView_IgnoresConcurrentSave()
    {
        string taskType = WfTaskType.access.ToString();
        WfReqTask requestTask = new() { Id = 11, TicketId = 7, TaskType = taskType, StateId = 1 };
        WfTicket ticket = new() { Id = 7, Tasks = [requestTask] };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, taskType, ticket);
        WfImplTask implementationTask = new(requestTask, false) { TaskNumber = 1 };
        handler.SelectReqTask(requestTask, ObjAction.display);
        handler.SelectImplTask(implementationTask, ObjAction.add);

        TaskCompletionSource insertStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseInsert = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ImplTaskCreationApiConnection apiConnection = new(ticket)
        {
            InsertStarted = insertStarted,
            ReleaseInsert = releaseInsert
        };
        SetImplTaskCreationDbAccess(handler, apiConnection);

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskCreationView> cut = context.Render<PlanningImplementationTaskCreationView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.ImplementationTask, implementationTask)
            .Add(parameter => parameter.WfHandler, handler));

        Task firstSave = await UiRequestWorkflowTest.StartPrivateTask(cut, "Save");
        await insertStarted.Task;
        Task secondSave = await UiRequestWorkflowTest.StartPrivateTask(cut, "Save");

        Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
        Assert.That(requestTask.ImplementationTasks, Is.Empty);
        Assert.That(cut.Find("button.btn-primary").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Markup, Does.Contain("spinner-border"));

        releaseInsert.SetResult();
        await Task.WhenAll(firstSave, secondSave);

        Assert.That(requestTask.ImplementationTasks, Has.Count.EqualTo(1));
        Assert.That(requestTask.ImplementationTasks.Single().StateId, Is.EqualTo(requestTask.StateId));
    }

    private static void SetImplTaskCreationDbAccess(WfHandler handler, SimulatedApiConnection apiConnection)
    {
        ActionHandler actionHandler = new(apiConnection, handler);
        WfDbAccess dbAccess = new(
            DefaultInit.DoNothing,
            new SimulatedUserConfig(),
            apiConnection,
            actionHandler,
            false,
            WorkflowPhases.planning);
        UiRequestWorkflowTest.SetMember(handler, "dbAcc", dbAccess);
    }

}

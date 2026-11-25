using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class UiPlanningTaskListViewTest
{
    private const string kAccessTable = ".planning-task-list[data-table=\"Access\"]";
    private static readonly List<string> kBundledSources = ["client-a", "client-b"];
    private static readonly List<string> kDisplayDataQueries = [StmQueries.getRuleActions, StmQueries.getTracking, StmQueries.getIpProtocols];

    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
        return context;
    }

    private static WfHandler CreateHandler(int stateId = 1)
    {
        string taskType = WfTaskType.access.ToString();
        Management management = new() { Id = 4, Name = "Management A" };
        WfImplTask implementationTask = new()
        {
            Id = 21,
            ReqTaskId = 11,
            TicketId = 7,
            Title = "impl access",
            StateId = stateId,
            TaskType = taskType,
            ManagementId = management.Id,
            ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "impl-client", IpString = "10.0.0.5" }]
        };
        WfReqTask accessTask = new()
        {
            Id = 11,
            Title = "list access",
            StateId = stateId,
            TaskType = taskType,
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-a", IpString = "10.0.0.1" }],
            ImplementationTasks = [implementationTask]
        };
        WfReqTask genericTask = new() { Id = 12, Title = "list generic", StateId = stateId, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, taskType,
            new WfTicket { Id = 7, Tasks = [accessTask, genericTask] });
        UiRequestWorkflowTest.SetMatrix(handler, WfTaskType.generic.ToString(), new StateMatrix { LowestInputState = 0, LowestStartedState = 2, LowestEndState = 10 });
        handler.Managements.Add(management);
        return handler;
    }

    private static PlanningImplTaskEditor Editor(IRenderedComponent<PlanningTaskListView> listView) =>
        UiRequestWorkflowTest.GetMember<PlanningImplTaskEditor>(listView.Instance, "Editor");

    private static IRenderedComponent<PlanningDetailsView> RenderListView(BunitContext context, WfHandler handler, Func<Task>? resetParent = null)
    {
        return context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ListView, true)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.States, new WfStateDict { Name = { [1] = "Planning" } })
            .Add(parameter => parameter.ResetParent, resetParent ?? (() => Task.CompletedTask)));
    }

    private static WfStateActionDataHelper OfferedAction(string buttonText, WfObjectScopes scope) => new()
    {
        Action = new WfStateAction
        {
            Event = StateActionEvents.OfferButton.ToString(),
            ActionType = StateActionTypes.DoNothing.ToString(),
            Scope = scope.ToString(),
            ButtonText = buttonText
        }
    };

    private static int ColumnCount(IRenderedComponent<PlanningDetailsView> cut, string key) =>
        cut.FindAll($"{kAccessTable} th[data-column-key=\"{key}\"]").Count;

    [Test]
    public async Task PlanningDetailsView_ListViewShowsRequestAndImplementationTaskRows()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, CreateHandler());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".card"), Is.Empty);
            Assert.That(cut.FindAll($"{kAccessTable} tbody tr"), Has.Count.EqualTo(2));
            Assert.That(cut.FindAll($"{kAccessTable} tbody tr.planning-task-list-impl-row"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll(".planning-task-list[data-table=\"Other\"] tbody tr"), Has.Count.EqualTo(1));
            Assert.That(cut.Find(kAccessTable).TextContent, Does.Contain("client-a"));
            Assert.That(cut.Find(kAccessTable).TextContent, Does.Contain("impl-client"));
            Assert.That(cut.Find(kAccessTable).TextContent, Does.Contain("Management A"));
            Assert.That(ColumnCount(cut, PlanningTaskListRowBuilder.kLevel), Is.EqualTo(1));
            Assert.That(ColumnCount(cut, ApprovalTaskListRowBuilder.kCompliance), Is.Zero);
            Assert.That(ColumnCount(cut, ApprovalTaskListRowBuilder.kSourceZone), Is.EqualTo(1));
            Assert.That(ColumnCount(cut, PlanningTaskListRowBuilder.kRequestTask), Is.Zero);
            Assert.That(cut.FindAll($"{kAccessTable} .task-list-header-buttons button.planning-task-list-mode-toggle"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll(".planning-task-list[data-table=\"Other\"] .task-list-header-buttons"), Is.Empty);
            Assert.That(cut.Find("button.planning-task-list-mode-toggle").InnerHtml, Does.Contain("planning_list_show_impl_tasks"));
        });
    }

    [Test]
    public async Task PlanningDetailsView_ListViewTogglesToImplementationTasksOnly()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, CreateHandler());

        cut.Find("button.planning-task-list-mode-toggle").Click();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll($"{kAccessTable} tbody tr"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll($"{kAccessTable} tbody tr.planning-task-list-impl-row"), Is.Empty);
            Assert.That(cut.Find(kAccessTable).TextContent, Does.Not.Contain("client-a"));
            Assert.That(ColumnCount(cut, PlanningTaskListRowBuilder.kRequestTask), Is.EqualTo(1));
            Assert.That(ColumnCount(cut, PlanningTaskListRowBuilder.kLevel), Is.Zero);
            Assert.That(ColumnCount(cut, ApprovalTaskListRowBuilder.kSourceZone), Is.Zero);
            Assert.That(cut.FindAll(".planning-task-list[data-table=\"Other\"]"), Has.Count.EqualTo(1));
            Assert.That(cut.Find("button.planning-task-list-mode-toggle").InnerHtml, Does.Contain("planning_list_show_all_tasks"));
        });

        cut.Find("button.planning-task-list-mode-toggle").Click();

        Assert.That(cut.FindAll($"{kAccessTable} tbody tr"), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PlanningDetailsView_ListViewOffersAddAndDeleteOfImplementationTasks()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = CreateHandler();
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler);

        Assert.That(ColumnCount(cut, "actions"), Is.EqualTo(1));
        cut.Find($"{kAccessTable} button.planning-task-list-add").Click();
        Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayImplementationTaskCreation"), Is.True);

        cut.Find($"{kAccessTable} button.planning-task-list-delete").Click();
        Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);
    }

    [Test]
    public async Task PlanningDetailsView_ListViewHidesActionsOutsideActionableStates()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, CreateHandler(stateId: 10));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("button.planning-task-list-add"), Is.Empty);
            Assert.That(cut.FindAll("button.planning-task-list-delete"), Is.Empty);
            Assert.That(cut.FindAll("button.task-list-edit"), Is.Empty);
        });
    }

    [Test]
    public async Task PlanningDetailsView_ListViewOpensImplementationTaskEditors()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = CreateHandler();
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler);
        IRenderedComponent<PlanningTaskListView> listView = cut.FindComponent<PlanningTaskListView>();

        cut.Find($"{kAccessTable} tr.planning-task-list-impl-row button.task-list-edit").Click();

        PlanningImplTaskEditor editor = Editor(listView);
        Assert.Multiple(() =>
        {
            Assert.That(editor.DisplayManagementEditor, Is.True);
            Assert.That(editor.SelectedManagement?.Id, Is.EqualTo(4));
            Assert.That(handler.ActImplTask.Id, Is.EqualTo(21));
            // the cell click must render the view hosting the editors, otherwise the popup would stay hidden
            Assert.That(cut.FindComponent<ManagementEditor>().Instance.Display, Is.True);
        });

        WfReqTask requestTask = handler.ActTicket.Tasks[0];
        WfImplTask implementationTask = requestTask.ImplementationTasks[0];
        await listView.InvokeAsync(() => editor.OpenFreeText(implementationTask, requestTask));
        await listView.InvokeAsync(() => editor.OpenDevice(implementationTask, requestTask));
        await listView.InvokeAsync(() => editor.OpenName(implementationTask, requestTask));

        Assert.Multiple(() =>
        {
            Assert.That(editor.DisplayFreeTextEditor, Is.True);
            Assert.That(editor.DisplayDeviceEditor, Is.True);
            Assert.That(editor.DisplayNameEditor, Is.True);
            Assert.That(editor.RequestTask, Is.SameAs(requestTask));
        });
    }

    [Test]
    public async Task PlanningDetailsView_ListViewDoesNotEditImplementationTaskOutsideActionableStates()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = CreateHandler(stateId: 10);
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler);
        IRenderedComponent<PlanningTaskListView> listView = cut.FindComponent<PlanningTaskListView>();
        PlanningImplTaskEditor editor = Editor(listView);
        WfReqTask requestTask = handler.ActTicket.Tasks[0];
        WfImplTask implementationTask = requestTask.ImplementationTasks[0];

        await listView.InvokeAsync(() => editor.OpenFreeText(implementationTask, requestTask));
        await listView.InvokeAsync(() => editor.OpenManagement(implementationTask, requestTask));
        await listView.InvokeAsync(() => editor.SaveFreeText("changed"));
        await listView.InvokeAsync(() => editor.SaveManagement(null));

        Assert.Multiple(() =>
        {
            Assert.That(editor.DisplayFreeTextEditor, Is.False);
            Assert.That(editor.DisplayManagementEditor, Is.False);
            Assert.That(implementationTask.FreeText, Is.Null);
            Assert.That(implementationTask.ManagementId, Is.EqualTo(4));
            Assert.That(handler.ActImplTask.Id, Is.Not.EqualTo(implementationTask.Id));
        });
    }

    [Test]
    public async Task PlanningDetailsView_ListViewShowsBundleAsOneRowAndCreatesFromMergedBundle()
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.ReqConsiderBundling = true;
        userConfig.ReqDisplayBundledTasksAsOne = true;
        WfHandler handler = CreateHandler();
        WfReqTask first = handler.ActTicket.Tasks[0];
        WfReqTask second = new()
        {
            Id = 13,
            Title = "bundled access",
            StateId = first.StateId,
            TaskType = first.TaskType,
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-b", IpString = "10.0.0.2" }]
        };
        first.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1");
        second.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1");
        handler.ActTicket.Tasks.Add(second);

        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll($"{kAccessTable} tbody tr"), Has.Count.EqualTo(2));
            Assert.That(cut.FindAll($"{kAccessTable} tbody tr.planning-task-list-impl-row"), Has.Count.EqualTo(1));
            Assert.That(cut.Find($"{kAccessTable} tbody tr").TextContent, Does.Contain("11, 13"));
            Assert.That(cut.FindAll($"{kAccessTable} button.planning-task-list-add"), Has.Count.EqualTo(1));
        });

        cut.Find($"{kAccessTable} button.planning-task-list-add").Click();

        WfReqTask creationRequestTask = UiRequestWorkflowTest.GetMember<WfReqTask>(cut.Instance, "creationRequestTask");
        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayImplementationTaskCreation"), Is.True);
            Assert.That(creationRequestTask.Id, Is.EqualTo(first.Id));
            Assert.That(creationRequestTask.Elements.Select(element => element.Name), Is.EquivalentTo(kBundledSources));
            Assert.That(handler.ActImplTask.ReqTaskId, Is.EqualTo(first.Id));
            Assert.That(handler.ActImplTask.ImplElements, Has.Count.EqualTo(2));
            // the owning request task is selected, not the transient merged copy
            Assert.That(handler.ActReqTask.Id, Is.EqualTo(first.Id));
            Assert.That(handler.ActReqTask.Elements, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task PlanningDetailsView_ListViewLoadsDisplayDataAndPerformsConfiguredActions()
    {
        const string requestActionText = "Request action";
        const string implementationActionText = "Implementation action";
        await using BunitContext context = CreateContext();
        UiRequestWorkflowTest.RequestWorkflowApiConn apiConnection = new();
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        WfHandler handler = CreateHandler();
        handler.ActionHandler = new ActionHandler(apiConnection, handler);
        await handler.ActionHandler.Init(
        [
            new WfState
            {
                Id = 1,
                Actions =
                [
                    OfferedAction(requestActionText, WfObjectScopes.RequestTask),
                    OfferedAction(implementationActionText, WfObjectScopes.ImplementationTask)
                ]
            }
        ]);
        int resetCount = 0;
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler, () =>
        {
            resetCount++;
            return Task.CompletedTask;
        });

        Assert.That(apiConnection.Queries, Is.SupersetOf(kDisplayDataQueries));

        await cut.Find($"{kAccessTable} tbody tr:not(.planning-task-list-impl-row)").QuerySelectorAll("button")
            .First(button => button.TextContent.Trim() == requestActionText).ClickAsync(new());
        Assert.That(resetCount, Is.EqualTo(1));

        await cut.Find($"{kAccessTable} tr.planning-task-list-impl-row").QuerySelectorAll("button")
            .First(button => button.TextContent.Trim() == implementationActionText).ClickAsync(new());
        Assert.That(resetCount, Is.EqualTo(2));
    }

    [Test]
    public async Task PlanningDetailsView_ListViewSavesEditedImplementationTask()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = CreateHandler();
        int resetCount = 0;
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler, () =>
        {
            resetCount++;
            return Task.CompletedTask;
        });
        IRenderedComponent<PlanningTaskListView> listView = cut.FindComponent<PlanningTaskListView>();
        PlanningImplTaskEditor editor = Editor(listView);
        WfReqTask requestTask = handler.ActTicket.Tasks[0];

        await listView.InvokeAsync(() => editor.OpenFreeText(requestTask.ImplementationTasks[0], requestTask));
        await listView.InvokeAsync(() => editor.SaveFreeText("changed"));

        Assert.Multiple(() =>
        {
            // the edited copy replaces the implementation task in the ticket
            Assert.That(handler.ActTicket.Tasks[0].ImplementationTasks[0].FreeText, Is.EqualTo("changed"));
            Assert.That(resetCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task PlanningDetailsView_IgnoresCreationAndDeletionOutsideActionableStates()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = CreateHandler(stateId: 10);
        IRenderedComponent<PlanningDetailsView> cut = RenderListView(context, handler);
        WfReqTask requestTask = handler.ActTicket.Tasks[0];

        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenImplementationTaskCreation", requestTask));
        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenDeleteConfirmation", requestTask.ImplementationTasks[0]));

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayImplementationTaskCreation"), Is.False);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.False);
        });
    }

    [Test]
    public void DisplayTicket_UsesPlanningDetailsViewForListView()
    {
        UiRequestWorkflowTest.RequestWorkflowUserConfig userConfig = new() { ReqPlannerEditMode = PlannerEditMode.ListView };
        WfHandler handler = new() { ActTicket = new WfTicket { Editable = true }, EditTicketMode = true };
        DisplayTicket component = UiRequestWorkflowTest.CreateDisplayTicket(handler, WorkflowPhases.planning, userConfig: userConfig);

        Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "UsePlanningDetailsView"), Is.True);
    }
}

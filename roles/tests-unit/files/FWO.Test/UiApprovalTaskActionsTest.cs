using AngleSharp.Dom;
using Bunit;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Configured actions of the request task scope in the access task view (card header) and the list view (actions column).
/// </summary>
[TestFixture]
internal class UiApprovalTaskActionsTest : UiSharedEditorTestBase
{
    private const int kStateId = 1;
    private const string kReadonlyActionText = "Show connection";
    private const string kChangingActionText = "Change something";
    private const string kBundleActionText = "Bundle tasks";
    private const string kImplementationActionText = "Show implementation path";
    private static readonly string[] kLeadingColumns = ["id", "actions"];

    private static WfStateActionDataHelper Action(StateActionTypes actionType, string buttonText,
        WfObjectScopes scope = WfObjectScopes.RequestTask) => new()
        {
            Action = new WfStateAction
            {
                Event = StateActionEvents.OfferButton.ToString(),
                ActionType = actionType.ToString(),
                Scope = scope.ToString(),
                ButtonText = buttonText
            }
        };

    private static async Task<WfHandler> CreateHandler(bool readOnly, params WfReqTask[] tasks)
    {
        WfHandler handler = new()
        {
            ReadOnlyMode = readOnly,
            Phase = WorkflowPhases.approval,
            // only ticket promotion, so the actions column of the list view depends on the task actions alone
            MasterStateMatrix = new StateMatrix { PromotionMode = PromotionMode.Ticket },
            ActTicket = new WfTicket { Id = 1, Tasks = [.. tasks] }
        };
        handler.ActionHandler = new ActionHandler(new UiRequestWorkflowTest.RequestWorkflowApiConn(), handler);
        await handler.ActionHandler.Init(
        [
            new WfState
            {
                Id = kStateId,
                Actions =
                [
                    Action(StateActionTypes.DisplayConnection, kReadonlyActionText),
                    Action(StateActionTypes.DoNothing, kChangingActionText),
                    Action(StateActionTypes.BundleTasks, kBundleActionText),
                    Action(StateActionTypes.TrafficPathAnalysis, kImplementationActionText, WfObjectScopes.ImplementationTask),
                    Action(StateActionTypes.BundleTasks, kBundleActionText, WfObjectScopes.ImplementationTask)
                ]
            }
        ]);
        return handler;
    }

    private static WfReqTask Task(long id, WfTaskType taskType) => new() { Id = id, StateId = kStateId, TicketId = 1, TaskType = taskType.ToString(), Title = $"task {id}" };

    private static IRenderedComponent<ApprovalTaskDetailsView> RenderView(BunitContext context, WfHandler handler, bool listView)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ListView, listView)
            .Add(parameter => parameter.WfHandler, handler));
    }

    private static List<string> ButtonTexts(IElement element)
    {
        return [.. element.QuerySelectorAll("button").Select(button => button.TextContent.Trim())];
    }

    [Test]
    public async Task AccessTaskView_ShowsOfferedTaskActionsInEveryTaskHeader()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = await CreateHandler(true, Task(1, WfTaskType.access), Task(2, WfTaskType.generic));

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, handler, false);

        List<IElement> cards = [.. cut.FindAll("div.card")];
        Assert.Multiple(() =>
        {
            Assert.That(cards, Has.Count.EqualTo(2));
            foreach (IElement card in cards)
            {
                List<string> headerButtons = ButtonTexts(card.Children[0]);
                Assert.That(headerButtons, Does.Contain(kReadonlyActionText));
                Assert.That(headerButtons, Does.Not.Contain(kChangingActionText), "state changing actions need write access");
                Assert.That(headerButtons, Does.Not.Contain(kBundleActionText), "bundling is only offered on ticket level");
            }
        });
    }

    [Test]
    public async Task ListView_ShowsActionsColumnWithTaskActionsWithoutTaskPromotion()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = await CreateHandler(true, Task(1, WfTaskType.access));

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, handler, true);

        IElement row = cut.Find(".approval-task-list[data-table=\"Access\"] tbody tr");
        List<string> rowButtons = ButtonTexts(row);
        List<string> columnKeys = [.. cut.FindAll(".approval-task-list[data-table=\"Access\"] col")
            .Select(column => column.GetAttribute("data-column-key") ?? "")];
        Assert.Multiple(() =>
        {
            Assert.That(columnKeys.Take(2), Is.EqualTo(kLeadingColumns));
            Assert.That(rowButtons, Does.Contain(kReadonlyActionText));
            Assert.That(rowButtons, Does.Not.Contain(kBundleActionText));
            Assert.That(rowButtons, Does.Not.Contain("promote"));
        });
    }

    [Test]
    public async Task ListView_HasNoActionsColumnWithoutTaskActionsOrPromotion()
    {
        await using BunitContext context = CreateContext();
        WfHandler handler = await CreateHandler(true, Task(1, WfTaskType.access));
        handler.ActionHandler = null;

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, handler, true);

        Assert.That(cut.Find(".approval-task-list[data-table=\"Access\"]").OuterHtml, Does.Not.Contain("data-column-key=\"actions\""));
    }

    [Test]
    public async Task PerformTaskAction_RunsForEveryTaskOfTheGroupAndReleasesTheView()
    {
        await using BunitContext context = CreateContext();
        WfReqTask first = Task(1, WfTaskType.access);
        WfReqTask second = Task(2, WfTaskType.access);
        WfHandler handler = await CreateHandler(true, first, second);
        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, handler, false);
        WfStateAction action = new() { ActionType = StateActionTypes.DoNothing.ToString(), Scope = WfObjectScopes.RequestTask.ToString() };

        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "PerformTaskAction",
            (IReadOnlyList<WfReqTask>)[first, second], action));

        Assert.That(cut.FindAll("div.card"), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PlanningView_ShowsTaskActionsInRequestAndImplementationTaskHeaders()
    {
        await using BunitContext context = CreateContext();
        context.Services.AddSingleton<DomEventService>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        WfReqTask requestTask = Task(1, WfTaskType.generic);
        requestTask.ImplementationTasks = [new WfImplTask { Id = 21, StateId = kStateId, TaskType = WfTaskType.generic.ToString() }];
        WfHandler handler = await CreateHandler(true, requestTask);
        handler.Phase = WorkflowPhases.planning;

        IRenderedComponent<PlanningDetailsView> cut = context.Render<PlanningDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler));

        List<IElement> cards = [.. cut.FindAll("div.card")];
        List<string> requestHeaderButtons = ButtonTexts(cards[0].Children[0]);
        List<string> implementationHeaderButtons = ButtonTexts(cards[^1].Children[0]);
        Assert.Multiple(() =>
        {
            Assert.That(cards, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(requestHeaderButtons, Does.Contain(kReadonlyActionText));
            Assert.That(requestHeaderButtons, Does.Not.Contain(kImplementationActionText));
            Assert.That(requestHeaderButtons, Does.Not.Contain(kChangingActionText));
            Assert.That(implementationHeaderButtons, Does.Contain(kImplementationActionText));
            Assert.That(implementationHeaderButtons, Does.Not.Contain(kReadonlyActionText));
            Assert.That(cut.Markup, Does.Not.Contain(kBundleActionText));
        });
    }
}

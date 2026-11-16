using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test;

[TestFixture]
internal class UiApprovalTaskListBodyTest : UiSharedEditorTestBase
{
    [Test]
    public async Task ApprovalTaskDetailsView_ListViewRendersOneRowPerTask()
    {
        await using BunitContext context = CreateListContext();
        WfHandler handler = CreateHandler();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);

        int sourceZonePosition = ColumnPosition(cut.Markup, "source_zone");
        int sourcePosition = ColumnPosition(cut.Markup, "source");
        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".card"), Is.Empty);
            Assert.That(cut.FindAll(".approval-task-list tbody tr"), Has.Count.EqualTo(2));
            Assert.That(cut.Markup, Does.Contain("list access"));
            Assert.That(cut.Markup, Does.Contain("list generic"));
            Assert.That(cut.Markup, Does.Contain("client-a"));
            Assert.That(sourceZonePosition, Is.GreaterThanOrEqualTo(0));
            Assert.That(sourceZonePosition, Is.LessThan(sourcePosition));
            Assert.That(cut.FindAll("button.approval-task-display-toggle"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll(".approval-task-list[data-table=\"Access\"]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll(".approval-task-list[data-table=\"Other\"]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll(".approval-task-list[data-table=\"Address\"]"), Is.Empty);
            Assert.That(cut.FindAll(".approval-task-list[data-table=\"Service\"]"), Is.Empty);
            Assert.That(cut.Find(".approval-task-list[data-table=\"Access\"] .fancy-table-topbar .approval-task-list-title").TextContent,
                Is.EqualTo("approval_list_access_tasks"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewOpensEditorOnClickOfEditableCell()
    {
        await using BunitContext context = CreateListContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        ApproverAllowedChangesConfig allowedChanges = new();
        allowedChanges.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Title, true);
        userConfig.ReqAllowedChangesByApprover = allowedChanges.ToConfigValue();
        WfHandler handler = CreateHandler();
        StateMatrixDict matrices = new();
        matrices.Matrices[WfTaskType.access.ToString()] = new StateMatrix { LowestInputState = 1, LowestEndState = 10 };
        matrices.Matrices[WfTaskType.generic.ToString()] = new StateMatrix { LowestInputState = 1, LowestEndState = 10 };
        SetMember(handler, "stateMatrixDict", matrices);

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);
        Assert.That(cut.FindAll("button.approval-task-list-edit"), Has.Count.EqualTo(1));

        cut.Find("button.approval-task-list-edit").Click();

        Assert.Multiple(() =>
        {
            Assert.That(GetMember<bool>(cut.Instance, "displayTextEditor"), Is.True);
            Assert.That(GetMember<string>(cut.Instance, "textEditorKey"), Is.EqualTo(WorkflowEditableFieldKeys.Title));
            Assert.That(GetMember<string>(cut.Instance, "textEditorValue"), Is.EqualTo("list access"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewRendersMemberBadgesAtValueEnd()
    {
        await using BunitContext context = CreateListContext();
        WfReqTask groupTask = new()
        {
            Id = 73,
            StateId = 2,
            Title = "modify group",
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "removed-member",
                    GroupName = "grp",
                    IpString = "10.0.0.5",
                    RequestAction = RequestAction.delete.ToString()
                }
            ]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 701, Tasks = [groupTask] } };

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);

        AngleSharp.Dom.IElement value = cut.Find(".approval-task-list[data-table=\"Address\"] .approval-task-list-badge").ParentElement!;
        Assert.Multiple(() =>
        {
            Assert.That(value.Children, Has.Length.EqualTo(2));
            Assert.That(value.Children[0].TextContent, Does.StartWith("removed-member"));
            Assert.That(value.Children[1].ClassList, Does.Contain("approval-task-list-badge").And.Contain("text-bg-danger"));
            Assert.That(value.Children[1].TextContent, Is.EqualTo("remove_member"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewOffersNoEditorsWithoutAllowedChanges()
    {
        await using BunitContext context = CreateListContext();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, CreateHandler());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("button.approval-task-list-edit"), Is.Empty);
            Assert.That(cut.FindAll("button.btn-warning"), Is.Empty);
        });
    }

    [TestCase(PromotionMode.Task, true)]
    [TestCase(PromotionMode.Both, true)]
    [TestCase(PromotionMode.Ticket, false)]
    [TestCase(PromotionMode.ActionButtonsOnly, false)]
    public async Task ApprovalTaskDetailsView_ListViewShowsActionsColumnOnlyForTaskPromotion(PromotionMode mode, bool expected)
    {
        await using BunitContext context = CreateListContext();
        WfHandler handler = CreateHandler();
        handler.Phase = WorkflowPhases.approval;
        handler.MasterStateMatrix = new StateMatrix { PromotionMode = mode };

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);

        bool actionsShown = ColumnPosition(cut.Markup, "actions") >= 0;
        Assert.That(actionsShown, Is.EqualTo(expected));
    }

    [Test]
    public void DisplayTicket_UsesApprovalTaskDetailsViewForListView()
    {
        RequestWorkflowUserConfig userConfig = new() { ReqApprovalEditMode = ApprovalEditMode.ListView };
        DisplayTicket component = CreateDisplayTicket(new WfHandler(), WorkflowPhases.approval, userConfig: userConfig);

        Assert.That(GetMember<bool>(component, "UseApprovalTaskDetailsView"), Is.True);
    }

    private static int ColumnPosition(string markup, string columnKey)
    {
        return markup.IndexOf($"data-column-key=\"{columnKey}\"", StringComparison.Ordinal);
    }

    private static BunitContext CreateListContext()
    {
        BunitContext context = CreateContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static IRenderedComponent<ApprovalTaskDetailsView> RenderListView(BunitContext context, WfHandler handler)
    {
        return context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.ListView, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));
    }

    private static WfHandler CreateHandler()
    {
        WfReqTask accessTask = new()
        {
            Id = 71,
            StateId = 2,
            Title = "list access",
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-a", IpString = "10.0.0.1" }]
        };
        WfReqTask genericTask = new()
        {
            Id = 72,
            StateId = 2,
            Title = "list generic",
            TaskType = WfTaskType.generic.ToString()
        };
        return new WfHandler { ActTicket = new WfTicket { Id = 700, Tasks = [accessTask, genericTask] } };
    }
}

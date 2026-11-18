using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static FWO.Test.ApprovalComplianceTestHelper;
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

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewShowsComplianceStatusWithReasons()
    {
        await using BunitContext context = CreateListContext();
        WfReqTask compliantTask = CheckableAccessTask(81, "allowed access");
        WfReqTask violatingTask = CheckableAccessTask(82, "forbidden access");
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 801, Tasks = [compliantTask, violatingTask] } };
        Dictionary<long, RequestTaskComplianceResult> results = new()
        {
            [81] = new(81, true, NoReasons),
            [82] = new(82, false, kViolationReasons)
        };
        await AttachPolicyCheck(handler, context.Services.GetRequiredService<ApiConnection>(), new FixedResultPolicyChecker(results));

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);

        cut.WaitForAssertion(() => Assert.That(cut.FindAll(".approval-task-list-compliance"), Has.Count.EqualTo(2)));
        AngleSharp.Dom.IElement ok = cut.Find("span.approval-task-list-compliance");
        AngleSharp.Dom.IElement notAllowed = cut.Find("button.approval-task-list-compliance");
        Assert.Multiple(() =>
        {
            Assert.That(ColumnPosition(cut.Markup, "id"), Is.LessThan(ColumnPosition(cut.Markup, "compliance")));
            Assert.That(ColumnPosition(cut.Markup, "compliance"), Is.LessThan(ColumnPosition(cut.Markup, "task_type")));
            Assert.That(ok.ClassList, Does.Contain("btn-success"));
            Assert.That(ok.TextContent.Trim(), Is.EqualTo("approval_compliance_ok"));
            Assert.That(notAllowed.ClassList, Does.Contain("btn-danger"));
            Assert.That(notAllowed.TextContent.Trim(), Is.EqualTo("approval_compliance_not_allowed"));
            Assert.That(notAllowed.GetAttribute("title"), Is.EqualTo("forbidden service\nzone violation"));
            Assert.That(cut.FindAll(".approval-compliance-reasons"), Is.Empty);
        });

        notAllowed.Click();

        Assert.That(cut.FindAll(".approval-compliance-reasons li").Select(item => item.TextContent), Is.EqualTo(kViolationReasons));
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewShowsLaterAddedComplianceColumnAfterId()
    {
        await using BunitContext context = CreateListContext();
        WfReqTask task = CheckableAccessTask(83, "later checked access");
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 802, Tasks = [task] } };
        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);
        Assert.That(ColumnPosition(cut.Markup, "compliance"), Is.EqualTo(-1));

        Dictionary<long, RequestTaskComplianceResult> results = new() { [83] = new(83, true, NoReasons) };
        await AttachPolicyCheck(handler, context.Services.GetRequiredService<ApiConnection>(), new FixedResultPolicyChecker(results));
        handler.ActTicket = new WfTicket { Id = 803, Tasks = [task] };
        cut.Render(parameters => parameters.Add(p => p.WfHandler, handler));

        cut.WaitForAssertion(() => Assert.That(cut.FindAll(".approval-task-list-compliance"), Has.Count.EqualTo(1)));
        Assert.Multiple(() =>
        {
            Assert.That(ColumnPosition(cut.Markup, "id"), Is.LessThan(ColumnPosition(cut.Markup, "compliance")));
            Assert.That(ColumnPosition(cut.Markup, "compliance"), Is.LessThan(ColumnPosition(cut.Markup, "task_type")));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_CardViewShowsComplianceAtEndOfAccessTaskHeader()
    {
        await using BunitContext context = CreateListContext();
        WfReqTask violatingTask = CheckableAccessTask(84, "forbidden access");
        WfReqTask genericTask = new() { Id = 85, StateId = 2, Title = "generic", TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 804, Tasks = [violatingTask, genericTask] } };
        Dictionary<long, RequestTaskComplianceResult> results = new() { [84] = new(84, false, kViolationReasons) };
        await AttachPolicyCheck(handler, context.Services.GetRequiredService<ApiConnection>(), new FixedResultPolicyChecker(results));

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("button.approval-compliance-button"), Has.Count.EqualTo(1)));
        AngleSharp.Dom.IElement button = cut.Find("button.approval-compliance-button");
        Assert.Multiple(() =>
        {
            Assert.That(button.ClassList, Does.Contain("btn-danger").And.Contain("btn-sm"));
            Assert.That(button.ParentElement!.LastElementChild!.ClassList, Does.Contain("approval-compliance-button"));
            Assert.That(button.ParentElement.TextContent, Does.Contain("forbidden access"));
        });

        button.Click();

        Assert.That(cut.FindAll(".approval-compliance-reasons li").Select(item => item.TextContent), Is.EqualTo(kViolationReasons));
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewHidesComplianceWithoutConditionalPolicyCheck()
    {
        await using BunitContext context = CreateListContext();
        WfHandler handler = CreateHandler();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);

        Assert.Multiple(() =>
        {
            Assert.That(ColumnPosition(cut.Markup, "compliance"), Is.EqualTo(-1));
            Assert.That(cut.FindAll(".approval-task-list-compliance"), Is.Empty);
            Assert.That(cut.FindAll(".approval-compliance-recheck"), Is.Empty);
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_ListViewRecheckChecksStoredOkAgainAndStoresTheChange()
    {
        await using BunitContext context = CreateListContext();
        WfReqTask task = CheckableAccessTask(86, "stored ok access");
        task.TicketId = 806;
        task.SetAddInfo(AdditionalInfoKeys.ComplianceOk, "true");
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 806, Tasks = [task] } };
        FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [86] = new(86, false, kViolationReasons) });
        await AttachPolicyCheck(handler, context.Services.GetRequiredService<ApiConnection>(), checker);

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderListView(context, handler);

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("span.approval-task-list-compliance.btn-success"), Has.Count.EqualTo(1)));
        Assert.That(checker.CheckCount, Is.Zero);

        cut.Find("button.approval-compliance-recheck").Click();

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("button.approval-task-list-compliance.btn-danger"), Has.Count.EqualTo(1)));
        Assert.Multiple(() =>
        {
            Assert.That(checker.CheckCount, Is.EqualTo(1));
            Assert.That(task.GetAddInfoValue(AdditionalInfoKeys.ComplianceOk), Is.EqualTo("false"));
        });
    }

    [Test]
    public async Task ApprovalTaskDetailsView_CardViewOffersRecheck()
    {
        await using BunitContext context = CreateListContext();
        WfReqTask task = CheckableAccessTask(87, "access");
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 807, Tasks = [task] } };
        FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [87] = new(87, true, NoReasons) });
        await AttachPolicyCheck(handler, context.Services.GetRequiredService<ApiConnection>(), checker);

        IRenderedComponent<ApprovalTaskDetailsView> cut = context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.WfHandler, handler)
            .Add(p => p.States, new WfStateDict { Name = { [2] = "Approval" } }));

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("button.approval-compliance-recheck"), Has.Count.EqualTo(1)));
        cut.Find("button.approval-compliance-recheck").Click();

        cut.WaitForAssertion(() => Assert.That(checker.CheckCount, Is.EqualTo(2)));
    }

    private static readonly List<string> kViolationReasons = ["forbidden service", "zone violation"];

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

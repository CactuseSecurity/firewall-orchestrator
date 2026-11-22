using AngleSharp.Dom;
using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Layout of the access task view: popup size, task order, collapsed tasks and the field column widths.
/// </summary>
[TestFixture]
internal class UiApprovalTaskAccessViewLayoutTest : UiSharedEditorTestBase
{
    private const string kAccessTitle = "AccessTitle";
    private const string kGenericTitle = "GenericTitle";

    private static WfReqTask AccessTask(long id) => new() { Id = id, TaskType = WfTaskType.access.ToString(), Title = kAccessTitle };

    private static WfReqTask GenericTask(long id) => new() { Id = id, TaskType = WfTaskType.generic.ToString(), Title = kGenericTitle };

    private static IRenderedComponent<ApprovalTaskDetailsView> RenderView(BunitContext context, params WfReqTask[] tasks)
    {
        WfHandler handler = new() { ActTicket = new WfTicket { Id = 1, Tasks = [.. tasks] } };
        return context.Render<ApprovalTaskDetailsView>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.WfHandler, handler));
    }

    /// <summary>The content area of each task card in display order, rendered by the Collapse component.</summary>
    private static List<string> CardContentClasses(IRenderedComponent<ApprovalTaskDetailsView> cut)
    {
        return [.. cut.FindAll("div.card").Select(card => card.Children.Last().ClassName ?? "")];
    }

    private static IElement FieldOf(IRenderedComponent<ApprovalTaskDetailsView> cut, string label)
    {
        return cut.FindAll("label").First(element => element.TextContent.Trim() == $"{label}:").ParentElement!;
    }

    [TestCase(2, 2, new[] { 3, 3 })]
    [TestCase(1, 2, new[] { 6, 3 })]
    [TestCase(0, 2, new[] { 6, 6 })]
    [TestCase(2, 1, new[] { 6 })]
    [TestCase(1, 1, new[] { 6 })]
    [TestCase(0, 0, new int[0])]
    public void FieldLayout_ExtendableFieldsGrowFromThreeToAtMostSixColumns(int standardFields, int extendableFields, int[] expected)
    {
        Assert.That(ApprovalTaskFieldLayout.ExtendableWidths(standardFields, extendableFields), Is.EqualTo(expected.ToList()));
    }

    [Test]
    public void FieldLayout_ColumnClassesKeepTheThreeColumnGrid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ApprovalTaskFieldLayout.StandardColumnClass, Is.EqualTo("col-xl-3 col-lg-6 mb-2"));
            Assert.That(ApprovalTaskFieldLayout.DoubleColumnClass, Is.EqualTo("col-xl-6 col-lg-6 mb-2"));
        });
    }

    [Test]
    public void AccessTasksFirst_MovesAccessTasksToTheBeginningAndKeepsOrder()
    {
        List<List<WfReqTask>> groups = [[GenericTask(1)], [AccessTask(2)], [GenericTask(3)], [AccessTask(4)]];

        List<long> orderedIds = [.. ApprovalTaskDetailsEditorService.AccessTasksFirst(groups).Select(group => group[0].Id)];

        Assert.That(orderedIds, Is.EqualTo(new List<long> { 2, 4, 1, 3 }));
    }

    [Test]
    public async Task AccessTaskView_UsesFullScreenAndShowsAccessTasksFirstWithOtherTasksCollapsed()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, GenericTask(1), AccessTask(2));

        List<string> contentClasses = CardContentClasses(cut);
        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("custom-modal-fs"));
            Assert.That(cut.Markup.IndexOf(kAccessTitle), Is.LessThan(cut.Markup.IndexOf(kGenericTitle)));
            Assert.That(contentClasses, Has.Count.EqualTo(2));
            Assert.That(contentClasses[0], Does.Contain("collapse.show"));
            Assert.That(contentClasses[1], Does.Not.Contain("collapse.show"));
        });
    }

    [Test]
    public async Task AccessTaskView_WithoutAccessTasksShowsOtherTasksExpanded()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, GenericTask(1));

        Assert.That(CardContentClasses(cut).Single(), Does.Contain("collapse.show"));
    }

    [Test]
    public async Task AccessTaskBody_HiddenHandlerLeavesItsSpaceToTheOwnerOnly()
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
        {
            AccessTask = new AccessTaskFieldVisibilityConfig { ShowHandler = false, ShowFreeText = false }
        }.ToConfigValue();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, AccessTask(1));

        IElement ownerField = FieldOf(cut, userConfig.GetText("owner"));
        IElement managementField = FieldOf(cut, userConfig.GetText("management"));
        IElement assignedField = FieldOf(cut, userConfig.GetText("assigned"));
        IElement reasonField = FieldOf(cut, userConfig.GetText("reason"));
        Assert.Multiple(() =>
        {
            Assert.That(ownerField.ClassList, Does.Contain("col-xl-6"));
            Assert.That(managementField.ClassList, Does.Contain("col-xl-3"));
            Assert.That(assignedField.ClassList, Does.Contain("col-xl-3"));
            Assert.That(reasonField.ClassList, Does.Contain("col-xl-6"));
            Assert.That(cut.FindAll("label.approval-task-field-label"), Is.Not.Empty);
            Assert.That(cut.Markup, Does.Not.Contain("min-width: 10rem"));
        });
    }

    [TestCase(WfTaskType.object_create, true, "col-xl-3")]
    [TestCase(WfTaskType.object_create, false, "col-xl-6")]
    [TestCase(WfTaskType.group_create, true, "col-xl-3")]
    [TestCase(WfTaskType.group_create, false, "col-xl-6")]
    public async Task ObjectAndGroupTaskBody_ReasonTakesTheSpaceOfHiddenManagement(WfTaskType taskType, bool showManagement, string expectedColumns)
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
        {
            ObjectTask = new ObjectTaskFieldVisibilityConfig { ShowManagement = showManagement }
        }.ToConfigValue();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context,
            new WfReqTask { Id = 1, TaskType = taskType.ToString(), Reason = "reason text" });

        Assert.Multiple(() =>
        {
            Assert.That(FieldOf(cut, userConfig.GetText("reason")).ClassList, Does.Contain(expectedColumns));
            Assert.That(FieldOf(cut, userConfig.GetText("handler")).ClassList, Does.Contain("col-xl-3"));
            Assert.That(cut.FindAll("label.approval-task-field-label"), Is.Not.Empty);
            Assert.That(cut.Markup, Does.Not.Contain("min-width: 10rem"));
        });
    }

    [TestCase(WfTaskType.generic)]
    [TestCase(WfTaskType.new_interface)]
    public async Task OtherTaskBodies_UseTheShortenedLabels(WfTaskType taskType)
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, new WfReqTask { Id = 1, TaskType = taskType.ToString() });

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("label.approval-task-field-label"), Is.Not.Empty);
            Assert.That(cut.Markup, Does.Not.Contain("min-width: 10rem"));
        });
    }

    [TestCase(WfTaskType.object_create)]
    [TestCase(WfTaskType.group_create)]
    public async Task ObjectAndGroupTaskBody_FollowObjectTaskVisibilityNotAccessTaskVisibility(WfTaskType taskType)
    {
        await using BunitContext context = CreateContext();
        SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
        userConfig.ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
        {
            AccessTask = new AccessTaskFieldVisibilityConfig { ShowManagement = false, ShowReason = false },
            ObjectTask = new ObjectTaskFieldVisibilityConfig { ShowHandler = false, ShowAssigned = false }
        }.ToConfigValue();

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context,
            new WfReqTask { Id = 1, TaskType = taskType.ToString(), Reason = "reason text" });

        List<string> labels = [.. cut.FindAll("label").Select(label => label.TextContent.Trim())];
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Not.Contain($"{userConfig.GetText("handler")}:"));
            Assert.That(labels, Does.Not.Contain($"{userConfig.GetText("assigned")}:"));
            Assert.That(labels, Does.Contain($"{userConfig.GetText("management")}:"));
            Assert.That(FieldOf(cut, userConfig.GetText("reason")).ClassList, Does.Contain("col-xl-6"));
        });
    }

    [TestCase(WfTaskType.object_create, "create")]
    [TestCase(WfTaskType.object_modify, "modify")]
    public async Task ObjectTaskBody_ObjectLabelUsesTheShortenedLabelWidth(WfTaskType taskType, string requestAction)
    {
        await using BunitContext context = CreateContext();
        WfReqTask task = new() { Id = 1, TaskType = taskType.ToString() };
        task.Elements.Add(new WfReqElement
        {
            Field = ElemFieldType.source.ToString(),
            RequestAction = requestAction,
            Name = "srv_web01",
            IpString = "10.1.1.5"
        });

        IRenderedComponent<ApprovalTaskDetailsView> cut = RenderView(context, task);

        IElement objectName = cut.FindAll("span").First(span => span.TextContent.Trim() == "srv_web01");
        IElement objectLabel = objectName.ParentElement!.QuerySelector("label")!;
        Assert.Multiple(() =>
        {
            Assert.That(objectLabel.ClassList, Does.Contain("approval-task-field-label"));
            Assert.That(objectLabel.ClassList, Does.Not.Contain("object-task-element-label"));
            Assert.That(cut.Markup, Does.Not.Contain("min-width: 10rem"));
        });
    }
}

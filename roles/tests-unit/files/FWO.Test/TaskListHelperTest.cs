using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using SoPro.FancyTable.Components;

namespace FWO.Test;

[TestFixture]
internal sealed class TaskListHelperTest
{
    private static readonly List<string> kValuesWithBlanks = ["a", " ", "b", "a", ""];

    [Test]
    public void Values_DropEmptyAndDuplicateValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskListHelper.SingleValue(" "), Is.Empty);
            Assert.That(TaskListHelper.SingleValue("x"), Is.EqualTo(new List<string> { "x" }));
            Assert.That(TaskListHelper.NonEmptyValues(kValuesWithBlanks), Is.EqualTo(new List<string> { "a", "b", "a" }));
            Assert.That(TaskListHelper.DistinctValues(kValuesWithBlanks), Is.EqualTo(new List<string> { "a", "b" }));
        });
    }

    [TestCase("host", "10.0.0.1", "host (10.0.0.1)")]
    [TestCase("", "10.0.0.1", "10.0.0.1")]
    [TestCase("host", "", "host")]
    [TestCase("10.0.0.1", "10.0.0.1", "10.0.0.1")]
    public void NameWithDetail_ShowsNonEmptyParts(string name, string detail, string expected)
    {
        Assert.That(TaskListHelper.NameWithDetail(name, detail), Is.EqualTo(expected));
    }

    [Test]
    public void Cells_AreReadOnlyOrEditable()
    {
        Func<Task> edit = () => Task.CompletedTask;

        Assert.Multiple(() =>
        {
            Assert.That(TaskListHelper.ReadOnly("value").CanEdit, Is.False);
            Assert.That(TaskListHelper.ReadOnly(null).Values, Is.Empty);
            Assert.That(TaskListHelper.Cell("value", edit).CanEdit, Is.True);
            Assert.That(TaskListHelper.Cell("value", edit).Values, Is.EqualTo(new List<string> { "value" }));
        });
    }

    [Test]
    public void AddIf_AddsColumnOnlyIfConditionHolds()
    {
        List<TaskListColumn> columns = [];

        TaskListHelper.AddIf(columns, false, new("hidden", "hidden"));
        TaskListHelper.AddIf(columns, true, new("shown", "shown"));

        Assert.That(columns.Select(column => column.Key), Is.EqualTo(new List<string> { "shown" }));
    }

    [TestCase("delete", "remove_member", "text-bg-danger")]
    [TestCase("unchanged", "unchanged", "text-bg-secondary")]
    [TestCase("create", "add_member", "text-bg-success")]
    [TestCase(null, "add_member", "text-bg-success")]
    public void MemberChangeBadge_UsesTextAndColorOfAction(string? action, string expectedText, string expectedClass)
    {
        TaskListBadge badge = TaskListHelper.MemberChangeBadge(action, new SimulatedUserConfig());

        Assert.Multiple(() =>
        {
            Assert.That(badge.Text, Is.EqualTo(expectedText));
            Assert.That(badge.CssClass, Is.EqualTo(expectedClass));
        });
    }

    [TestCase(TaskListTable.Access, "approval_list_access_tasks")]
    [TestCase(TaskListTable.Address, "approval_list_address_tasks")]
    [TestCase(TaskListTable.Service, "approval_list_service_tasks")]
    [TestCase(TaskListTable.Other, "approval_list_other_tasks")]
    public void TitleKey_NamesEachTable(TaskListTable table, string expected)
    {
        Assert.That(TaskListHelper.TitleKey(table), Is.EqualTo(expected));
    }

    [Test]
    public void InsertActionsColumn_PlacesActionsDirectlyAfterId()
    {
        List<FancyColumn<string>> columns =
        [
            new() { Key = "id", Title = "Id" },
            new() { Key = "title", Title = "Title" }
        ];

        TaskListHelper.InsertActionsColumn(columns, "id", "Actions", _ => (RenderFragment)(_ => { }));

        Assert.Multiple(() =>
        {
            Assert.That(columns.Select(column => column.Key), Is.EqualTo(new List<string> { "id", TaskListHelper.kActionsColumn, "title" }));
            Assert.That(columns[1].Title, Is.EqualTo("Actions"));
            Assert.That(columns[1].Sortable, Is.False);
        });
    }

    [Test]
    public void UsedTables_ReturnsTablesOfTheTaskGroupsInDisplayOrder()
    {
        List<List<WfReqTask>> taskGroups =
        [
            [new WfReqTask { TaskType = WfTaskType.generic.ToString() }],
            [],
            [new WfReqTask { TaskType = WfTaskType.access.ToString() }]
        ];

        Assert.That(ApprovalTaskListRowBuilder.UsedTables(taskGroups), Is.EqualTo(new List<TaskListTable> { TaskListTable.Access, TaskListTable.Other }));
    }

    [Test]
    public void TaskPermissions_RequireChangeableTasksOutsideReadOnlyMode()
    {
        string taskType = WfTaskType.access.ToString();
        WfReqTask changeable = new() { Id = 1, TaskType = taskType, StateId = 1 };
        WfReqTask finished = new() { Id = 2, TaskType = taskType, StateId = 10 };
        WfImplTask implementationTask = new() { Id = 3, TaskType = taskType, StateId = 1 };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, taskType,
            new WfTicket { Id = 7, Tasks = [changeable, finished] });

        Assert.Multiple(() =>
        {
            Assert.That(WorkflowTaskActions.CanChangeRequestTasks(handler, [changeable]), Is.True);
            Assert.That(WorkflowTaskActions.CanChangeRequestTasks(handler, [changeable, finished]), Is.False);
            Assert.That(WorkflowTaskActions.CanChangeRequestTasks(handler, []), Is.False);
            Assert.That(WorkflowTaskActions.CanChangeImplementationTask(handler, implementationTask, changeable), Is.True);
            Assert.That(WorkflowTaskActions.CanChangeImplementationTask(handler, implementationTask, finished), Is.False);
            Assert.That(WorkflowTaskActions.VisibleForRequestTasks(handler, []), Is.Empty);
        });

        handler.ReadOnlyMode = true;

        Assert.Multiple(() =>
        {
            Assert.That(WorkflowTaskActions.CanChangeRequestTasks(handler, [changeable]), Is.False);
            Assert.That(WorkflowTaskActions.CanChangeImplementationTask(handler, implementationTask, changeable), Is.False);
        });
    }

    [Test]
    public void TaskDetailsViewTitle_ShowsTicketIdStateAndRequester()
    {
        WfTicket ticket = new() { Id = 7, Title = "Firewall change", StateId = 2, Requester = new FWO.Data.UiUser { Name = "alice" } };

        string title = TaskDetailsViewTitle.Build(new SimulatedUserConfig(), ticket, new WfStateDict { Name = { [2] = "Planning" } });

        Assert.Multiple(() =>
        {
            Assert.That(title, Does.Contain("Firewall change"));
            Assert.That(title, Does.Contain(": 7"));
            Assert.That(title, Does.Contain("Planning"));
            Assert.That(title, Does.Contain("alice"));
        });
    }
}

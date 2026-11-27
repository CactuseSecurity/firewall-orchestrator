using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using SoPro.FancyTable.Components;
using System.Net;
using System.Text;

namespace FWO.Test;

[TestFixture]
internal sealed class TaskListHelperTest
{
    private static readonly List<string> kValuesWithBlanks = ["a", " ", "b", "a", ""];
    private static readonly IReadOnlyList<int> kOncePromotedStates = [2, 2];

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
    public void EmptyCell_SharedValuesCannotBeChanged()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskListCell.Empty.Values, Is.Empty);
            Assert.That(TaskListCell.Empty.Values, Is.Not.InstanceOf<List<string>>());
            Assert.That(((ICollection<string>)TaskListCell.Empty.Values).IsReadOnly, Is.True);
        });
    }

    [Test]
    public async Task TaskActions_DoNothingWithoutActionHandler()
    {
        WfReqTask requestTask = new() { Id = 1, TicketId = 7 };
        WfImplTask implementationTask = new() { Id = 2 };
        WfTicket ticket = new() { Id = 7, Tasks = [requestTask] };
        WfHandler handler = new() { ActTicket = ticket };
        WfStateAction action = new() { ActionType = StateActionTypes.DoNothing.ToString() };

        await WorkflowTaskActions.PerformOnRequestTasks(handler, action, [requestTask]);
        await WorkflowTaskActions.PerformOnImplementationTask(handler, action, implementationTask, requestTask);

        Assert.Multiple(() =>
        {
            Assert.That(handler.ActTicket, Is.SameAs(ticket));
            Assert.That(WorkflowTaskActions.VisibleForImplementationTask(handler, implementationTask, requestTask), Is.Empty);
        });
    }

    [Test]
    public async Task PerformOnRequestTasks_PromotesBundleOnlyOnce()
    {
        string taskType = WfTaskType.access.ToString();
        StateMatrix matrix = new()
        {
            LowestInputState = 0,
            LowestStartedState = 2,
            LowestEndState = 5,
            Matrix = new() { [0] = [2], [2] = [3] }
        };
        WfReqTask first = new() { Id = 7, TicketId = 1, TaskType = taskType, StateId = 0 };
        WfReqTask second = new() { Id = 8, TicketId = 1, TaskType = taskType, StateId = 0 };
        first.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
        second.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
        WfTicket ticket = new() { Id = 1, Tasks = [first, second] };
        WfHandler handler = new()
        {
            ActTicket = ticket,
            userConfig = new SimulatedUserConfig { ReqConsiderBundling = true },
            MasterStateMatrix = matrix,
            ActStateMatrix = matrix
        };
        handler.TicketList.Add(ticket);
        UiRequestWorkflowTest.SetMatrix(handler, taskType, matrix);
        handler.ActionHandler = new ActionHandler(new SimulatedApiConnection(), handler);
        // without a target state, auto promotion uses the next allowed transition
        WfStateAction autoPromote = new() { ActionType = StateActionTypes.AutoPromote.ToString(), ExternalParams = "" };

        await WorkflowTaskActions.PerformOnRequestTasks(handler, autoPromote, [first, second]);

        // the second task is moved along with the first one and must not be promoted again to state 3
        Assert.That(handler.ActTicket.Tasks.Select(task => task.StateId), Is.EqualTo(kOncePromotedStates));
    }

    [Test]
    public void PerformOnRequestTasks_ReloadsTicketAlsoIfTheActionFails()
    {
        WfTicket reloadedTicket = new() { Id = 1, Title = "reloaded" };
        TicketReloadApiConn apiConnection = new() { Ticket = reloadedTicket };
        WfHandler handler = CreateReloadingHandler(apiConnection, null!);
        WfReqTask requestTask = new() { Id = 7, TicketId = 1, TaskType = WfTaskType.access.ToString() };
        handler.ActTicket = new WfTicket { Id = 1, Title = "stale", Tasks = [requestTask] };
        // unparsable parameters make the action fail
        WfStateAction failingAction = new() { ActionType = StateActionTypes.AutoPromote.ToString(), ExternalParams = "{" };

        Assert.ThrowsAsync<System.Text.Json.JsonException>(() => WorkflowTaskActions.PerformOnRequestTasks(handler, failingAction, [requestTask]));

        Assert.That(handler.ActTicket.Title, Is.EqualTo("reloaded"));
    }

    [Test]
    public async Task PerformOnRequestTasks_SkipsTasksTheMiddlewareMovedAlong()
    {
        string taskType = WfTaskType.access.ToString();
        WfReqTask first = new() { Id = 7, TicketId = 1, TaskType = taskType, StateId = 0 };
        WfReqTask second = new() { Id = 8, TicketId = 1, TaskType = taskType, StateId = 0 };
        WfTicket storedTicket = new()
        {
            Id = 1,
            Tasks = [new() { Id = 7, TicketId = 1, TaskType = taskType, StateId = 0 }, new() { Id = 8, TicketId = 1, TaskType = taskType, StateId = 0 }]
        };
        TicketReloadApiConn apiConnection = new() { Ticket = storedTicket };
        using TestMiddlewareClient middlewareClient = new();
        // like the middleware, promoting one task of the bundle moves the other one along, only in the stored ticket
        BundlePromotingMiddlewareHandler middlewareHandler = new(() => storedTicket.Tasks.ForEach(task => task.StateId = 2));
        middlewareClient.UseHandler(middlewareHandler);
        WfHandler handler = CreateReloadingHandler(apiConnection, middlewareClient);
        handler.ActTicket = new WfTicket { Id = 1, Tasks = [first, second] };
        WfStateAction autoPromote = new() { Id = 3, ActionType = StateActionTypes.AutoPromote.ToString(), ExternalParams = "" };

        await WorkflowTaskActions.PerformOnRequestTasks(handler, autoPromote, [first, second]);

        Assert.Multiple(() =>
        {
            // the second task would be rejected by the middleware, as it is no longer in the state it is sent with
            Assert.That(middlewareHandler.RequestCount, Is.EqualTo(1));
            Assert.That(handler.ActTicket.Tasks.Select(task => task.StateId), Is.EqualTo(kOncePromotedStates));
        });
    }

    [TestCase(false, 2, TestName = "PerformOnRequestTasks_SkipsRemainingTasksIfTheReloadQueryFails")]
    [TestCase(true, 3, TestName = "PerformOnRequestTasks_SkipsRemainingTasksIfTheReloadFailsQuietly")]
    public async Task PerformOnRequestTasks_SkipsRemainingTasksIfTheReloadFails(bool quietly, long ticketId)
    {
        string taskType = WfTaskType.access.ToString();
        // other ids than in the other tests, as the action handler drops repeated middleware delegations for a few seconds
        WfReqTask first = new() { Id = ticketId * 10 + 7, TicketId = ticketId, TaskType = taskType, StateId = 0 };
        WfReqTask second = new() { Id = ticketId * 10 + 8, TicketId = ticketId, TaskType = taskType, StateId = 0 };
        TicketReloadApiConn apiConnection = new() { Ticket = new WfTicket { Id = ticketId, Tasks = [first, second] } };
        using TestMiddlewareClient middlewareClient = new();
        List<(string Message, bool Error)> messages = [];
        WfHandler handler = CreateReloadingHandler(apiConnection, middlewareClient,
            (_, _, message, error) => messages.Add((message, error)), new SkippedTasksTextUserConfig());
        // the middleware moves the bundle along, but the ticket can not be reloaded afterwards: the query fails (reported
        // by the handler), or the reload ends without an error, like for a ticket the user may no longer see
        BundlePromotingMiddlewareHandler middlewareHandler = new(() =>
        {
            if (quietly)
            {
                SetDbAccess(handler, null);
            }
            else
            {
                apiConnection.FailReload = true;
            }
        });
        middlewareClient.UseHandler(middlewareHandler);
        handler.ActTicket = new WfTicket { Id = ticketId, Tasks = [first, second] };
        WfStateAction autoPromote = new() { Id = 3, Name = "promote", ActionType = StateActionTypes.AutoPromote.ToString(), ExternalParams = "" };

        await WorkflowTaskActions.PerformOnRequestTasks(handler, autoPromote, [first, second]);

        Assert.Multiple(() =>
        {
            // with its stale state, the second task would be rejected by the middleware
            Assert.That(middlewareHandler.RequestCount, Is.EqualTo(1));
            // the user learns that the bundle moved only partly
            Assert.That(messages, Does.Contain(($"skipped: {second.Id}", true)));
            // the final reload is still tried, so a passing query error does not leave a stale ticket
            Assert.That(apiConnection.TicketQueryCount, Is.EqualTo(quietly ? 0 : 2));
        });
    }

    private static WfHandler CreateReloadingHandler(TicketReloadApiConn apiConnection, FWO.Middleware.Client.MiddlewareClient middlewareClient,
        Action<Exception?, string, string, bool>? displayMessage = null, SimulatedUserConfig? userConfig = null)
    {
        userConfig ??= new SimulatedUserConfig();
        WfHandler handler = new(displayMessage ?? DefaultInit.DoNothing, userConfig, new System.Security.Claims.ClaimsPrincipal(), apiConnection, middlewareClient, WorkflowPhases.planning);
        handler.ActionHandler = new ActionHandler(apiConnection, handler);
        SetDbAccess(handler, new WfDbAccess(DefaultInit.DoNothing, userConfig, apiConnection, handler.ActionHandler, false, WorkflowPhases.planning));
        return handler;
    }

    private static void SetDbAccess(WfHandler handler, WfDbAccess? dbAccess)
    {
        typeof(WfHandler).GetField("dbAcc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(handler, dbAccess);
    }

    private sealed class SkippedTasksTextUserConfig : SimulatedUserConfig
    {
        public override string GetText(string key) => key == "planning_bundle_action_stopped" ? "skipped: {0}" : base.GetText(key);
    }

    private sealed class BundlePromotingMiddlewareHandler(Action promoteBundle) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            promoteBundle();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"messages\":[],\"errorMessage\":\"\"}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TicketReloadApiConn : SimulatedApiConnection
    {
        public WfTicket Ticket { get; set; } = new();

        public bool FailReload { get; set; }

        public int TicketQueryCount { get; private set; }

        public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == FWO.Api.Client.Queries.RequestQueries.getTicketById)
            {
                TicketQueryCount++;
            }
            if (FailReload)
            {
                throw new HttpRequestException("reload failed");
            }
            return query == FWO.Api.Client.Queries.RequestQueries.getTicketById
                ? Task.FromResult((T)(object)Ticket)
                : throw new NotImplementedException($"Unexpected query: {query}");
        }
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

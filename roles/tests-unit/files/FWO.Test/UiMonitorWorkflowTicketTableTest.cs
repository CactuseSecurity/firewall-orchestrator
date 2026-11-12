using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiMonitorWorkflowTicketTableTest
    {
        private const int kDefaultPageSize = 10;
        private const string kExpandAllButton = "button[name=btnExpandAll]";
        private const string kChangeStateButton = "button.btn-danger";
        private const string kAutoCreateButton = "button.btn-warning";

        private static readonly Dictionary<int, string> kStateNames = new() { [1] = "Requested", [2] = "Approved", [3] = "Implementation" };

        private BunitContext context = null!;
        private readonly List<FancyItemsRequest> requests = [];

        [SetUp]
        public void SetUp()
        {
            context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new MonitorWorkflowUserConfig());
        }

        [TearDown]
        public async Task TearDown()
        {
            await context.DisposeAsync();
        }

        [Test]
        public void Render_NoTickets_ShowsNoRowsText()
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket>());

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("tbody tr"), Is.Empty);
                Assert.That(component.Find(".fancy-pagination-summary").TextContent, Does.Contain(new MonitorWorkflowUserConfig().GetText("no_rows")));
            });
        }

        [Test]
        public void FirstRender_RequestsPage1WithDefaultPageSize()
        {
            RenderTable(new List<WfTicket> { CreateTicket() });

            FancyItemsRequest request = requests.Single();
            Assert.That((request.Page, request.PageSize, request.SearchText, request.SortColumnKey), Is.EqualTo((1, kDefaultPageSize, "", (string?)null)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NextPage_EnabledOnlyWhenHasMore(bool hasMore)
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() }, hasMore: hasMore);

            List<IElement> enabledNextButtons = [.. component.FindAll(".fancy-pagination button")
                .Where(button => button.TextContent.Contains(new MonitorWorkflowUserConfig().GetText("next")) && !button.HasAttribute("disabled"))];

            // Without a next page the single first page needs no pager at all.
            Assert.That(enabledNextButtons, Has.Count.EqualTo(hasMore ? 1 : 0));
        }

        [Test]
        public void TotalCount_IsPassedToThePager()
        {
            List<WfTicket> tickets = [CreateTicket()];
            Func<FancyItemsRequest, CancellationToken, Task<FancyItemsResult<WfTicket>>> loadTickets = (request, _) =>
                Task.FromResult(new FancyItemsResult<WfTicket>(tickets, true) { TotalCount = 3 * kDefaultPageSize + 1 });

            IRenderedComponent<MonitorWorkflowTicketTable> component = context.Render<MonitorWorkflowTicketTable>(parameters =>
                parameters.Add(p => p.LoadTickets, loadTickets).Add(p => p.StateNames, kStateNames));

            List<string> pageButtons = [.. component.FindAll(".fancy-pagination .btn-group button")
                .Select(button => button.TextContent.Trim()).Where(text => int.TryParse(text, out _))];
            Assert.That(string.Join(",", pageButtons), Is.EqualTo("1,2,3,4"));
        }

        [Test]
        public void SortHeaderClick_SendsSortKey()
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() });

            component.Find("th[data-column-key='name'] button[aria-label]:has(i.bi-arrow-down-up)").Click();

            Assert.That((requests[^1].SortColumnKey, requests[^1].SortAscending, requests[^1].Page), Is.EqualTo(("name", true, 1)));
        }

        [Test]
        public async Task RefreshAsync_ResetToFirstPage_RequestsPage1()
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() }, hasMore: true);
            component.FindAll(".fancy-pagination button").Single(button => button.TextContent.Contains(new MonitorWorkflowUserConfig().GetText("next"))).Click();

            await component.InvokeAsync(() => component.Instance.RefreshAsync(false));
            Assert.That(requests[^1].Page, Is.EqualTo(2));

            await component.InvokeAsync(() => component.Instance.RefreshAsync(true));
            Assert.That(requests[^1].Page, Is.EqualTo(1));
        }

        [Test]
        public void Render_OffersNoFreeTextSearch()
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() });

            string searchPlaceholder = new MonitorWorkflowUserConfig().GetText("search");
            Assert.That(component.FindAll($"input[placeholder='{searchPlaceholder}']"), Is.Empty);
        }

        [TestCase("id", true, true)]
        [TestCase("name", true, true)]
        [TestCase("state", true, true)]
        [TestCase("requester", false, true)]
        [TestCase("start_time", true, true)]
        [TestCase("end_time", true, true)]
        [TestCase("type", false, false)]
        [TestCase("tasks", false, false)]
        [TestCase("actions", false, false)]
        public void ColumnFlags_OfferOnlyWhatTheServerSupports(string columnKey, bool sortable, bool filterable)
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() });

            string header = $"th[data-column-key='{columnKey}']";
            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll($"{header} i.bi-arrow-down-up"), Has.Count.EqualTo(sortable ? 1 : 0));
                Assert.That(component.FindAll($"{header} button.fancy-filter-button"), Has.Count.EqualTo(filterable ? 1 : 0));
            });
        }

        [TestCase("id", "Equals,NotEquals")]
        [TestCase("name", "Equals,NotEquals,StartsWith")]
        [TestCase("start_time", "Equals,NotEquals,GreaterThan,GreaterThanOrEqual,LessThan,LessThanOrEqual,Between,IsEmpty,IsNotEmpty")]
        [TestCase("end_time", "Equals,NotEquals,GreaterThan,GreaterThanOrEqual,LessThan,LessThanOrEqual,Between,IsEmpty,IsNotEmpty")]
        [TestCase("state", "Equals,NotEquals,Contains,NotContains,StartsWith,EndsWith,IsEmpty,IsNotEmpty")]
        [TestCase("requester", "Equals,NotEquals,Contains,NotContains,StartsWith,EndsWith,IsEmpty,IsNotEmpty")]
        public void FilterPopup_OffersOnlyOperatorsTheServerSupports(string columnKey, string expectedOperators)
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() });

            string header = $"th[data-column-key='{columnKey}']";
            component.Find($"{header} button.fancy-filter-button").Click();

            List<string?> offeredOperators = [.. component.FindAll($"{header} select option").Select(option => option.GetAttribute("value"))];
            Assert.That(string.Join(",", offeredOperators), Is.EqualTo(expectedOperators));
        }

        [Test]
        public void DateFilter_Between_SendsBothDaysToTheServer()
        {
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { CreateTicket() });
            string header = "th[data-column-key='start_time']";

            component.Find($"{header} button.fancy-filter-button").Click();
            component.Find($"{header} select").Change(nameof(FancyColumnFilterOperator.Between));
            List<IElement> dateInputs = [.. component.FindAll($"{header} input[type=date]")];
            dateInputs[0].Input("2026-11-01");
            dateInputs[1].Input("2026-11-10");
            component.Find($"{header} button.btn-primary").Click();

            FancyColumnFilter filter = requests[^1].ColumnFilters.Single();
            Assert.That((filter.Key, filter.Operator, filter.Value, filter.ValueTo),
                Is.EqualTo(("start_time", FancyColumnFilterOperator.Between, "2026-11-01", "2026-11-10")));
        }

        [Test]
        public void Render_ShowsStartTimeAsBefore()
        {
            WfTicket ticket = CreateTicket();
            ticket.CreationDate = new DateTime(2026, 11, 10, 8, 30, 0);

            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { ticket });

            List<string?> columnKeys = [.. component.FindAll("thead th").Select(header => header.GetAttribute("data-column-key"))];
            IElement startCell = component.FindAll("tbody tr td")[columnKeys.IndexOf("start_time")];
            Assert.That(startCell.TextContent.Trim(), Is.EqualTo(ticket.CreationDate.ToString()));
        }

        [Test]
        public void Render_ShowsTicketRowsWithResolvedStateAndCollapsedChildren()
        {
            WfTicket ticket = CreateTicket();

            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { ticket });

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("tbody tr"), Has.Count.EqualTo(1));
                Assert.That(component.Markup, Does.Contain("Ticket A"));
                Assert.That(component.Markup, Does.Contain("Approved"));
                Assert.That(component.Markup, Does.Contain("Alice"));
                Assert.That(component.Markup, Does.Not.Contain("Request task"));
            });
        }

        [Test]
        public void ExpandAll_ShowsRequestTasksImplementationTasksAndApprovals()
        {
            WfTicket ticket = CreateTicket();
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { ticket });

            component.Find(kExpandAllButton).Click();

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("tbody tr"), Has.Count.EqualTo(4));
                Assert.That(component.Markup, Does.Contain("Request task"));
                Assert.That(component.Markup, Does.Contain("Implementation task"));
                Assert.That(component.Markup, Does.Contain("Approver group"));
                Assert.That(component.Markup, Does.Contain("99"));
            });
        }

        [Test]
        public void ChangeStateButtons_InvokeTheCallbackOfTheirRowKind()
        {
            WfTicket ticket = CreateTicket();
            WfReqTask reqTask = ticket.Tasks[0];
            List<string> calls = [];
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { ticket }, parameters => parameters
                .Add(p => p.ChangeTicketState, (Func<WfTicket, Task>)(changedTicket =>
                {
                    calls.Add($"ticket {changedTicket.Id}");
                    return Task.CompletedTask;
                }))
                .Add(p => p.ChangeReqTaskState, (Func<WfTicket, WfReqTask, Task>)((changedTicket, changedReqTask) =>
                {
                    calls.Add($"reqtask {changedTicket.Id}/{changedReqTask.Id}");
                    return Task.CompletedTask;
                }))
                .Add(p => p.ChangeImplTaskState, (Func<WfTicket, WfReqTask, WfImplTask, Task>)((changedTicket, changedReqTask, changedImplTask) =>
                {
                    calls.Add($"impltask {changedTicket.Id}/{changedReqTask.Id}/{changedImplTask.Id}");
                    return Task.CompletedTask;
                }))
                .Add(p => p.ChangeApprovalState, (Func<WfTicket, WfReqTask, WfApproval, Task>)((changedTicket, changedReqTask, changedApproval) =>
                {
                    calls.Add($"approval {changedTicket.Id}/{changedReqTask.Id}/{changedApproval.Id}");
                    return Task.CompletedTask;
                })));
            component.Find(kExpandAllButton).Click();

            int buttonCount = component.FindAll(kChangeStateButton).Count;
            for (int index = 0; index < buttonCount; index++)
            {
                component.FindAll(kChangeStateButton)[index].Click();
            }

            Assert.That(calls, Is.EquivalentTo(new List<string>
            {
                "ticket 1",
                $"reqtask 1/{reqTask.Id}",
                $"impltask 1/{reqTask.Id}/{reqTask.ImplementationTasks[0].Id}",
                $"approval 1/{reqTask.Id}/{reqTask.Approvals[0].Id}"
            }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AutoCreateButton_IsOfferedOnlyForAllowedRequestTasks(bool canAutoCreate)
        {
            WfTicket ticket = CreateTicket();
            WfReqTask? autoCreatedReqTask = null;
            IRenderedComponent<MonitorWorkflowTicketTable> component = RenderTable(new List<WfTicket> { ticket }, parameters => parameters
                .Add(p => p.CanAutoCreateImplementationTasks, (Func<WfTicket, WfReqTask, bool>)((_, _) => canAutoCreate))
                .Add(p => p.AutoCreateImplementationTasks, (Func<WfTicket, WfReqTask, Task>)((_, reqTask) =>
                {
                    autoCreatedReqTask = reqTask;
                    return Task.CompletedTask;
                })));
            component.Find(kExpandAllButton).Click();

            IReadOnlyList<IElement> autoCreateButtons = component.FindAll(kAutoCreateButton);
            Assert.That(autoCreateButtons, Has.Count.EqualTo(canAutoCreate ? 1 : 0));
            if (canAutoCreate)
            {
                autoCreateButtons[0].Click();
                Assert.That(autoCreatedReqTask, Is.SameAs(ticket.Tasks[0]));
            }
        }

        private IRenderedComponent<MonitorWorkflowTicketTable> RenderTable(List<WfTicket> tickets,
            Action<ComponentParameterCollectionBuilder<MonitorWorkflowTicketTable>>? addParameters = null, bool hasMore = false)
        {
            Func<FancyItemsRequest, CancellationToken, Task<FancyItemsResult<WfTicket>>> loadTickets = (request, _) =>
            {
                requests.Add(request);
                return Task.FromResult(new FancyItemsResult<WfTicket>(tickets, hasMore));
            };
            return context.Render<MonitorWorkflowTicketTable>(parameters =>
            {
                parameters.Add(p => p.LoadTickets, loadTickets).Add(p => p.StateNames, kStateNames);
                addParameters?.Invoke(parameters);
            });
        }

        private static WfTicket CreateTicket()
        {
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 1,
                TaskNumber = 1,
                Title = "Request task",
                StateId = 3,
                TaskType = WfTaskType.access.ToString(),
                ImplementationTasks = [new WfImplTask { Id = 21, TaskNumber = 1, Title = "Implementation task", StateId = 3 }],
                Approvals = [new WfApproval { Id = 31, ApproverGroup = "Approver group", StateId = 99 }]
            };
            reqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            return new WfTicket
            {
                Id = 1,
                Title = "Ticket A",
                StateId = 2,
                Requester = new() { Name = "Alice" },
                Tasks = [reqTask]
            };
        }
    }
}

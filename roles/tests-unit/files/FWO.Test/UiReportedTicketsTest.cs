using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiReportedTicketsTest
    {
        private static T GetPrivateProperty<T>(ReportedTickets component, string propertyName)
        {
            PropertyInfo? property = typeof(ReportedTickets).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(ReportedTickets).FullName, propertyName);
            }
            return (T)property.GetValue(component)!;
        }

        private static void SetComponentProperty<T>(ReportedTickets component, string propertyName, T value)
        {
            PropertyInfo? property = typeof(ReportedTickets).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(ReportedTickets).FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static object? InvokePrivateMethod(ReportedTickets component, string methodName, params object?[] args)
        {
            MethodInfo? method = typeof(ReportedTickets).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(typeof(ReportedTickets).FullName, methodName);
            }
            return method.Invoke(component, args);
        }

        [Test]
        public void SortedTickets_OrdersByStoredReferenceDate_ThenByTicketId()
        {
            DateTime earliestDate = new(2026, 2, 1);
            DateTime latestDate = new(2026, 3, 1);
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.SelectedReportType), ReportType.TicketChangeReport);
            SetComponentProperty(component, nameof(ReportedTickets.Tickets), new List<WfTicket>
            {
                new() { Id = 1 },
                new() { Id = 2 },
                new() { Id = 3 },
                new() { Id = 4 }
            });
            SetComponentProperty(component, nameof(ReportedTickets.TicketReferenceDates), new Dictionary<long, DateTime?>
            {
                [1] = earliestDate,
                [3] = latestDate,
                [4] = latestDate
            });

            List<WfTicket> sortedTickets = GetPrivateProperty<IEnumerable<WfTicket>>(component, "SortedTickets").ToList();

            Assert.That(sortedTickets.Select(ticket => ticket.Id), Is.EqualTo(new long[] { 4, 3, 1, 2 }));
        }

        [Test]
        public void SortedTickets_WithoutReferenceDateColumn_OrdersByTicketId()
        {
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.SelectedReportType), ReportType.TicketReport);
            SetComponentProperty(component, nameof(ReportedTickets.Tickets), new List<WfTicket>
            {
                new() { Id = 3 },
                new() { Id = 1 },
                new() { Id = 2 }
            });

            List<WfTicket> sortedTickets = GetPrivateProperty<IEnumerable<WfTicket>>(component, "SortedTickets").ToList();

            Assert.That(sortedTickets.Select(ticket => ticket.Id), Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        [Test]
        public void GetTicketReferenceDateValue_ReturnsStoredDate_OrEmptyString()
        {
            DateTime referenceDate = new(2026, 4, 12, 8, 30, 0);
            WfTicket ticketWithDate = new() { Id = 7 };
            WfTicket ticketWithoutDate = new() { Id = 8 };
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.TicketReferenceDates), new Dictionary<long, DateTime?>
            {
                [ticketWithDate.Id] = referenceDate
            });

            string storedValue = (string)(InvokePrivateMethod(component, "GetTicketReferenceDateValue", ticketWithDate)
                ?? throw new InvalidOperationException("Expected reference date string."));
            string missingValue = (string)(InvokePrivateMethod(component, "GetTicketReferenceDateValue", ticketWithoutDate)
                ?? throw new InvalidOperationException("Expected empty reference date string."));

            Assert.That(storedValue, Is.EqualTo(referenceDate.ToString()));
            Assert.That(missingValue, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ResolveStateName_ReturnsConfiguredName_OrStateId()
        {
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.StateNames), new Dictionary<int, string>
            {
                [7] = "Implemented"
            });

            string configuredName = (string)(InvokePrivateMethod(component, "ResolveStateName", 7)
                ?? throw new InvalidOperationException("Expected configured state name."));
            string fallbackName = (string)(InvokePrivateMethod(component, "ResolveStateName", 8)
                ?? throw new InvalidOperationException("Expected fallback state name."));

            Assert.Multiple(() =>
            {
                Assert.That(configuredName, Is.EqualTo("Implemented"));
                Assert.That(fallbackName, Is.EqualTo("8"));
            });
        }

        [Test]
        public void GetLabelValue_ReturnsDistinctNonEmptyTaskLabels()
        {
            const string addInfoName = "externalId";
            WfReqTask firstTask = new() { Id = 1 };
            WfReqTask duplicateTask = new() { Id = 2 };
            WfReqTask emptyTask = new() { Id = 3 };
            firstTask.SetAddInfo(addInfoName, "CR-7");
            duplicateTask.SetAddInfo(addInfoName, "CR-7");
            emptyTask.SetAddInfo(addInfoName, "");
            WfTicket ticket = new()
            {
                Tasks = [firstTask, duplicateTask, emptyTask]
            };
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.AddInfoName), addInfoName);

            string addInfoValue = (string)(InvokePrivateMethod(component, "GetAddInfoValue", ticket)
                ?? throw new InvalidOperationException("Expected additional info value."));

            Assert.That(addInfoValue, Is.EqualTo("CR-7"));
        }

        [Test]
        public void GetLabelValue_WithoutLabelColumn_ReturnsEmptyString()
        {
            WfReqTask task = new() { Id = 1 };
            task.SetAddInfo("externalId", "CR-7");
            WfTicket ticket = new()
            {
                Tasks = [task]
            };
            ReportedTickets component = new();

            string addInfoValue = (string)(InvokePrivateMethod(component, "GetAddInfoValue", ticket)
                ?? throw new InvalidOperationException("Expected empty label value."));

            Assert.That(addInfoValue, Is.EqualTo(string.Empty));
        }

        private const int kOpenStateId = 1;
        private const int kDoneStateId = 9;
        private const int kUnnamedStateId = 5;
        private const string kFlowBundleId = "bundle-7";

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }

        private static Dictionary<int, string> StateNames()
        {
            return new() { [kOpenStateId] = "open", [kDoneStateId] = "done" };
        }

        /// <summary>
        /// A ticket with one request task, which has one implementation task and one approval.
        /// </summary>
        private static WfTicket BuildTicket(long id, string title)
        {
            WfReqTask task = new()
            {
                Id = id * 10,
                TaskNumber = 1,
                Title = $"{title} task",
                StateId = kOpenStateId,
                TaskType = WfTaskType.access.ToString(),
                ImplementationTasks = [new WfImplTask { Id = id * 100, TaskNumber = 1, Title = $"{title} implementation", StateId = kDoneStateId }],
                Approvals = [new WfApproval { Id = id * 1000, StateId = kUnnamedStateId, DateOpened = new DateTime(2026, 1, 2) }]
            };
            task.SetAddInfo(AdditionalInfoKeys.FlowBundleId, kFlowBundleId);
            return new WfTicket
            {
                Id = id,
                Title = title,
                StateId = kOpenStateId,
                Requester = new UiUser { Name = "requester" },
                CreationDate = new DateTime(2026, 1, 1),
                Tasks = [task]
            };
        }

        private static IRenderedComponent<ReportedTickets> Render(BunitContext context, List<WfTicket> tickets,
            Action<ComponentParameterCollectionBuilder<ReportedTickets>>? configure = null)
        {
            return context.Render<ReportedTickets>(parameters =>
            {
                parameters.Add(p => p.Tickets, tickets).Add(p => p.StateNames, StateNames());
                configure?.Invoke(parameters);
            });
        }

        private static List<string> ColumnTitles(AngleSharp.Dom.IElement table)
        {
            return [.. table.QuerySelectorAll(":scope > thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        private static List<string> FirstRowCells(AngleSharp.Dom.IElement table)
        {
            return [.. table.QuerySelector(":scope > tbody > tr")!.QuerySelectorAll(":scope > td").Select(cell => cell.TextContent.Trim())];
        }

        private static List<AngleSharp.Dom.IElement> FirstRowButtons(AngleSharp.Dom.IElement table)
        {
            return [.. table.QuerySelector(":scope > tbody > tr")!.QuerySelectorAll(":scope > td button")];
        }

        private static void ExpandFirstRow(AngleSharp.Dom.IElement table)
        {
            table.QuerySelector(":scope > tbody > tr a[aria-expanded]")!.Click();
        }

        [Test]
        public async Task WithoutTicketsNothingIsShown()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedTickets> component = Render(context, []);

            Assert.That(component.FindAll("table"), Is.Empty);
        }

        [Test]
        public async Task EachTicketShowsItsDataAndTheNameOfItsState()
        {
            await using BunitContext context = CreateContext();
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();

            IRenderedComponent<ReportedTickets> component = Render(context, [BuildTicket(1, "first")]);

            AngleSharp.Dom.IElement table = component.Find("table");
            Assert.That(ColumnTitles(table), Is.EqualTo(new List<string>
            {
                userConfig.GetText("id"), userConfig.GetText("name"), userConfig.GetText("tasks"), userConfig.GetText("requester"), userConfig.GetText("state"),
                $"{userConfig.GetText("ticket")} {userConfig.GetText("created")}", $"{userConfig.GetText("ticket")} {userConfig.GetText("closed")}"
            }));
            Assert.That(FirstRowCells(table).Take(5), Is.EqualTo(new List<string> { "1", "first", "1", "requester", "open" }));
            Assert.That(table.QuerySelectorAll(":scope > tbody a[aria-expanded]"), Is.Empty);
        }

        [Test]
        public async Task TicketsCanBeListedNewestFirst()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedTickets> component = Render(context, [BuildTicket(1, "older"), BuildTicket(2, "newer")],
                parameters => parameters.Add(p => p.SortDescendingById, true));

            List<string> titles = [.. component.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td")[1].TextContent.Trim())];
            Assert.That(titles, Is.EqualTo(new List<string> { "newer", "older" }));
        }

        [Test]
        public async Task ATicketChangeReportShowsTheReferenceDateOfEachTicket()
        {
            await using BunitContext context = CreateContext();
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();
            DateTime referenceDate = new(2026, 3, 4, 5, 6, 0);

            IRenderedComponent<ReportedTickets> component = Render(context, [BuildTicket(1, "first")], parameters => parameters
                .Add(p => p.SelectedReportType, ReportType.TicketChangeReport)
                .Add(p => p.ReferenceDate, WorkflowReferenceDate.TaskStart)
                .Add(p => p.TicketReferenceDates, new Dictionary<long, DateTime?> { [1] = referenceDate }));

            AngleSharp.Dom.IElement table = component.Find("table");
            Assert.That(ColumnTitles(table)[^1], Is.EqualTo(userConfig.GetText(WorkflowReferenceDate.TaskStart.ToString())));
            Assert.That(FirstRowCells(table)[^1], Is.EqualTo(referenceDate.ToString()));
        }

        [Test]
        public async Task AnAdditionalInfoColumnShowsTheValueOfTheTicketsTasks()
        {
            await using BunitContext context = CreateContext();
            WfTicket ticket = BuildTicket(1, "first");
            ticket.Tasks[0].SetAddInfo("externalId", "CR-7");

            IRenderedComponent<ReportedTickets> component = Render(context, [ticket], parameters => parameters.Add(p => p.AddInfoName, "externalId"));

            AngleSharp.Dom.IElement table = component.Find("table");
            Assert.That(ColumnTitles(table)[^1], Is.EqualTo("externalId"));
            Assert.That(FirstRowCells(table)[^1], Is.EqualTo("CR-7"));
        }

        [Test]
        public async Task TheTicketStateCanBeChangedFromTheReport()
        {
            await using BunitContext context = CreateContext();
            WfTicket ticket = BuildTicket(1, "first");
            List<WfTicket> changedTickets = [];

            IRenderedComponent<ReportedTickets> component = Render(context, [ticket], parameters => parameters
                .Add(p => p.ShowStateChangeButtons, true)
                .Add(p => p.ChangeTicketState, changed => { changedTickets.Add(changed); return Task.CompletedTask; }));
            component.Find("tbody button.btn-danger").Click();

            Assert.That(changedTickets, Is.EqualTo(new List<WfTicket> { ticket }));
        }

        [Test]
        public async Task TheDetailedViewListsTheTasksOfATicket()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedTickets> component = Render(context, [BuildTicket(1, "first")], parameters => parameters
                .Add(p => p.DetailedView, true)
                .Add(p => p.SelectedReportType, ReportType.TicketReport));
            ExpandFirstRow(component.Find("table"));

            AngleSharp.Dom.IElement taskTable = component.FindAll("table")[1];
            Assert.That(FirstRowCells(taskTable).Skip(1).Take(5), Is.EqualTo(new List<string> { "10", "1", "first task", kFlowBundleId, "open" }));
        }

        [Test]
        public async Task ExpandingATaskShowsItsImplementationTasksAndApprovals()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedTickets> component = Render(context, [BuildTicket(1, "first")], parameters => parameters
                .Add(p => p.DetailedView, true)
                .Add(p => p.SelectedReportType, ReportType.TicketReport));
            ExpandFirstRow(component.Find("table"));
            ExpandFirstRow(component.FindAll("table")[1]);

            List<AngleSharp.Dom.IElement> tables = [.. component.FindAll("table")];
            Assert.That(tables, Has.Count.EqualTo(4));
            Assert.That(FirstRowCells(tables[2]).Take(4), Is.EqualTo(new List<string> { "100", "1", "first implementation", "done" }));
            // a state without configured name is shown by its id
            Assert.That(FirstRowCells(tables[3])[0], Is.EqualTo("1000"));
            Assert.That(FirstRowCells(tables[3])[2], Is.EqualTo(kUnnamedStateId.ToString()));
        }

        [Test]
        public async Task EveryLevelOffersAStateChangeAndTasksCanGetImplementationTasks()
        {
            await using BunitContext context = CreateContext();
            WfTicket ticket = BuildTicket(1, "first");
            WfReqTask task = ticket.Tasks[0];
            List<string> calls = [];

            IRenderedComponent<ReportedTickets> component = Render(context, [ticket], parameters => parameters
                .Add(p => p.DetailedView, true)
                .Add(p => p.SelectedReportType, ReportType.TicketReport)
                .Add(p => p.ShowStateChangeButtons, true)
                .Add(p => p.ChangeReqTaskState, (_, changed) => { calls.Add($"task {changed.Id}"); return Task.CompletedTask; })
                .Add(p => p.ChangeImplTaskState, (_, _, changed) => { calls.Add($"implementation {changed.Id}"); return Task.CompletedTask; })
                .Add(p => p.ChangeApprovalState, (_, _, changed) => { calls.Add($"approval {changed.Id}"); return Task.CompletedTask; })
                .Add(p => p.CanAutoCreateImplementationTasks, (_, candidate) => candidate == task)
                .Add(p => p.AutoCreateImplementationTasks, (_, created) => { calls.Add($"create for {created.Id}"); return Task.CompletedTask; }));
            ExpandFirstRow(component.Find("table"));
            ExpandFirstRow(component.FindAll("table")[1]);

            // every click renders again, so the buttons are looked up anew each time
            int buttonCount = component.FindAll("table").Skip(1).Sum(table => FirstRowButtons(table).Count);
            for (int button = 0; button < buttonCount; button++)
            {
                component.FindAll("table").Skip(1).SelectMany(FirstRowButtons).ElementAt(button).Click();
            }

            Assert.That(calls, Is.EquivalentTo((List<string>)["task 10", "create for 10", "implementation 100", "approval 1000"]));
        }

        [Test]
        public async Task TasksWithoutImplementationTasksOrApprovalsHaveNoNestedDetails()
        {
            await using BunitContext context = CreateContext();
            WfTicket ticket = BuildTicket(1, "first");
            ticket.Tasks[0].ImplementationTasks = [];
            ticket.Tasks[0].Approvals = [];

            IRenderedComponent<ReportedTickets> component = Render(context, [ticket], parameters => parameters
                .Add(p => p.DetailedView, true)
                .Add(p => p.SelectedReportType, ReportType.TicketReport));
            ExpandFirstRow(component.Find("table"));
            ExpandFirstRow(component.FindAll("table")[1]);

            Assert.That(component.FindAll("table"), Has.Count.EqualTo(2));
            Assert.That(component.FindAll("div.bg-light"), Is.Empty);
        }

        [Test]
        public async Task ATicketChangeReportShowsOnlyTheActivitiesMatchingItsReferenceDate()
        {
            await using BunitContext context = CreateContext();
            WfTicket ticket = BuildTicket(1, "first");
            ticket.Tasks[0].Approvals[0].DateOpened = new DateTime(2026, 2, 15);

            IRenderedComponent<ReportedTickets> component = Render(context, [ticket], parameters => parameters
                .Add(p => p.DetailedView, true)
                .Add(p => p.ShowFullTicket, false)
                .Add(p => p.SelectedReportType, ReportType.TicketChangeReport)
                .Add(p => p.ReferenceDate, WorkflowReferenceDate.ApprovalOpened)
                .Add(p => p.TimeRangeStart, "2026-02-01")
                .Add(p => p.TimeRangeEnd, "2026-03-01"));
            ExpandFirstRow(component.Find("table"));
            ExpandFirstRow(component.FindAll("table")[1]);

            List<AngleSharp.Dom.IElement> tables = [.. component.FindAll("table")];
            // implementation tasks are not part of an approval reference date, the approval is in the time range
            Assert.That(tables, Has.Count.EqualTo(3));
            Assert.That(component.Markup, Does.Not.Contain("first implementation"));
            Assert.That(FirstRowCells(tables[2])[0], Is.EqualTo("1000"));
        }

        [Test]
        public void GetDisplayedTasks_WhenDetailedViewIsDisabled_ReturnsEmptyList()
        {
            WfTicket ticket = new()
            {
                Tasks =
                [
                    new WfReqTask
                    {
                        Id = 1,
                        TaskType = WfTaskType.access.ToString()
                    }
                ]
            };
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.DetailedView), false);
            SetComponentProperty(component, nameof(ReportedTickets.ShowFullTicket), true);

            List<WfReqTask> displayedTasks = (List<WfReqTask>)(InvokePrivateMethod(component, "GetDisplayedTasks", ticket)
                ?? throw new InvalidOperationException("Expected displayed task list."));

            Assert.That(displayedTasks, Is.Empty);
        }
    }
}

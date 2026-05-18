using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Reporting.Reports;
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
            const string labelName = "externalId";
            WfReqTask firstTask = new() { Id = 1 };
            WfReqTask duplicateTask = new() { Id = 2 };
            WfReqTask emptyTask = new() { Id = 3 };
            firstTask.SetAddInfo(labelName, "CR-7");
            duplicateTask.SetAddInfo(labelName, "CR-7");
            emptyTask.SetAddInfo(labelName, "");
            WfTicket ticket = new()
            {
                Tasks = [firstTask, duplicateTask, emptyTask]
            };
            ReportedTickets component = new();
            SetComponentProperty(component, nameof(ReportedTickets.LabelName), labelName);

            string labelValue = (string)(InvokePrivateMethod(component, "GetLabelValue", ticket)
                ?? throw new InvalidOperationException("Expected label value."));

            Assert.That(labelValue, Is.EqualTo("CR-7"));
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

            string labelValue = (string)(InvokePrivateMethod(component, "GetLabelValue", ticket)
                ?? throw new InvalidOperationException("Expected empty label value."));

            Assert.That(labelValue, Is.EqualTo(string.Empty));
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

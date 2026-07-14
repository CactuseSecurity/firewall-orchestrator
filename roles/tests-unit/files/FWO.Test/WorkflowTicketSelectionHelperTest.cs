using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WorkflowTicketSelectionHelperTest
    {
        [Test]
        public void IsInSelectedTimeRange_UsesInclusiveStartAndExclusiveEnd()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(null, "2026-05-01", "2026-06-01"), Is.False);
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(default(DateTime), "2026-05-01", "2026-06-01"), Is.False);
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(new DateTime(2026, 5, 1), "2026-05-01", "2026-06-01"), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(new DateTime(2026, 5, 31, 23, 59, 59), "2026-05-01", "2026-06-01"), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(new DateTime(2026, 6, 1), "2026-05-01", "2026-06-01"), Is.False);
            });
        }

        [Test]
        public void IsInSelectedTimeRange_MissingRangeBoundary_AllowsNonDefaultDate()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(new DateTime(2026, 5, 15), "", "2026-06-01"), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.IsInSelectedTimeRange(new DateTime(2026, 5, 15), "2026-05-01", ""), Is.True);
            });
        }

        [Test]
        public void GetReferenceTasks_AnyActivity_UsesNestedActivities_AndAppliesTaskTypeFilter()
        {
            DateTime rangeStart = new(2026, 4, 1);
            DateTime rangeEnd = new(2026, 4, 30);
            WfReqTask filteredByTaskType = new()
            {
                Id = 1,
                TaskType = WfTaskType.master.ToString(),
                Start = new DateTime(2026, 4, 10)
            };
            WfReqTask keptByApproval = new()
            {
                Id = 5,
                TaskType = WfTaskType.access.ToString(),
                Approvals =
                [
                    new WfApproval
                    {
                        Id = 51,
                        DateOpened = new DateTime(2026, 4, 15)
                    }
                ]
            };
            WfReqTask filteredAfterImplementationMatch = new()
            {
                Id = 2,
                TaskType = WfTaskType.generic.ToString(),
                ImplementationTasks =
                [
                    new WfImplTask
                    {
                        Id = 21,
                        Start = new DateTime(2026, 4, 20)
                    }
                ]
            };
            WfTicket ticket = new()
            {
                Tasks = [filteredByTaskType, keptByApproval, filteredAfterImplementationMatch]
            };

            List<WfReqTask> referenceTasks = WorkflowTicketSelectionHelper.GetReferenceTasks(
                ticket,
                false,
                WorkflowReferenceDate.AnyActivity,
                rangeStart.ToString("yyyy-MM-dd"),
                rangeEnd.ToString("yyyy-MM-dd"),
                [WfTaskType.access]);

            Assert.That(referenceTasks.Select(task => task.Id), Is.EqualTo(new long[] { 5 }));
        }

        [Test]
        public void GetReferenceTasks_ShowFullTicket_IgnoresTaskTypeFilter()
        {
            WfTicket ticket = new()
            {
                Tasks =
                [
                    new WfReqTask
                    {
                        Id = 2,
                        TaskType = WfTaskType.rule_modify.ToString()
                    },
                    new WfReqTask
                    {
                        Id = 1,
                        TaskType = WfTaskType.access.ToString()
                    }
                ]
            };

            List<WfReqTask> referenceTasks = WorkflowTicketSelectionHelper.GetReferenceTasks(
                ticket,
                true,
                WorkflowReferenceDate.AnyActivity,
                null,
                null,
                [WfTaskType.access]);

            Assert.That(referenceTasks.Select(task => task.Id), Is.EqualTo(new long[] { 1, 2 }));
        }

        [Test]
        public void GetReferenceImplementationTasks_FiltersByImplementationEnd_AndOrdersById()
        {
            WfReqTask task = new()
            {
                ImplementationTasks =
                [
                    new WfImplTask { Id = 3, Stop = new DateTime(2026, 5, 14) },
                    new WfImplTask { Id = 1, Stop = new DateTime(2026, 5, 12) },
                    new WfImplTask { Id = 2, Stop = new DateTime(2026, 6, 1) }
                ]
            };

            List<WfImplTask> implementationTasks = WorkflowTicketSelectionHelper.GetReferenceImplementationTasks(
                task,
                false,
                WorkflowReferenceDate.ImplementationEnd,
                "2026-05-01",
                "2026-05-31");

            Assert.That(implementationTasks.Select(implTask => implTask.Id), Is.EqualTo(new long[] { 1, 3 }));
        }

        [Test]
        public void GetReferenceApprovals_FiltersByApprovalDate_AndOrdersById()
        {
            WfReqTask task = new()
            {
                Approvals =
                [
                    new WfApproval { Id = 3, ApprovalDate = new DateTime(2026, 5, 20) },
                    new WfApproval { Id = 1, ApprovalDate = new DateTime(2026, 5, 10) },
                    new WfApproval { Id = 2, ApprovalDate = new DateTime(2026, 6, 1) }
                ]
            };

            List<WfApproval> approvals = WorkflowTicketSelectionHelper.GetReferenceApprovals(
                task,
                false,
                WorkflowReferenceDate.Approved,
                "2026-05-01",
                "2026-05-31");

            Assert.That(approvals.Select(approval => approval.Id), Is.EqualTo(new long[] { 1, 3 }));
        }

        [Test]
        public void GetTicketReferenceDate_AnyActivity_ReturnsLatestTicketOrTaskActivity()
        {
            DateTime creationDate = new(2026, 1, 1);
            DateTime completionDate = new(2026, 1, 5);
            DateTime taskActivityDate = new(2026, 1, 10);
            WfReqTask task = new()
            {
                Id = 1,
                Start = taskActivityDate
            };
            WfTicket ticket = new()
            {
                CreationDate = creationDate,
                CompletionDate = completionDate,
                Tasks = [task]
            };

            DateTime? referenceDate = WorkflowTicketSelectionHelper.GetTicketReferenceDate(
                ticket,
                WorkflowReferenceDate.AnyActivity,
                selectedTicket => selectedTicket.Tasks,
                _ => [],
                _ => [],
                selectedTicket => [selectedTicket.CreationDate, selectedTicket.CompletionDate],
                selectedTask => [selectedTask.Start]);

            Assert.That(referenceDate, Is.EqualTo(taskActivityDate));
        }

        [Test]
        public void GetTicketReferenceDate_Approved_IgnoresOpenApprovals()
        {
            DateTime approvalDate = new(2026, 2, 10);
            WfReqTask task = new()
            {
                Id = 1,
                Approvals =
                [
                    new WfApproval { Id = 1, ApprovalDate = null },
                    new WfApproval { Id = 2, ApprovalDate = approvalDate }
                ]
            };
            WfTicket ticket = new()
            {
                Tasks = [task]
            };

            DateTime? referenceDate = WorkflowTicketSelectionHelper.GetTicketReferenceDate(
                ticket,
                WorkflowReferenceDate.Approved,
                selectedTicket => selectedTicket.Tasks,
                selectedTask => selectedTask.Approvals,
                _ => [],
                _ => [],
                _ => []);

            Assert.That(referenceDate, Is.EqualTo(approvalDate));
        }

        [Test]
        public void ShowNestedDetails_RequiresDetailedView_OrFullTicketOrMatchingReferenceDate()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WorkflowTicketSelectionHelper.ShowImplementationTasks(false, true, ReportType.TicketChangeReport, WorkflowReferenceDate.AnyActivity), Is.False);
                Assert.That(WorkflowTicketSelectionHelper.ShowImplementationTasks(true, true, ReportType.TicketChangeReport, WorkflowReferenceDate.ApprovalOpened), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.ShowImplementationTasks(true, false, ReportType.TicketChangeReport, WorkflowReferenceDate.ImplementationStart), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.ShowImplementationTasks(true, false, ReportType.TicketChangeReport, WorkflowReferenceDate.ApprovalOpened), Is.False);
                Assert.That(WorkflowTicketSelectionHelper.ShowApprovals(false, true, ReportType.TicketChangeReport, WorkflowReferenceDate.AnyActivity), Is.False);
                Assert.That(WorkflowTicketSelectionHelper.ShowApprovals(true, true, ReportType.TicketChangeReport, WorkflowReferenceDate.ImplementationStart), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.ShowApprovals(true, false, ReportType.TicketChangeReport, WorkflowReferenceDate.Approved), Is.True);
                Assert.That(WorkflowTicketSelectionHelper.ShowApprovals(true, false, ReportType.TicketChangeReport, WorkflowReferenceDate.ImplementationStart), Is.False);
            });
        }

        [Test]
        public void GetLabelValue_ReturnsDistinctNonEmptyValues()
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

            string labelValue = WorkflowTicketSelectionHelper.GetLabelValue(ticket, labelName);

            Assert.That(labelValue, Is.EqualTo("CR-7"));
        }
    }
}

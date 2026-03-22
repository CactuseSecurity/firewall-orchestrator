using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report.Filter.FilterTypes;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportFiltersTest
    {
        [Test]
        public void ToReportParams_CopiesWorkflowFilter_ForTicketChangeReport()
        {
            ReportFilters filters = new()
            {
                ReportType = ReportType.TicketChangeReport,
                WorkflowFilter = new() { ReferenceDate = WorkflowReferenceDate.TaskEnd, TaskTypes = [WfTaskType.access, WfTaskType.rule_modify], StateIds = [3, 7], Phase = "implementation", ShowFullTicket = false }
            };

            var reportParams = filters.ToReportParams();

            Assert.That(reportParams.WorkflowFilter.ReferenceDate, Is.EqualTo(WorkflowReferenceDate.TaskEnd));
            Assert.That(reportParams.WorkflowFilter.TaskTypes, Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.rule_modify }));
            Assert.That(reportParams.WorkflowFilter.StateIds, Is.EqualTo(new List<int> { 3, 7 }));
            Assert.That(reportParams.WorkflowFilter.Phase, Is.EqualTo("implementation"));
            Assert.That(reportParams.WorkflowFilter.ShowFullTicket, Is.False);
        }

        [Test]
        public void SyncFiltersFromTemplate_CopiesWorkflowFilter_ForTicketChangeReport()
        {
            ReportFilters filters = new();
            var template = new FWO.Data.Report.ReportTemplate("", new FWO.Data.Report.ReportParams())
            {
                ReportParams =
                {
                    ReportType = (int)ReportType.TicketChangeReport,
                    WorkflowFilter = new() { ReferenceDate = WorkflowReferenceDate.Approved, TaskTypes = [WfTaskType.access, WfTaskType.rule_delete], StateIds = [9], Phase = "review", ShowFullTicket = false }
                }
            };

            filters.SyncFiltersFromTemplate(template);

            Assert.That(filters.ReportType, Is.EqualTo(ReportType.TicketChangeReport));
            Assert.That(filters.WorkflowFilter.ReferenceDate, Is.EqualTo(WorkflowReferenceDate.Approved));
            Assert.That(filters.WorkflowFilter.TaskTypes, Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.rule_delete }));
            Assert.That(filters.WorkflowFilter.StateIds, Is.EqualTo(new List<int> { 9 }));
            Assert.That(filters.WorkflowFilter.Phase, Is.EqualTo("review"));
            Assert.That(filters.WorkflowFilter.ShowFullTicket, Is.False);
        }
    }
}

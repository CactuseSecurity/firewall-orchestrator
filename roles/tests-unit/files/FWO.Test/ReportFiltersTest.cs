using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report.Filter.FilterTypes;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
                WorkflowFilter = new()
                {
                    ReferenceDate = WorkflowReferenceDate.TaskEnd,
                    TaskTypes = [WfTaskType.access, WfTaskType.rule_modify],
                    StateIds = [3, 7],
                    Phase = "implementation",
                    LabelFilter = new() { Name = "policy_check", Mode = WorkflowLabelFilterMode.value, Value = "true" },
                    DetailedView = true,
                    ShowFullTicket = false
                }
            };

            var reportParams = filters.ToReportParams();

            Assert.That(reportParams.WorkflowFilter.ReferenceDate, Is.EqualTo(WorkflowReferenceDate.TaskEnd));
            Assert.That(reportParams.WorkflowFilter.TaskTypes, Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.rule_modify }));
            Assert.That(reportParams.WorkflowFilter.StateIds, Is.EqualTo(new List<int> { 3, 7 }));
            Assert.That(reportParams.WorkflowFilter.Phase, Is.EqualTo("implementation"));
            Assert.That(reportParams.WorkflowFilter.LabelFilter.Name, Is.EqualTo("policy_check"));
            Assert.That(reportParams.WorkflowFilter.LabelFilter.Mode, Is.EqualTo(WorkflowLabelFilterMode.value));
            Assert.That(reportParams.WorkflowFilter.LabelFilter.Value, Is.EqualTo("true"));
            Assert.That(reportParams.WorkflowFilter.DetailedView, Is.True);
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
                    WorkflowFilter = new()
                    {
                        ReferenceDate = WorkflowReferenceDate.Approved,
                        TaskTypes = [WfTaskType.access, WfTaskType.rule_delete],
                        StateIds = [9],
                        Phase = "review",
                        LabelFilter = new() { Name = "policy_check", Mode = WorkflowLabelFilterMode.not_existing },
                        DetailedView = true,
                        ShowFullTicket = false
                    }
                }
            };

            filters.SyncFiltersFromTemplate(template);

            Assert.That(filters.ReportType, Is.EqualTo(ReportType.TicketChangeReport));
            Assert.That(filters.WorkflowFilter.ReferenceDate, Is.EqualTo(WorkflowReferenceDate.Approved));
            Assert.That(filters.WorkflowFilter.TaskTypes, Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.rule_delete }));
            Assert.That(filters.WorkflowFilter.StateIds, Is.EqualTo(new List<int> { 9 }));
            Assert.That(filters.WorkflowFilter.Phase, Is.EqualTo("review"));
            Assert.That(filters.WorkflowFilter.LabelFilter.Name, Is.EqualTo("policy_check"));
            Assert.That(filters.WorkflowFilter.LabelFilter.Mode, Is.EqualTo(WorkflowLabelFilterMode.not_existing));
            Assert.That(filters.WorkflowFilter.LabelFilter.Value, Is.EqualTo(string.Empty));
            Assert.That(filters.WorkflowFilter.DetailedView, Is.True);
            Assert.That(filters.WorkflowFilter.ShowFullTicket, Is.False);
        }

        [Test]
        public void ToReportParams_UsesReducedDeviceFilterForUnusedRulesAndCopiesCreationTolerance()
        {
            SimulatedUserConfig userConfig = new();
            ReportFilters filters = new();
            filters.Init(userConfig, true);
            filters.ReportType = ReportType.UnusedRules;
            filters.ReducedDeviceFilter = new DeviceFilter(
            [
                new ManagementSelect
                {
                    Id = 7,
                    Devices = [new DeviceSelect { Id = 70, Name = "gw70", Selected = true }]
                }
            ]);
            filters.UnusedDays = 21;

            ReportParams reportParams = filters.ToReportParams();

            Assert.That(reportParams.DeviceFilter.Managements, Has.Count.EqualTo(1));
            Assert.That(reportParams.DeviceFilter.Managements[0].Id, Is.EqualTo(7));
            Assert.That(reportParams.UnusedFilter.UnusedForDays, Is.EqualTo(21));
            Assert.That(reportParams.UnusedFilter.CreationTolerance, Is.EqualTo(userConfig.CreationTolerance));
        }

        [Test]
        public void ToReportParams_UsesTenantFromUserWhenNoTenantSelected()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Tenant = new Tenant { Id = 4 };
            ReportFilters filters = new()
            {
                ReportType = ReportType.Rules
            };
            filters.Init(userConfig, true);

            ReportParams reportParams = filters.ToReportParams();

            Assert.That(reportParams.TenantFilter.IsActive, Is.True);
            Assert.That(reportParams.TenantFilter.TenantId, Is.EqualTo(4));
        }

        [Test]
        public void SetDisplayedTimeSelection_ForTicketChangeReportOpenEnd_UsesFromText()
        {
            SimulatedUserConfig userConfig = new();
            ReportFilters filters = new();
            filters.Init(userConfig, true);
            filters.ReportType = ReportType.TicketChangeReport;
            filters.TimeFilter = new()
            {
                TimeRangeType = TimeRangeType.Fixeddates,
                OpenEnd = true,
                StartTime = new DateTime(2025, 1, 2, 3, 4, 5)
            };

            filters.SetDisplayedTimeSelection();

            StringAssert.StartsWith("from ", filters.DisplayedTimeSelection);
            StringAssert.Contains(filters.TimeFilter.StartTime.ToString(), filters.DisplayedTimeSelection);
        }
    }
}

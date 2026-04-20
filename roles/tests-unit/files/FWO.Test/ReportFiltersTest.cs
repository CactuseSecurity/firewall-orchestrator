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
        public void Init_SetsDefaultsAndCollapsesWhenDeviceCountExceedsThreshold()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.MinCollapseAllDevices = 1;
            ReportFilters filters = new();
            filters.DeviceFilter = new DeviceFilter(
            [
                new ManagementSelect { Id = 1 },
                new ManagementSelect { Id = 2 }
            ]);

            filters.Init(userConfig, showRuleRelatedReports: false);

            Assert.That(filters.ReportType, Is.EqualTo(ReportType.Connections));
            Assert.That(filters.DisplayedTimeSelection, Is.EqualTo("now"));
            Assert.That(filters.UnusedDays, Is.EqualTo(userConfig.UnusedTolerance));
            Assert.That(filters.CollapseDevices, Is.True);
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
                    },
                    IncludeObjects = true
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
            Assert.That(filters.IncludeObjects, Is.True);
        }

        [Test]
        public void ToReportParams_CopiesIncludeObjectChangesSettings_ForChangesReport()
        {
            ReportFilters filters = new()
            {
                ReportType = ReportType.Changes,
                IncludeObjects = true
            };

            ReportParams reportParams = filters.ToReportParams();

            Assert.That(reportParams.IncludeObjects, Is.True);
        }

        [Test]
        public void Init_CopiesGlobalDefaultForIncludeObjectChanges()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.GlobalConfig = new() { ImpChangeIncludeObjectChanges = true };
            ReportFilters filters = new();

            filters.Init(userConfig, true);

            Assert.That(filters.IncludeObjects, Is.True);
        }

        [Test]
        public void SyncFiltersFromTemplate_CopiesDeviceFilterAndUpdatesSelectAll()
        {
            ReportFilters filters = new()
            {
                DeviceFilter = new DeviceFilter(
                [
                    new ManagementSelect
                    {
                        Id = 7,
                        Devices = [new DeviceSelect { Id = 70, Name = "gw70" }]
                    }
                ])
            };
            SimulatedUserConfig userConfig = new();
            filters.Init(userConfig, true);
            var template = new FWO.Data.Report.ReportTemplate("", new FWO.Data.Report.ReportParams())
            {
                ReportParams =
                {
                    ReportType = (int)ReportType.Rules,
                    DeviceFilter = new DeviceFilter(
                    [
                        new ManagementSelect
                        {
                            Id = 7,
                            Selected = true,
                            Devices = [new DeviceSelect { Id = 70, Name = "gw70", Selected = true }]
                        }
                    ])
                }
            };

            filters.SyncFiltersFromTemplate(template);

            Assert.That(filters.DeviceFilter.Managements[0].Selected, Is.True);
            Assert.That(filters.DeviceFilter.Managements[0].Devices[0].Selected, Is.True);
            Assert.That(filters.SelectAll, Is.False);
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
        public void ToReportParams_CopiesOwnerFilter_ForOwnersReport()
        {
            ReportFilters filters = new()
            {
                ReportType = ReportType.Owners,
                OwnerFilter = new OwnerFilter
                {
                    SelectedOwnerLifeCycleStateId = 3,
                    SelectedCriticality = "High"
                }
            };

            ReportParams reportParams = filters.ToReportParams();

            Assert.That(reportParams.OwnerFilter.SelectedOwnerLifeCycleStateId, Is.EqualTo(3));
            Assert.That(reportParams.OwnerFilter.SelectedCriticality, Is.EqualTo("High"));
            Assert.That(ReferenceEquals(reportParams.OwnerFilter, filters.OwnerFilter), Is.False);
        }

        [Test]
        public void SyncFiltersFromTemplate_CopiesOwnerFilter_ForOwnersReport()
        {
            ReportFilters filters = new();
            var template = new FWO.Data.Report.ReportTemplate("", new FWO.Data.Report.ReportParams())
            {
                ReportParams =
                {
                    ReportType = (int)ReportType.Owners,
                    OwnerFilter = new OwnerFilter
                    {
                        SelectedOwnerLifeCycleStateId = 5,
                        SelectedCriticality = "Medium"
                    }
                }
            };

            filters.SyncFiltersFromTemplate(template);

            Assert.That(filters.ReportType, Is.EqualTo(ReportType.Owners));
            Assert.That(filters.OwnerFilter.SelectedOwnerLifeCycleStateId, Is.EqualTo(5));
            Assert.That(filters.OwnerFilter.SelectedCriticality, Is.EqualTo("Medium"));
            Assert.That(ReferenceEquals(filters.OwnerFilter, template.ReportParams.OwnerFilter), Is.False);
        }

        [Test]
        public void ToReportParams_PreservesExplicitTemplateOwner_ForAppRules()
        {
            ReportFilters filters = new()
            {
                ReportType = ReportType.AppRules
            };
            filters.ModellingFilter.SelectedOwner = new FwoOwner { Id = 17, Name = "App A" };
            filters.ModellingFilter.SelectedTemplateOwner = new FwoOwner { Id = 23, Name = "Template App" };

            ReportParams reportParams = filters.ToReportParams();

            Assert.That(reportParams.ModellingFilter.SelectedOwner.Id, Is.EqualTo(17));
            Assert.That(reportParams.ModellingFilter.SelectedTemplateOwner.Id, Is.EqualTo(23));
            Assert.That(reportParams.ModellingFilter.SelectedTemplateOwner.Name, Is.EqualTo("Template App"));
        }

        [Test]
        public void ToReportParams_CopiesVarianceFlags()
        {
            ReportFilters filters = new()
            {
                ReportType = ReportType.VarianceAnalysis
            };
            filters.ModellingFilter.RulesForDeletedConns = true;
            filters.ModellingFilter.AnalyseRemainingRules = true;

            ReportParams reportParams = filters.ToReportParams();

            Assert.That(reportParams.ModellingFilter.RulesForDeletedConns, Is.True);
            Assert.That(reportParams.ModellingFilter.AnalyseRemainingRules, Is.True);
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

        [Test]
        public void SetDisplayedTimeSelection_ForChangeReportShortcut_UsesTranslatedShortcut()
        {
            SimulatedUserConfig userConfig = new();
            ReportFilters filters = new();
            filters.Init(userConfig, true);
            filters.ReportType = ReportType.Changes;
            filters.TimeFilter = new()
            {
                TimeRangeType = TimeRangeType.Shortcut,
                TimeRangeShortcut = "today"
            };

            filters.SetDisplayedTimeSelection();

            Assert.That(filters.DisplayedTimeSelection, Is.EqualTo("today"));
        }

        [Test]
        public void SetDisplayedTimeSelection_ForNonChangeShortcut_UsesTranslatedShortcut()
        {
            SimulatedUserConfig userConfig = new();
            ReportFilters filters = new();
            filters.Init(userConfig, true);
            filters.ReportType = ReportType.Rules;
            filters.TimeFilter = new()
            {
                IsShortcut = true,
                TimeShortcut = "now"
            };

            filters.SetDisplayedTimeSelection();

            Assert.That(filters.DisplayedTimeSelection, Is.EqualTo("now"));
        }

        [Test]
        public void SetDisplayedTimeSelection_ForNonChangeFixedTime_UsesReportTime()
        {
            SimulatedUserConfig userConfig = new();
            ReportFilters filters = new();
            filters.Init(userConfig, true);
            filters.ReportType = ReportType.Rules;
            filters.TimeFilter = new()
            {
                IsShortcut = false,
                ReportTime = new DateTime(2025, 1, 2, 3, 4, 5)
            };

            filters.SetDisplayedTimeSelection();

            Assert.That(filters.DisplayedTimeSelection, Is.EqualTo(filters.TimeFilter.ReportTime.ToString()));
        }

        [Test]
        public void TenantViewChanged_WithTenantZero_MarksAllDevicesVisible()
        {
            ReportFilters filters = new()
            {
                DeviceFilter = new DeviceFilter(
                [
                    new ManagementSelect
                    {
                        Id = 7,
                        Visible = false,
                        Shared = true,
                        Devices =
                        [
                            new DeviceSelect { Id = 70, Name = "gw70", Visible = false, Shared = true },
                            new DeviceSelect { Id = 71, Name = "gw71", Visible = false, Shared = true }
                        ]
                    }
                ])
            };

            filters.TenantViewChanged(new Tenant { Id = 1 });

            Assert.That(filters.DeviceFilter.Managements[0].Visible, Is.True);
            Assert.That(filters.DeviceFilter.Managements[0].Shared, Is.False);
            Assert.That(filters.DeviceFilter.Managements[0].Devices.All(device => device.Visible && !device.Shared), Is.True);
            Assert.That(filters.SelectAll, Is.True);
        }

        [Test]
        public void TenantViewChanged_WithRestrictedTenant_HidesUnlistedDevicesAndClearsSelection()
        {
            ReportFilters filters = new()
            {
                DeviceFilter = new DeviceFilter(
                [
                    new ManagementSelect
                    {
                        Id = 7,
                        Selected = true,
                        Devices =
                        [
                            new DeviceSelect { Id = 70, Name = "gw70", Selected = true },
                            new DeviceSelect { Id = 71, Name = "gw71", Selected = true }
                        ]
                    }
                ])
            };

            filters.TenantViewChanged(new Tenant
            {
                Id = 2,
                VisibleGatewayIds = [70]
            });

            Assert.That(filters.DeviceFilter.Managements[0].Visible, Is.True);
            Assert.That(filters.DeviceFilter.Managements[0].Shared, Is.True);
            Assert.That(filters.DeviceFilter.Managements[0].Devices[0].Visible, Is.True);
            Assert.That(filters.DeviceFilter.Managements[0].Devices[1].Visible, Is.False);
            Assert.That(filters.DeviceFilter.Managements[0].Devices[1].Selected, Is.False);
        }

        [Test]
        public void DeviceFilter_ListAllSelectedManagements_ReturnsSelectedManagementNames()
        {
            DeviceFilter deviceFilter = new(
            [
                new ManagementSelect
                {
                    Id = 7,
                    Name = "Mgmt A",
                    Devices =
                    [
                        new DeviceSelect { Id = 70, Name = "gw70", Selected = true },
                        new DeviceSelect { Id = 71, Name = "gw71", Selected = false }
                    ]
                },
                new ManagementSelect
                {
                    Id = 8,
                    Name = "Mgmt B",
                    Devices =
                    [
                        new DeviceSelect { Id = 80, Name = "gw80", Selected = true }
                    ]
                },
                new ManagementSelect
                {
                    Id = 9,
                    Name = "Mgmt C",
                    Devices =
                    [
                        new DeviceSelect { Id = 90, Name = "gw90", Selected = false }
                    ]
                }
            ]);

            Assert.That(deviceFilter.ListAllSelectedManagements(), Is.EqualTo("Mgmt A, Mgmt B"));
        }
    }
}

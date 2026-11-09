using FWO.Data.Report;
using FWO.Config.Api;
using FWO.Basics;
using FWO.Data;


namespace FWO.Report.Filter.FilterTypes
{
    public class ReportFilters
    {
        public ReportType ReportType { get; set; } = ReportType.Rules;

        public DeviceFilter DeviceFilter { get; set; } = new();
        public bool ManagementRulebaseView { get; set; }
        public List<RulebaseManagementSelect> RulebaseManagements { get; set; } = [];
        public bool RulebaseManagementsLoaded { get; private set; }
        public List<SelectedRulebase> SelectedRulebases { get; set; } = [];
        public DeviceFilter ReducedDeviceFilter { get; set; } = new();
        public bool SelectAll = true;
        public bool CollapseDevices = false;

        public TimeFilter TimeFilter { get; set; } = new();
        public TimeFilter SavedTimeFilter { get; set; } = new();

        public TenantFilter TenantFilter { get; set; } = new();
        public Tenant? SelectedTenant = null;

        public RecertFilter RecertFilter { get; set; } = new();

        public UnusedFilter UnusedFilter { get; set; } = new();
        public int UnusedDays = 0;

        public ModellingFilter ModellingFilter { get; set; } = new();
        public OwnerFilter OwnerFilter { get; set; } = new();

        public ComplianceFilter ComplianceFilter { get; set; } = new();

        public WorkflowFilter WorkflowFilter { get; set; } = new();

        public string DisplayedTimeSelection = "";

        private UserConfig? userConfig;

        public bool IncludeObjects { get; set; } = false;

        /// <summary>Initializes filters from the effective user configuration.</summary>
        public void Init(UserConfig userConfigIn, bool showRuleRelatedReports)
        {
            userConfig = userConfigIn;
            ReportType = showRuleRelatedReports ? ReportType.Rules : ReportType.Connections;
            DisplayedTimeSelection = userConfig.GetText("now");
            UnusedDays = userConfig.UnusedTolerance;
            ManagementRulebaseView = userConfig.DefaultManagementRulebaseView;
            IncludeObjects = userConfig.GlobalConfig?.ImpChangeIncludeObjectChanges ?? false;

            if (DeviceFilter.NumberMgmtDev() > userConfig.MinCollapseAllDevices)
            {
                CollapseDevices = true;
            }
        }

        public void SyncFiltersFromTemplate(ReportTemplate template)
        {
            ReportType = (ReportType)template.ReportParams.ReportType;
            ManagementRulebaseView = template.ReportParams.ManagementRulebaseView;
            SelectedRulebases = RulebaseSelectionHelper.KeepAvailable(template.ReportParams.SelectedRulebases, RulebaseManagements);
            IncludeObjects = template.ReportParams.IncludeObjects;
            if (template.ReportParams.DeviceFilter != null && template.ReportParams.DeviceFilter.Managements.Count > 0)
            {
                DeviceFilter.SynchronizeDevFilter(template.ReportParams.DeviceFilter);
            }
            SelectAll = !DeviceFilter.IsAnyDeviceFilterSet();

            if (template.ReportParams.TimeFilter != null)
            {
                TimeFilter = new TimeFilter(template.ReportParams.TimeFilter);
                SavedTimeFilter = new TimeFilter(template.ReportParams.TimeFilter);
            }
            SetDisplayedTimeSelection();
            RecertFilter = new(template.ReportParams.RecertFilter);
            UnusedDays = template.ReportParams.UnusedFilter.UnusedForDays;
            ModellingFilter = template.ReportParams.ModellingFilter;
            OwnerFilter = new(template.ReportParams.OwnerFilter);
            ComplianceFilter = new(template.ReportParams.ComplianceFilter);
            WorkflowFilter = new(template.ReportParams.WorkflowFilter);
        }

        public ReportParams ToReportParams()
        {
            ReportParams reportParams = new((int)ReportType, ReportType == ReportType.UnusedRules ? ReducedDeviceFilter : DeviceFilter)
            {
                IncludeObjects = IncludeObjects,
                ManagementRulebaseView = ReportType == ReportType.Rules && ManagementRulebaseView,
                SelectedRulebases = [.. SelectedRulebases],
                TimeFilter = new TimeFilter(SavedTimeFilter),
                RecertFilter = new RecertFilter(RecertFilter),
                UnusedFilter = new UnusedFilter()
                {
                    UnusedForDays = UnusedDays,
                    CreationTolerance = userConfig?.CreationTolerance ?? 0
                },
                ModellingFilter = new ModellingFilter(ModellingFilter),
                OwnerFilter = new OwnerFilter(OwnerFilter),
                ComplianceFilter = new ComplianceFilter(ComplianceFilter),
                WorkflowFilter = new WorkflowFilter(WorkflowFilter)
            };
            if (ReportType != ReportType.Statistics)
            {
                // also make sure the report a user belonging to a tenant <> 1 sees, gets the additional filters in DynGraphqlQuery.cs
                if (SelectedTenant == null && userConfig?.User.Tenant?.Id > 1)
                {
                    SelectedTenant = userConfig.User.Tenant;
                    // TODO: when admin selects a tenant filter, add the corresponding device filter to make sure only those devices are reported that the tenant is allowed to see
                }
                reportParams.TenantFilter = new TenantFilter(SelectedTenant);
            }
            return reportParams;
        }

        public bool SetDisplayedTimeSelection()
        {
            if (ReportType.IsChangeReport() || ReportType == ReportType.TicketChangeReport)
            {
                switch (TimeFilter.TimeRangeType)
                {
                    case TimeRangeType.Shortcut:
                        DisplayedTimeSelection = userConfig?.GetText(TimeFilter.TimeRangeShortcut) ?? TimeFilter.TimeRangeShortcut;
                        break;
                    case TimeRangeType.Interval:
                        DisplayedTimeSelection = userConfig?.GetText("last") + " " +
                            TimeFilter.Offset + " " + userConfig?.GetText(TimeFilter.Interval.ToString());
                        break;
                    case TimeRangeType.Fixeddates:
                        if (TimeFilter.OpenStart && TimeFilter.OpenEnd)
                        {
                            DisplayedTimeSelection = userConfig?.GetText("open") ?? "open";
                        }
                        else if (TimeFilter.OpenStart)
                        {
                            DisplayedTimeSelection = userConfig?.GetText("until") + " " + TimeFilter.EndTime.ToString();
                        }
                        else if (TimeFilter.OpenEnd)
                        {
                            DisplayedTimeSelection = userConfig?.GetText("from") + " " + TimeFilter.StartTime.ToString();
                        }
                        else
                        {
                            DisplayedTimeSelection = TimeFilter.StartTime.ToString() + " - " + TimeFilter.EndTime.ToString();
                        }
                        break;
                    default:
                        DisplayedTimeSelection = "";
                        break;
                }
                ;
            }
            else
            {
                if (TimeFilter.IsShortcut)
                {
                    DisplayedTimeSelection = userConfig?.GetText(TimeFilter.TimeShortcut) ?? TimeFilter.TimeShortcut;
                }
                else
                {
                    DisplayedTimeSelection = TimeFilter.ReportTime.ToString();
                }
            }
            return true;
        }

        /// sets deviceFilter.Managements and selectedTenant according to either
        /// a) selected tenant for tenant simulation
        /// b) tenant of the user logged in (if belonging to tenant <> tenant0)
        public void TenantViewChanged(Tenant? newTenantView)
        {
            SelectedTenant = newTenantView;

            // we must modify the device visibility in the device filter
            if (SelectedTenant == null || SelectedTenant.Id == 1)
            {
                // tenant0 or no tenant selected --> all devices are visible            
                MarkAllDevicesVisible(DeviceFilter.Managements);
            }
            else
            {
                // not all devices are visible
                SetDeviceVisibility(SelectedTenant);
            }
            SetRulebaseManagementVisibility();
            SelectAll = !DeviceFilter.IsAnyDeviceFilterSet();
        }

        /// <summary>
        /// Sets the start rulebases offered in management rulebases view (loaded on first use)
        /// and applies the current tenant view to them.
        /// </summary>
        /// <param name="managements">managements with all active rulebases and their incoming links</param>
        public void SetRulebaseManagements(List<RulebaseManagementSelect> managements)
        {
            RulebaseManagements = RulebaseManagementSelect.KeepStartRulebases(managements);
            RulebaseManagementsLoaded = true;
            SetRulebaseManagementVisibility();
        }

        /// <summary>
        /// Shows rulebases of a management only if the management is visible in the current tenant view
        /// and drops selected rulebases that are no longer visible.
        /// </summary>
        private void SetRulebaseManagementVisibility()
        {
            bool allVisible = SelectedTenant == null || SelectedTenant.Id == 1;
            foreach (RulebaseManagementSelect management in RulebaseManagements)
            {
                management.Visible = allVisible || DeviceFilter.Managements.Any(mgm => mgm.Id == management.Id && mgm.Visible);
            }
            SelectedRulebases = RulebaseSelectionHelper.KeepAvailable(SelectedRulebases, RulebaseManagements);
        }

        private static void MarkAllDevicesVisible(List<ManagementSelect> mgms)
        {
            foreach (ManagementSelect management in mgms)
            {
                management.Visible = true;
                management.Shared = false;
                foreach (DeviceSelect gw in management.Devices)
                {
                    gw.Visible = true;
                    gw.Shared = false;
                }
            }
        }

        private void SetDeviceVisibility(Tenant tenantView)
        {
            if ((userConfig == null || userConfig.User.Tenant == null || userConfig.User.Tenant.Id == 1) && tenantView.Id != 1)
            {
                // filtering for tenant simulation only done by a tenant0 user
                foreach (TenantGateway gw in tenantView.TenantGateways)
                {
                    if (!tenantView.VisibleGatewayIds.Contains(gw.VisibleGateway.Id))
                    {
                        tenantView.VisibleGatewayIds = [.. tenantView.VisibleGatewayIds, gw.VisibleGateway.Id];
                    }
                }

                // also add all gateways of non-shared managments - necessary for simulated tenant filtering
                foreach (TenantManagement mgm in tenantView.TenantManagements)
                {
                    if (!mgm.Shared)
                    {
                        foreach (Device gw in mgm.VisibleManagement.Devices)
                        {
                            if (!tenantView.VisibleGatewayIds.Contains(gw.Id))
                            {
                                tenantView.VisibleGatewayIds = [.. tenantView.VisibleGatewayIds, gw.Id];
                            }
                        }
                    }
                }
            }

            foreach (ManagementSelect mgm in DeviceFilter.Managements)
            {
                mgm.Shared = false;
                bool mgmVisible = false;
                foreach (DeviceSelect gw in mgm.Devices)
                {
                    gw.Visible = tenantView.VisibleGatewayIds.Contains(gw.Id);
                    if (gw.Visible)
                    {
                        // one gateway is visible, so the management must be visible
                        mgmVisible = true;
                    }
                    else
                    {
                        gw.Selected = false; // make sure invisible devices are not selected
                        mgm.Shared = true; // if one gateway is not visible, the mgm is shared (filtered)
                    }
                }
                mgm.Visible = mgmVisible;
                if (!mgm.Visible)
                {   // make sure invisible managements are not selected
                    mgm.Selected = false;
                }
            }
        }
    }
}

using FWO.Api.Data;
using FWO.Config.Api;

namespace FWO.Report.Filter
{
    public class ReportFilters
    {
        public ReportType ReportType { get; set; } = ReportType.Rules;

        public DeviceFilter DeviceFilter { get; set; } = new DeviceFilter();
        public DeviceFilter ReducedDeviceFilter { get; set; } = new DeviceFilter();
        public bool SelectAll = true;
        public bool CollapseDevices = false;

        public TimeFilter TimeFilter { get; set; } = new TimeFilter();
        public TimeFilter SavedTimeFilter { get; set; } = new TimeFilter();

        public TenantFilter TenantFilter { get; set; } = new TenantFilter();
        public Tenant? SelectedTenant = null;

        public RecertFilter RecertFilter { get; set; } = new RecertFilter();

        public UnusedFilter UnusedFilter { get; set; } = new UnusedFilter();
        public int UnusedDays = 0;

        public string DisplayedTimeSelection = "";

        private UserConfig userConfig;


        public ReportFilters()
        {}

        public void Init(UserConfig userConfigIn, bool showRuleRelatedReports)
        {
            userConfig = userConfigIn;
            ReportType = showRuleRelatedReports ? ReportType.Rules : ReportType.Connections;
            DisplayedTimeSelection = userConfig.GetText("now");
            UnusedDays = userConfig.UnusedTolerance;

            if (DeviceFilter.NumberMgmtDev() > userConfig.MinCollapseAllDevices)
            {
                CollapseDevices = true;
            }
        }

        public void SyncFiltersFromTemplate(ReportTemplate template)
        {
            ReportType = (ReportType)template.ReportParams.ReportType;
            if(template.ReportParams.DeviceFilter != null && template.ReportParams.DeviceFilter.Managements.Count > 0)
            {
                DeviceFilter.SynchronizeDevFilter(template.ReportParams.DeviceFilter);
            }
            SelectAll = !DeviceFilter.isAnyDeviceFilterSet();

            if(template.ReportParams.TimeFilter != null)
            {
                TimeFilter = template.ReportParams.TimeFilter;
            }
            SetDisplayedTimeSelection();
            RecertFilter = new(template.ReportParams.RecertFilter);
            UnusedDays = template.ReportParams.UnusedFilter.UnusedForDays;
        }

        public ReportParams ToReportParams()
        {
            ReportParams reportParams = new ReportParams((int)ReportType, ReportType == ReportType.UnusedRules ? ReducedDeviceFilter : DeviceFilter)
            {
                TimeFilter = SavedTimeFilter,
                RecertFilter = new RecertFilter(RecertFilter),
                UnusedFilter = new UnusedFilter() 
                {
                    UnusedForDays = UnusedDays, 
                    CreationTolerance = userConfig.CreationTolerance
                }
            };
            if (ReportType != ReportType.Statistics)
            {
                // also make sure the report a user belonging to a tenant <> 1 sees, gets the additional filters in DynGraphqlQuery.cs
                if (SelectedTenant == null && userConfig.User.Tenant.Id > 1)
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
            if (ReportType.IsChangeReport())
            {
                switch (TimeFilter.TimeRangeType)
                {
                    case TimeRangeType.Shortcut:
                        DisplayedTimeSelection = userConfig.GetText(TimeFilter.TimeRangeShortcut);
                        break;
                    case TimeRangeType.Interval:
                        DisplayedTimeSelection = userConfig.GetText("last") + " " + 
                            TimeFilter.Offset + " " + userConfig.GetText(TimeFilter.Interval.ToString());
                        break;
                    case TimeRangeType.Fixeddates:
                        if(TimeFilter.OpenStart && TimeFilter.OpenEnd)
                        {
                            DisplayedTimeSelection = userConfig.GetText("open");
                        }
                        else if(TimeFilter.OpenStart)
                        {
                            DisplayedTimeSelection = userConfig.GetText("until") + " " + TimeFilter.EndTime.ToString();
                        }
                        else if(TimeFilter.OpenEnd)
                        {
                            DisplayedTimeSelection = userConfig.GetText("from") + " " + TimeFilter.StartTime.ToString();
                        }
                        else
                        {
                            DisplayedTimeSelection = TimeFilter.StartTime.ToString() + " - " + TimeFilter.EndTime.ToString();
                        }
                        break;
                    default:
                        DisplayedTimeSelection = "";
                        break;
                };
            }
            else
            {
                if (TimeFilter.IsShortcut)
                {
                    DisplayedTimeSelection = userConfig.GetText(TimeFilter.TimeShortcut);
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
            if (SelectedTenant==null || SelectedTenant.Id == 1)
            {
            // tenant0 or no tenant selected --> all devices are visible            
                MarkAllDevicesVisible(DeviceFilter.Managements);
            }
            else
            {
                // not all devices are visible
                SetDeviceVisibility(SelectedTenant);
            }
            SelectAll = !DeviceFilter.isAnyDeviceFilterSet();
        }
        
        private void MarkAllDevicesVisible(List<ManagementSelect> mgms)
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
            if ((userConfig.User.Tenant.Id==null || userConfig.User.Tenant.Id==1) && tenantView.Id!=1)
            {
                // filtering for tenant simulation only done by a tenant0 user
                foreach (TenantGateway gw in tenantView.TenantGateways)
                {
                    if (!tenantView.VisibleGatewayIds.Contains(gw.VisibleGateway.Id))
                    {
                        tenantView.VisibleGatewayIds.Append(gw.VisibleGateway.Id);
                        tenantView.VisibleGatewayIds = tenantView.VisibleGatewayIds.Concat(new int[] { gw.VisibleGateway.Id }).ToArray();
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
                                tenantView.VisibleGatewayIds.Append(gw.Id);
                                tenantView.VisibleGatewayIds = tenantView.VisibleGatewayIds.Concat(new int[] { gw.Id }).ToArray();
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

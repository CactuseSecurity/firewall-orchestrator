using System.Text.RegularExpressions;
using FWO.Api.Data;

namespace FWO.Report.Filter
{
    public class DeviceFilter
    {
        public static List<int> ExtractAllDevIds(Management[] managements)
        {
            List<int> devs = new List<int>();
            foreach (Management mgmt in managements)
                foreach (Device dev in mgmt.Devices)
                    devs.Add(dev.Id);
            return devs;
        }

        public static List<int> ExtractSelectedDevIds(Management[] managements)
        {
            List<int> selectedDevs = new List<int>();
            foreach (Management mgmt in managements)
                foreach (Device dev in mgmt.Devices)
                    if (dev.Selected)
                        selectedDevs.Add(dev.Id);
            return selectedDevs;
        }

        public static bool IsSelectedManagement(Management management)
        {
            foreach (Device dev in management.Devices)
            {
                if (dev.Selected)
                {
                    return true;
                }
            }     
            return false;
        }

        public static List<int> getSelectedManagements(Management[] managements)
        {
            List<int> selectedMgmts = new List<int>();
            foreach (Management mgmt in managements)
            {
                if (IsSelectedManagement(mgmt))
                {
                    selectedMgmts.Add(mgmt.Id);
                }
            }
            return selectedMgmts;
        }

        public Management[] saveSelectedState(Management[] managements)
        {
            return managements;
        }

        public static Management[] restoreSelectedState(Management[] selectedState, Management[] managements)
        {
            int mIdx;
            int dIdx;
            for (mIdx = 0; mIdx <= managements.Length; ++mIdx)
                if (managements.Length > mIdx && managements[mIdx].Devices != null)
                    for (dIdx = 0; dIdx <= managements[mIdx].Devices.Length; ++dIdx)
                    {
                        if (managements[mIdx].Devices.Length > dIdx && managements[mIdx].Devices[dIdx] != null)
                            managements[mIdx].Devices[dIdx].Selected = selectedState[mIdx].Devices[dIdx].Selected;
                    }
            return managements;
        }

        public static bool areAllDevicesSelected(Management[] managements)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        if (device != null)
                            if (!device.Selected)
                                return false;
            return true;
        }

        public static bool isAnyLSBDeviceFilterSet(Management[] managements)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        if (device != null)
                            if (device.Selected)
                                return true;
            return false;
        }

        public static bool isAnyDeviceFilterSet(Management[] managements)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        if (device != null)
                            if (device.Selected)
                                return true;
            return false;
        }

        public static bool isAnyDeviceFilterSet(DynGraphqlQuery query)
        {
            // check if filterline contains a device filter
            if (query.FullQuery.Contains("device: {dev_name : {_ilike:") || query.FullQuery.Contains("management: {mgm_name : {_ilike:"))
                return true;
            return false;
        }

        public static bool isAnyDeviceFilterSet(string filterLine)
        {
            Match m = Regex.Match(filterLine, @"gateway|gw|device|firewall");
            if (m.Success)
                return true;
            else
                return false;
        }

        public static bool isAnyDeviceFilterSet(Management[] managements, DynGraphqlQuery query)
        {
            return isAnyDeviceFilterSet(managements) || isAnyDeviceFilterSet(query);
        }

        public static bool isAnyDeviceFilterSet(Management[] managements, string filterLine)
        {
            return isAnyDeviceFilterSet(managements) || isAnyDeviceFilterSet(filterLine);
        }

        /// <summary>
        /// apply all device action (either clear all device selections or set them all)
        /// </summary>
        public static void applyFullDeviceSelection(Management[] managements, bool selectAll)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        device.Selected = selectAll;
        }

        /// <summary>
        /// removes all residue remaining after gw filters have been removed
        /// </summary>
        public static string cleanFilter(string filter)
        {
            bool match = true;
            filter = filter.Trim().ToLower();
            string[] patterns = { @"(\(\s*or\s*\))", @"(and\s*\(\))", @"(^\s*and\s+)", @"\s*(and\s*$)", @"(\(\s*\))" };
            while (match)
            {
                match = false;
                foreach (string pattern in patterns)
                {
                    Match m = Regex.Match(filter, pattern);
                    if (m.Success)
                    {
                        match = true;
                        int matchLength = m.Value.Length;
                        int matchPosition = m.Index;
                        filter = filter.Remove(matchPosition, matchLength);
                    }
                }
            }
            // finally removing duplicate ands:
            Match m2 = Regex.Match(filter, @"(and\s+and)");
            if (m2.Success)
                filter = filter.Remove(m2.Index, 4);
            return filter.Trim();
        }

        /// <summary>
        /// removes all device & management filters from filter line and adds current dev filter from left side bar
        /// </summary>
        public static string syncLSBFilterToFilterLine(Management[] managements, string filterLine)
        {
            // remove all traces of device filterin from filter line before copying from LSB
            filterLine = filterLine.ToLower();
            bool match = true;
            while (match)
            {
                Match m = Regex.Match(filterLine, @"(or|and)?\s*(gateway|gw|device|firewall)(\s*\=\=?\s*""?)([\w\-\s]+""?)\s*(or|and)?");
                if (!m.Success)
                    match = false;
                else
                {
                    // remove the gw expression from filter line
                    int matchLength = m.Value.Length;
                    int matchPosition = m.Index;
                    filterLine = filterLine.Remove(matchPosition, matchLength);
                }
            }
            string filterWithoutDev = cleanFilter(filterLine); // removing syntax issues that remain after removing gw filter parts
            string devFilter = "";

            foreach (Management mgm in managements)
                foreach (Device dev in mgm.Devices)
                    if (dev.Selected)
                        devFilter += $"gateway=\"{dev.Name}\" or ";

            if (devFilter.Length > 0)
                devFilter = $"({devFilter.Remove(devFilter.Length - 4)})"; // remove final 4 chars " or "
            if (filterWithoutDev.Length > 0 && devFilter.Length > 0)
                devFilter = $" and {devFilter}";
            return filterWithoutDev + devFilter;
        }

        /// <summary>
        /// clears current dev filter from left side bar and sets it to device & management filters from filter line
        //// returns either empty string or the new text for the "select/clear all" button
        /// </summary>
        public static void syncFilterLineToLSBFilter(string currentFilterLine, Management[] LSBFilter)
        {
            List<string> filteredGatewayList = new List<string>();
            List<string> gatewayList = new List<string>();

            // clear device filter first:
            foreach (Management mgmt in LSBFilter)
            {
                if (mgmt.Devices != null)
                {
                    foreach (Device device in mgmt.Devices)
                    {
                        gatewayList.Add(device.Name != null ? device.Name : "");
                        device.Selected = false;
                    }
                }
            }

            // find gw filter in filter string and perform pattern matching against gw names 
            string pattern = @"(gateway|gw|device|firewall)\s*\=\=?\s*""?([\w\-]+)""?";
            Regex gwFilterRgx = new Regex(pattern);
            string filterLine = currentFilterLine.ToLower();

            foreach (Match gwExpressionMatch in gwFilterRgx.Matches(filterLine))
            {
                Regex gwRgx = new Regex($@"{gwExpressionMatch.Groups[2].Value}");
                foreach (string gw in gatewayList)
                {
                    Match m = gwRgx.Match(gw.ToLower());
                    if (m.Success)
                        filteredGatewayList.Add(gw.ToLower());
                }
            }

            // now set all gateways to selected that are mentioned in currentFilterLine
            foreach (Management mgmt in LSBFilter)
            {
                if (mgmt.Devices != null)
                {
                    foreach (Device device in mgmt.Devices)
                    {
                        if (filteredGatewayList.Contains(device.Name != null ? device.Name.ToLower() : ""))
                            device.Selected = true;
                    }
                }
            }

            // if (!DeviceFilter.isAnyLSBDeviceFilterSet(LSBFilter))
            //     return true;
            // if (DeviceFilter.areAllDevicesSelected(LSBFilter))
            //     return false;
        }
    }
}

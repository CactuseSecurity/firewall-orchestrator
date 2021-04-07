using System;
using System.Text.RegularExpressions;
using FWO.Api.Data;
using FWO.Report.Filter.Ast;
using System.Collections.Generic;

namespace FWO.Report.Filter
{
    public class DeviceFilter
    {
        public static bool areAllDevicesSelected(Management[] managements)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        if (device != null)
                            if (!device.selected)
                                return false;
            return true;
        }

        public static bool isAnyDeviceFilterSet(Management[] managements, DynGraphqlQuery query)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        if (device != null)
                            if (device.selected)
                                return true;
            // check if filterline contains a device filter
            if (query.FullQuery.Contains("device: {dev_name : {_ilike:") || query.FullQuery.Contains("management: {mgm_name : {_ilike:"))
                return true;
            return false;
        }

        public static bool fullDeviceSelection(Management[] managements, bool fullDeviceSelectionState, out string selectButtonText)
        {
            fullDeviceSelectionState = !fullDeviceSelectionState;
            if (fullDeviceSelectionState)
                selectButtonText = "Clear device selection";
            else
                selectButtonText = "Select all devices";

            foreach (Management management in managements)
            {
                if (management != null)
                {
                    foreach (Device device in management.Devices)
                    {
                        device.selected = fullDeviceSelectionState;
                    }
                }
            }
            return fullDeviceSelectionState;
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
                    if (dev.selected)
                        devFilter += $"gateway=\"{dev.Name}\" or ";

            if (devFilter.Length > 0)
                devFilter = $"({devFilter.Remove(devFilter.Length - 4)})"; // remove final 4 chars " or "
            if (filterWithoutDev.Length > 0 && devFilter.Length>0)
                devFilter = $" and {devFilter}";
            return filterWithoutDev + devFilter;
        }

        /// <summary>
        /// clears current dev filter from left side bar and sets it to device & management filters from filter line
        /// </summary>
        public static void syncFilterLineToLSBFilter(string currentFilterLine, ref Management[] LSBFilter)
        {
            List<string> filteredGatewayList = new List<string>();
            List<string> gatewayList = new List<string>();

            // clear device filter first:
            for (int midx = 0; midx < LSBFilter.Length; ++midx)
                for (int didx = 0; didx < LSBFilter[midx].Devices.Length; ++didx)
                {
                    gatewayList.Add(LSBFilter[midx].Devices[didx].Name);
                    LSBFilter[midx].Devices[didx].selected = false;
                }

            // find gw filter in filter string and perform pattern matching against gw names 
            string pattern = @"(gateway|gw|device|firewall)\s*\=\=?\s*""?(\w+)""?";
            Regex gwFilterRgx = new Regex(pattern);
            string filterLine = currentFilterLine.ToLower();

            foreach (Match gwExpressionMatch in gwFilterRgx.Matches(filterLine))
            {
                Regex gwRgx = new Regex($@"{gwExpressionMatch.Groups[2].Value}");
                foreach (string gw in gatewayList)
                {
                    Match m = gwRgx.Match(gw);
                    if (m.Success)
                        filteredGatewayList.Add(gw);
                }
            }

            // now set all gateways to selected that are mentioned in currentFilterLine
            for (int midx = 0; midx < LSBFilter.Length; ++midx)
                for (int didx = 0; didx < LSBFilter[midx].Devices.Length; ++didx)
                    if (filteredGatewayList.Contains(LSBFilter[midx].Devices[didx].Name))
                        LSBFilter[midx].Devices[didx].selected = true;
        }
    }
}

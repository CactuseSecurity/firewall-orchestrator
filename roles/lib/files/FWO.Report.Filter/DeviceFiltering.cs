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

        public static string cleanFilter(string filter)
        {
            // todo: this mess needs to be cleaned up - should really not be necessary 
            filter = filter.Trim().ToLower();
            if (filter.StartsWith("and "))
                filter = filter.Remove(0, 4);
            if (filter.EndsWith(" and"))
                filter = filter.Remove(filter.Length - 4, 4);
            if (filter.EndsWith(" and "))
                filter = filter.Remove(filter.Length - 5, 5);
            if (filter.IndexOf("( or )")>=0)
                filter = filter.Remove(filter.IndexOf("( or )"), 6);
            if (filter.IndexOf("( and )")>=0)
                filter = filter.Remove(filter.IndexOf("( or )"), 7);
            if (filter.IndexOf("and ()")>=0)
                filter = filter.Remove(filter.IndexOf("and ()"), 6);
            if (filter.Contains("and and"))  // remove duplicate and
                filter = filter.Remove(filter.IndexOf("and and"), 4);
            if (filter=="()")
                filter = "";
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
                Match m = Regex.Match(filterLine, @"(gateway|gw|device|firewall)(\s*\=\=?\s*""?)(\w+""?)");
                if (!m.Success)
                    match = false;
                else {
                    // remove the gw expression from filter line
                    int matchLength = m.Value.Length;
                    int matchPosition = m.Index;
                    filterLine = filterLine.Remove(matchPosition, matchLength);
               }
            }
            string filterWithoutDev = cleanFilter(filterLine);
            string devFilter = "";

            foreach (Management mgm in managements)
                foreach (Device dev in mgm.Devices)
                    if (dev.selected)
                        devFilter += $"gateway={dev.Name} or ";
            if (devFilter.Length>0)
                devFilter = $"({devFilter.Remove(devFilter.Length -4)})"; // remove final 4 chars " or "
            if (filterWithoutDev.Length>0)
                devFilter = $" and {devFilter}";
            return cleanFilter(filterWithoutDev + devFilter);
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

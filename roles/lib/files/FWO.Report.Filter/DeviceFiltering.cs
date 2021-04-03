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
            filter = filter.Trim().ToLower();
            if (filter.StartsWith("and "))
                filter = filter.Remove(0, 4);
            if (filter.EndsWith(" and"))
                filter = filter.Remove(filter.Length - 4, 4);
            if (filter.Contains("and and"))  // remove duplicate and
                filter = filter.Remove(filter.IndexOf("and and"), 4);
            return filter;
        }

        public static string replaceDeviceFilter(string currentInputFilter, Management[] managements, DynGraphqlQuery query)
        {
            // remove old device filter part from currentInputFilter and add the new device filter from management.devices
            string filter = removeDeviceFilterPart(currentInputFilter).TrimEnd() + deviceFilterToString(managements, query);
            // todo: this currently removes filtering completely when loading a template with device filtering
            // we need to convert the loaded filter line into the management.devices structure after loading
            // probably need to see that LSB dev filter is always equal to filter line content

            // if the filter line (without device filter) is empty, get rid of the leading "and" of the device filter
            return cleanFilter(filter);
        }

        /// <summary>
        /// removes all device & management filters from filter line and adds current dev filter from left side bar
        /// </summary>
        public static string syncLSBFilterToFilterLine(Management[] managements, string filterLine)
        {
            string filter = cleanFilter(removeDeviceFilterPart(filterLine));
            string devFilter = "";
            foreach (Management mgm in managements)
                foreach (Device dev in mgm.Devices)
                    if (dev.selected)
                        devFilter += $"gateway={dev.Name} or ";
            if (devFilter.Length>0)
            {
                devFilter = devFilter.Remove(devFilter.Length - 4);  // remove final " or "
                devFilter = "(" + devFilter + ")";  
                if (filter.Length>0)
                    devFilter = " and " + devFilter;  
            }
            return filter + devFilter;
        }

        /// <summary>
        /// clears current dev filter from left side bar and sets it to  device & management filters from filter line
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
                Console.WriteLine("Found gwExpression '{0}' at position {1}", gwExpressionMatch.Groups[2].Value, gwExpressionMatch.Groups[2].Index);
                Regex gwRgx = new Regex($@"{gwExpressionMatch.Groups[2].Value}");
                foreach (string gw in gatewayList)
                {
                    Match m = gwRgx.Match(gw);
                    if (m.Success)
                    {
                        Console.WriteLine("Found matching gateway '{0}' at position {1}.", m.Value, m.Index);
                        filteredGatewayList.Add(gw);
                    }
                }
            }

            // now set all gateways to selected that are mentioned in currentFilterLine
            for (int midx = 0; midx < LSBFilter.Length; ++midx)
                for (int didx = 0; didx < LSBFilter[midx].Devices.Length; ++didx)
                    if (filteredGatewayList.Contains(LSBFilter[midx].Devices[didx].Name))
                        LSBFilter[midx].Devices[didx].selected = true;
            return;
        }

        public static string removeDeviceFilterPart(string filter)
        {
            const string deviceFilterStart = "(gateway=";

            // identify and remove device filter part from filter
            if (filter.Contains(deviceFilterStart))
            {
                int startPosition = filter.IndexOf(deviceFilterStart);
                int endPosition = filter.IndexOf(")", startPosition + deviceFilterStart.Length);
                if (startPosition != 1 && endPosition != -1)
                    filter = filter.Remove(startPosition, endPosition - startPosition + 1); // to remove the first 10 characters
            }
            return cleanFilter(filter);
        }

        public static String deviceFilterToString(Management[] managements, DynGraphqlQuery query)
        {
            string deviceFilter = "";
            if (isAnyDeviceFilterSet(managements, query))
            {
                deviceFilter = " and (";
                foreach (Management management in managements)
                {
                    if (management != null)
                    {
                        foreach (Device device in management.Devices)
                        {
                            if (device.selected)
                            {
                                if (deviceFilter.Length > 6)  // not the first selected device
                                    deviceFilter += " or ";
                                deviceFilter += $"gateway=\"{device.Name}\"";
                            }
                        }
                    }
                }
                if (deviceFilter.Length > 6)  // any filter found at all?
                    deviceFilter += ") ";
                else
                    deviceFilter = "";
            }
            return deviceFilter;
            // return " and gateway=forti";
        }
    }
}

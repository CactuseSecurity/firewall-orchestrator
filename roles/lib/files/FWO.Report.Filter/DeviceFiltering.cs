using System;
using FWO.Api.Data;

namespace FWO.Report.Filter
{
    public class DeviceFilter
    {
        public static bool areAllDevicesSeclected(ref Management[] managements)
        {
            foreach (Management management in managements)
                if (management != null)
                    foreach (Device device in management.Devices)
                        if (device != null)
                            if (!device.selected)
                                return false;
            return true;
        }

        public static bool isAnyDeviceFilterSet(ref Management[] managements, DynGraphqlQuery query)
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

        public static void fullDeviceSelection(ref Management[] managements, ref bool fullDeviceSelectionState, out string selectButtonText)
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
        }

        public static string cleanFilter(string filter)
        {
            filter = filter.Trim();
            if (filter.StartsWith("and "))
                filter = filter.Remove(0, 4);
            if (filter.EndsWith(" and"))
                filter = filter.Remove(filter.Length - 4, 4);
            if (filter.Contains("and and"))  // remove duplicate and
                filter = filter.Remove(filter.IndexOf("and and"), 4);
            return filter;
        }

        public static string replaceDeviceFilter(string currentInputFilter, ref Management[] managements, DynGraphqlQuery query)
        {
            // remove old device filter part from currentInputFilter and add the new device filter from management.devices
            string filter = removeDeviceFilterPart(currentInputFilter).TrimEnd() + deviceFilterToString(ref managements, query);

            // if the filter line (without device filter) is empty, get rid of the leading "and" of the device filter
            return cleanFilter(filter);
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

        public static String deviceFilterToString(ref Management[] managements, DynGraphqlQuery query)
        {
            string deviceFilter = "";
            if (isAnyDeviceFilterSet(ref managements, query))
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

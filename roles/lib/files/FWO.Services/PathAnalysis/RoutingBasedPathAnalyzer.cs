using FWO.Data;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using NetTools;


namespace FWO.Services.PathAnalysis
{
    /// <summary>
    /// Finds the devices on the path between a source and a destination from the imported routing and interface
    /// data, using the <c>devices_in_path</c> database function.
    /// </summary>
    /// <remarks>
    /// Only the first address of each source and destination block is analyzed. Errors are logged and yield an
    /// empty result instead of an exception. The algorithm has no database id and is not created by
    /// <see cref="PathAnalyzerFactory"/>.
    /// </remarks>
    public static class RoutingBasedPathAnalyzer
    {
        /// <summary>
        /// Analyzes one path and returns the names of the devices on it.
        /// </summary>
        /// <param name="source">Source address, network or range; only its first address is analyzed.</param>
        /// <param name="destination">Destination address, network or range; only its first address is analyzed.</param>
        /// <param name="apiConnection">Connection used to run the path analysis query.</param>
        /// <returns>The device names separated by ", "; empty when no device was found or the analysis failed.</returns>
        public static async Task<string> GetDeviceNamesForSinglePath(string source, string destination, ApiConnection apiConnection)
        {
            List<Device> deviceList = await AnalyzeSinglePath(source, destination, apiConnection);

            List<string> devNames = [];
            foreach (Device dev in deviceList)
            {
                devNames.Add(dev.Name ?? "");
            }
            return string.Join(", ", devNames);
        }

        /// <summary>
        /// Analyzes the path from every source to every destination element of a request task and returns the
        /// devices found on any of these paths.
        /// </summary>
        /// <param name="elements">Elements of the request task; only source and destination elements with an IP address are used.</param>
        /// <param name="apiConnection">Connection used to run the path analysis queries.</param>
        /// <returns>Each device once, in the order it was first found; empty when the analysis failed.</returns>
        public static async Task<List<Device>> GetAllDevices(List<WfReqElement> elements, ApiConnection apiConnection)
        {
            List<Device> DevList = [];

            try
            {
                foreach (var elemPair in AnalyseElements(elements))
                {
                    List<Device> actDevList = await AnalyzeSinglePath(elemPair.Key, elemPair.Value, apiConnection);
                    foreach (var dev in actDevList)
                    {
                        if (DevList.FirstOrDefault(x => x.Id == dev.Id) == null)
                        {
                            DevList.Add(dev);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteError("Path Analysis", $"error while analysing paths", e);
            }
            return DevList;
        }

        /// <summary>
        /// Pairs every source address of the elements with every destination address.
        /// </summary>
        private static List<KeyValuePair<string, string>> AnalyseElements(List<WfReqElement> elements)
        {
            List<KeyValuePair<string, string>> elementPairs = [];
            List<string> sources = [];
            List<string> destinations = [];

            foreach (var elem in elements)
            {
                if (elem.Field == ElemFieldType.source.ToString() && elem.Cidr?.Valid == true)
                {
                    sources.Add(elem.Cidr.CidrString);
                }
                else if (elem.Field == ElemFieldType.destination.ToString() && elem.Cidr?.Valid == true)
                {
                    destinations.Add(elem.Cidr.CidrString);
                }
            }

            foreach (var src in sources)
            {
                foreach (var dst in destinations)
                {
                    elementPairs.Add(new KeyValuePair<string, string>(src, dst));
                }
            }
            return elementPairs;
        }

        /// <summary>
        /// Runs the path analysis query for the first address of the source and of the destination.
        /// </summary>
        /// <returns>The devices on the path; empty when an address cannot be parsed or the query fails.</returns>
        private static async Task<List<Device>> AnalyzeSinglePath(string source, string destination, ApiConnection apiConnection)
        {
            List<Device> DevList = [];
            try
            {
                IPAddressRange routingSource = IPAddressRange.Parse(source);
                IPAddressRange routingDestination = IPAddressRange.Parse(destination);
                // we only want cidr
                try
                {
                    var Variables = new { src = routingSource.Begin.ToString(), dst = routingDestination.Begin.ToString() };
                    DevList = await apiConnection.SendQueryAsync<List<Device>>(PathAnalysisQueries.pathAnalysis, Variables);
                }
                catch (Exception exeption)
                {
                    Log.WriteError("Path Analysis", $"error while analysing path", exeption);
                }
            }
            catch (Exception e)
            {
                Log.WriteError("Path Analysis", $"no valid ip address", e);
            }
            return DevList;
        }
    }
}

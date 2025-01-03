using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using NetTools;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Services
{
    public enum PathAnalysisOptions
    {
        WriteToDeviceList, 
        DisplayFoundDevices
    }

    public class PathAnalysisActionParams
    {
        [JsonProperty("option"), JsonPropertyName("option")]
        public PathAnalysisOptions Option { get; set; } = PathAnalysisOptions.DisplayFoundDevices;
    }


    public class PathAnalysis
    {
        public static async Task<string> GetDeviceNamesForSinglePath(string source, string destination, ApiConnection apiConnection)
        {
            List<Device> deviceList = await AnalyzeSinglePath(source, destination, apiConnection);

            List<string> devNames = [];
            foreach(Device dev in deviceList)
            {
                devNames.Add(dev.Name ?? "");
            }
            return string.Join(", ", devNames);
        }

        public static async Task<List<Device>> GetAllDevices(List<WfReqElement> elements, ApiConnection apiConnection)
        {
            List<Device> DevList = [];

            try
            {
                foreach(var elemPair in AnalyseElements(elements))
                {
                    List<Device> actDevList = [];
                    actDevList = await AnalyzeSinglePath(elemPair.Key, elemPair.Value, apiConnection);
                    foreach(var dev in actDevList)
                    {
                        if(DevList.FirstOrDefault(x => x.Id == dev.Id) == null)
                        {
                            DevList.Add(dev);
                        }
                    }
                }  
            }
            catch(Exception e)
            {
                Log.WriteError("Path Analysis", $"error while analysing paths", e);
            }
            return DevList;
        }

        private static List<KeyValuePair<string, string>> AnalyseElements(List<WfReqElement> elements)
        {
            List<KeyValuePair<string, string>> elementPairs = [];
            List<string> sources = [];
            List<string> destinations = [];

            foreach(var elem in elements)
            {
                if (elem.Field == ElemFieldType.source.ToString() && elem.Cidr.CidrString != null)
                {
                    sources.Add(elem.Cidr.CidrString);
                }
                else if (elem.Field == ElemFieldType.destination.ToString() && elem.Cidr.CidrString != null)
                {
                    destinations.Add(elem.Cidr.CidrString);
                }
            }

            foreach(var src in sources)
            {
                foreach(var dst in destinations)
                {
                    elementPairs.Add(new KeyValuePair<string, string>(src, dst));
                }
            }
            return elementPairs;
        }

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
                    DevList = await apiConnection.SendQueryAsync<List<Device>>(NetworkAnalysisQueries.pathAnalysis, Variables);
                }
                catch (Exception exeption)
                {
                    Log.WriteError("Path Analysis", $"error while analysing path", exeption);
                }       
            }
            catch(Exception e)
            {
                Log.WriteError("Path Analysis", $"no valid ip address", e);
            }
            return DevList;
        }
    }
}

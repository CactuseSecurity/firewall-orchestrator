using FWO.GlobalConstants;
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
        private List<WfState> states = new List<WfState>();
        private ApiConnection apiConnection;

        public PathAnalysis(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }


        public async Task<string> getDeviceNamesForSinglePath(string source, string destination)
        {
            List<Device> deviceList = await analyzeSinglePath(source, destination);

            List<string> devNames = new List<string>();
            foreach(Device dev in deviceList)
            {
                devNames.Add(dev.Name ?? "");
            }
            return string.Join(", ", devNames);
        }

        public async Task<List<Device>> getAllDevices(List<WfReqElement> elements)
        {
            List<Device> DevList = new List<Device>();

            try
            {
                foreach(var elemPair in analyseElements(elements))
                {
                    List<Device> actDevList = new List<Device>();
                    actDevList = await analyzeSinglePath(elemPair.Key, elemPair.Value);
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

        private List<KeyValuePair<string, string>> analyseElements(List<WfReqElement> elements)
        {
            List<KeyValuePair<string, string>> elementPairs = new List<KeyValuePair<string, string>>();
            List<string> sources = new List<string>();
            List<string> destinations = new List<string>();

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

        private async Task<List<Device>> analyzeSinglePath(string source, string destination)
        {
            IPAddressRange routingSource = new IPAddressRange();
            IPAddressRange routingDestination = new IPAddressRange();
            List<Device> DevList = new List<Device>();

            try
            {
                routingSource = IPAddressRange.Parse(source);
                routingDestination = IPAddressRange.Parse(destination);
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

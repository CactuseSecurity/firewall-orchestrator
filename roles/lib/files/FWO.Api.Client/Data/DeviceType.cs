using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class DeviceType
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("version"), JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonProperty("manufacturer"), JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = "";

        // [JsonProperty("predefinedObjects"), JsonPropertyName("predefinedObjects")]
        // public ??? PredefinedObjects { get; set; }

        public static List<int> LegacyDevTypeList = new List<int> 
        {
            2, // Netscreen 5.x-6.x
            4, // FortiGateStandalone 5ff
            5, // Barracuda Firewall Control Center Vx
            6, // phion netfence 3.x
            7, // Check Point R5x-R7x
            8  // JUNOS 10-21
        };

        public static Dictionary<int, int> SupermanagerMap = new Dictionary<int, int>
        {  
            { 11, 12 }, // FortiADOM 5ff -> FortiManager 5ff
            { 9, 13 }   // Check Point R8x -> Check Point MDS R8x
        };

        public DeviceType()
        {}
        
        public DeviceType(DeviceType deviceType)
        {
            Id = deviceType.Id;
            Name = deviceType.Name;
            Version = deviceType.Version;
            Manufacturer = deviceType.Manufacturer;
        }

        public string NameVersion()
        {
            return Name + " " + Version;
        }

        public bool IsLegacyDevType()
        {
            return LegacyDevTypeList.Contains(Id);
        }

        public bool CanHaveSupermanager()
        {
            return SupermanagerMap.Keys.Contains(Id);
        }

        public bool CanBeSupermanager()
        {
            return SupermanagerMap.Values.Contains(Id);
        }

        public int GetSupermanagerId()
        {
            return SupermanagerMap[Id];
        }
    }
}

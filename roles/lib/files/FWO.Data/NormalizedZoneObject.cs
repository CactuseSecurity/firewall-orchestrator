using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedZoneObject
    {
        [JsonProperty("zone_uid"), JsonPropertyName("zone_uid")]
        public string ZoneUid { get; set; } = "";

        [JsonProperty("zone_name"), JsonPropertyName("zone_name")]
        public string ZoneName { get; set; } = "";

        public static NormalizedZoneObject FromZoneObject(NetworkZone networkZone)
        {
            return new NormalizedZoneObject
            {
                ZoneName = networkZone.Name,
                ZoneUid = networkZone.Name,
            };
        }
    }
}
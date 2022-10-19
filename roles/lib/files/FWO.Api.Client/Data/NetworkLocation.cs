using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkLocation
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Object { get; set; } = new NetworkObject() { };

        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser User { get; set; } = new NetworkUser() { };

        public NetworkLocation(NetworkUser user, NetworkObject network)
        {
            User = user;
            Object = network;
        }

        public int CompareTo(NetworkLocation secondObj)
        {
            if (this.User != null && secondObj.User != null)
            {
                if (this.User?.Name.CompareTo(secondObj.User?.Name) != 0)
                    return this.User.Name.CompareTo(secondObj.User.Name);
                else
                    return this.Object.Name.CompareTo(secondObj.Object.Name);
            }
            else 
            {
                return this.Object.Name.CompareTo(secondObj.Object.Name);
            }
        }
    }
}

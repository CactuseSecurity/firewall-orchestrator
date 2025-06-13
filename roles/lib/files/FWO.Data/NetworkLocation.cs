using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkLocation : IComparable
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Object { get; set; }

        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser User { get; set; }

        public NetworkLocation(NetworkUser user, NetworkObject network)
        {
            User = user;
            Object = network;
        }

        int IComparable.CompareTo(object? obj)
        {
            if (obj is NetworkLocation)
            {
                NetworkLocation secondNetworkLocation = (obj as NetworkLocation)!;
                if (this.User != null && secondNetworkLocation.User != null)
                {
                    if (this.User?.Name.CompareTo(secondNetworkLocation.User?.Name) != 0)
                        return this.User!.Name.CompareTo(secondNetworkLocation.User!.Name);
                    else
                        return this.Object.Name.CompareTo(secondNetworkLocation.Object.Name);
                }
                else 
                {
                    return this.Object.Name.CompareTo(secondNetworkLocation.Object.Name);
                }
            }
            else
            {
                throw new ArgumentException("Uncomparable");
            }
        }
    }
}

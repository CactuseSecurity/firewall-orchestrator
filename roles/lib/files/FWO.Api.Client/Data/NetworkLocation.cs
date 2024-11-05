using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkLocation(NetworkUser user, NetworkObject network) : IComparable
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Object { get; set; } = network;

        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser User { get; set; } = user;

        int IComparable.CompareTo(object? secondObject)
        {
            if (secondObject != null && secondObject is NetworkLocation)
            {
                NetworkLocation secondNetworkLocation = (secondObject as NetworkLocation)!;
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
                throw new Exception("Uncomparable");
            }
        }
    }
}

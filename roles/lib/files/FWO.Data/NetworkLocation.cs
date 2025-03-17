using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkLocation : IComparable
    {
        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject? Object { get; set; } = default!;

        [JsonProperty("usr"), JsonPropertyName("usr")]
        public NetworkUser? User { get; set; } = default!;

        public NetworkLocation(NetworkUser user, NetworkObject? networkObject)
        {
            Object = networkObject;
            User = user;
        }

        int IComparable.CompareTo(object? secondObject)
        {
            if (secondObject != null && secondObject is NetworkLocation)
            {
                NetworkLocation secondNetworkLocation = (secondObject as NetworkLocation)!;
                if (this.User != null && secondNetworkLocation.User != null &&
                    this.User?.Name.CompareTo(secondNetworkLocation.User?.Name) != 0)
                {
                    return this.User!.Name.CompareTo(secondNetworkLocation.User!.Name);
                }
                if (this.Object != null && secondNetworkLocation.Object != null)
                {
                    return this.Object.Name.CompareTo(secondNetworkLocation.Object.Name);
                }
                else {
                    return 0;
                } 
            }
            else
            {
                throw new Exception("Uncomparable");
            }
        }
    }
}

using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NetworkUserType
    {
        [JsonPropertyName("usr_typ_name")]
        public string Name { get; set; } = "";
    }
}

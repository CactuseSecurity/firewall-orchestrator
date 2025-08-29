
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
  public class Color
  {
    [JsonProperty("color_name"), JsonPropertyName("color_name")]
    public string Name { get; set; } = "";
  }
}
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Config.Api.Data
{
    /// <summary>
    /// a list of all available ColorSchemes
    /// </summary>
    public class ColorScheme
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("isDefault"), JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; } = false;

        [JsonProperty("hex"), JsonPropertyName("hex")]
        public string Hex { get; set; } = "";

        [JsonProperty("hex2"), JsonPropertyName("hex2")]
        public string Hex2 { get; set; } = "";

        public static List<ColorScheme> AvailableSchemes { get; } = new List<ColorScheme>
        {
            new ColorScheme { Name = "color_scheme_blue", IsDefault = true, Hex = "#054B8C", Hex2 = "#03335E" },
            new ColorScheme { Name = "color_scheme_green", Hex = "#1c8c05ff" , Hex2 = "#155e03ff" },
            new ColorScheme { Name = "color_scheme_red", Hex = "#8c0505ff", Hex2 = "#5e0303ff" },
            new ColorScheme { Name = "color_scheme_purple", Hex = "#5b058cff", Hex2 = "#3a035eff" }
            // Add more schemes as needed
        };

        public static ColorScheme GetSchemeByName(string name)
        {
            return AvailableSchemes.FirstOrDefault(s => s.Name == name) ?? AvailableSchemes.First(s => s.IsDefault);
        }
    }

}

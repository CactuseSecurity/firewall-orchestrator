using System.Text.Json.Serialization;
using System.Collections.Generic;
namespace FWO.ApiConfig.Data
{
    /// <summary>
    /// a list of all available languages
    /// </summary>
    public class Language
    {

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("culture_info")]
        public string CultureInfo { get; set; }
    }

    // public class LanguageDict
    // {
    //     public string LanguageName { get; set; }
    //     public string LanguageCultureInfo { get; set; }
    //     public Dictionary<string,string> Dict; 
    //     public LanguageDict(string languageName, string languageCulture)
    //     {
    //         LanguageName = languageName;
    //         LanguageCultureInfo = languageCulture;
    //     }
    // }
}

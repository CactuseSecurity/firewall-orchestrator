using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class FileFormat
    {
        [JsonProperty("report_schedule_format_name"), JsonPropertyName("report_schedule_format_name")]
        public string Name { get; set; } = "";
    }

    public static class FileFormatUtil
    {
        public static FileFormat Find(this IEnumerable<FileFormat> fileFormats, string name)
        {
            return fileFormats.First(fileFormat => fileFormat.Name == name);
        }

        public static void AddOrRemove(this List<FileFormat> fileFormats, string name)
        {
            if (fileFormats.RemoveAll(fileFormat => fileFormat.Name == name) == 0)
            {
                fileFormats.Add(new FileFormat { Name = name });
            }
        }
        public static void Remove(this List<FileFormat> fileFormats, string name)
        {
            fileFormats.RemoveAll(fileFormat => fileFormat.Name == name);
        }
    }
}

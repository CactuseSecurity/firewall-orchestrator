using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class FileFormat
    {
        [JsonPropertyName("report_schedule_format_name")]
        public string Name { get; set; }
    }

    //public class FileFormatReportSchedule
    //{
    //    [JsonPropertyName("file_format_name")]
    //    public string FileFormatName { get; set; }

    //    [JsonPropertyName("report_schedule_id")]
    //    public string ReportScheduleId { get; set; }
    //}

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
    }
}

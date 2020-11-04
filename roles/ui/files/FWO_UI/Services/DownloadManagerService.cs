using FWO.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Services
{
    public class DownloadManagerService
    {
        public List<Download> Downloads { get; set; } = new List<Download>();

        public static string GetPath(string fileName, string userId)
        {
            return $"Downloads/{userId}/{fileName}";
        }
    }

    public abstract class Download
    {
        public string Name { get; protected set; }
        public string Type { get; protected set; }

        public abstract byte[] GetContent();
    }

    public class MemoryDownload : Download
    {
        private readonly byte[] content;

        public MemoryDownload(string name, string type, byte[] content)
        {
            Name = name;
            Type = type;
            this.content = content;
        }

        public override byte[] GetContent()
        {
            return content;
        }
    }

    public class DiskDownload : Download
    {
        public string Path { get; protected set; }

        public DiskDownload(string name, string type, string path)
        {
            Name = name;
            Type = type;
            Path = path;
        }

        public override byte[] GetContent()
        {
            try
            {
                return File.ReadAllBytes(Path);
            }
            catch (Exception exception)
            {
                Log.WriteError("Download file", $"Could not read download file at path \"{Path}\"", exception);
                return new byte[0];
            }
        }
    }
}

using FWO.Data;
using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class AppServerImportEventArgs(bool success = false) : IEventArgs
    {
        public bool Success { get; set; } = success;
        public List<CSVFileUploadErrorModel> Errors { get; set; } = [];
        public List<string> Appserver { get; set; } = [];
    }
}

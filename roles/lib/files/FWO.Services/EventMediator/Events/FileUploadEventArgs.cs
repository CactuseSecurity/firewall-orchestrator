using FWO.Data;
using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class FileUploadEventArgs(bool success = false, string? fileName = null, ErrorBaseModel? error = default) : IEventArgs
    {
        public bool Success { get; set; } = success;
        public string? FileName { get; set; } = fileName;
        public ErrorBaseModel? Error { get; set; } = error ?? new ErrorBaseModel();
    }
}

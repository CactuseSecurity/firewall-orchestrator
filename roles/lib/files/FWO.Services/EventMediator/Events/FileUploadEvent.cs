using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class FileUploadEvent(FileUploadEventArgs? eventArgs = default) : IEvent
    {
        public FileUploadEventArgs? EventArgs { get; set; } = eventArgs ?? new FileUploadEventArgs();

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as FileUploadEventArgs;
        }
    }
}
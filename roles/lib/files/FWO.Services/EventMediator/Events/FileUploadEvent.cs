using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class FileUploadEvent : IEvent
    {
        public FileUploadEventArgs? EventArgs { get; set; } = default;

        public FileUploadEvent(FileUploadEventArgs? eventArgs = default)
        {
            EventArgs = eventArgs ?? new FileUploadEventArgs();
        }

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as FileUploadEventArgs;
        }
    }
}
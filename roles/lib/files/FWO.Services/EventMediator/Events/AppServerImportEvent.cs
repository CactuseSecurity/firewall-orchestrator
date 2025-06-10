using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class AppServerImportEvent(AppServerImportEventArgs? eventArgs = default) : IEvent
    {
        public AppServerImportEventArgs? EventArgs { get; set; } = eventArgs ?? new AppServerImportEventArgs();

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as AppServerImportEventArgs;
        }
    }
}

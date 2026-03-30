using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class PermissionChangedEvent(PermissionChangedEventArgs? eventArgs = default) : IEvent
    {
        public PermissionChangedEventArgs? EventArgs { get; set; } = eventArgs ?? new PermissionChangedEventArgs();

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as PermissionChangedEventArgs;
        }
    }
}

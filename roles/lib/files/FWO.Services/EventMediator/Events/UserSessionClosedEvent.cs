using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class UserSessionClosedEvent(UserSessionClosedEventArgs? eventArgs = default) : IEvent
    {
        public UserSessionClosedEventArgs? EventArgs { get; set; } = eventArgs ?? new UserSessionClosedEventArgs();

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as UserSessionClosedEventArgs;
        }
    }
}
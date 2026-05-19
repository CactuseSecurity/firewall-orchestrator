using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class JwtExpiredEvent(JwtExpiredEventArgs? eventArgs = default) : IEvent
    {
        public JwtExpiredEventArgs? EventArgs { get; set; } = eventArgs ?? new JwtExpiredEventArgs();

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as JwtExpiredEventArgs;
        }
    }
}

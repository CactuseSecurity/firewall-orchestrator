using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    /// <summary>
    /// Signals that automatic session recovery is no longer possible and the user must re-authenticate.
    /// </summary>
    public class ReloginRequiredEvent(ReloginRequiredEventArgs? eventArgs = default) : IEvent
    {
        /// <summary>
        /// Event payload describing the affected user session.
        /// </summary>
        public ReloginRequiredEventArgs? EventArgs { get; set; } = eventArgs ?? new ReloginRequiredEventArgs();

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as ReloginRequiredEventArgs;
        }
    }
}

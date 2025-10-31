using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class CollectionChangedEvent(CollectionChangedEventArgs? eventArgs = default) : IEvent
    {
        private readonly CollectionChangedEventArgs? eventArgs = eventArgs;

        IEventArgs? IEvent.EventArgs { get; set; }
    }
}

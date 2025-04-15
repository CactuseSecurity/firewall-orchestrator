using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events;

public class CollectionChangedEvent(CollectionChangedEventArgs? eventArgs = default) : IEvent
{
    public IEventArgs? EventArgs { get; set; } = eventArgs;
}
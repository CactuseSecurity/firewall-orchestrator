using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{

#pragma warning disable CS9113 // Der Parameter ist ungelesen.
    public class CollectionChangedEvent(CollectionChangedEventArgs? eventArgs = default) : IEvent
    {
        IEventArgs? IEvent.EventArgs { get; set; }
    }
#pragma warning restore CS9113 // Der Parameter ist ungelesen.
}

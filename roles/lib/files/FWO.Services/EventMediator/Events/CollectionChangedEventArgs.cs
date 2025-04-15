using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events;

public class CollectionChangedEventArgs(IEnumerable<dynamic> collection) : IEventArgs
{
    IEnumerable<dynamic> Collection { get; } = collection;
}


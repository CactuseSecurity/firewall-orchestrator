using FWO.Data;
using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator;

public class EventMediator : IEventMediator
{
    private readonly Dictionary<Type, List<Action<IEvent>>> _handlers = [];

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IEvent
    {
        if(!_handlers.ContainsKey(typeof(TEvent)))
        {
            _handlers[typeof(TEvent)] = [];
        }

        _handlers[typeof(TEvent)].Add(e => handler((TEvent)e));
    }

    public void Publish<TEvent>(TEvent @event)  where TEvent : class, IEvent
    {
        if(_handlers.TryGetValue(typeof(TEvent), out List<Action<IEvent>>? handlers))
        {
            foreach(Action<IEvent> handler in handlers)
            {
                handler(@event);
            }
        }
    }

    public void Unsubscribe<TEvent>(TEvent @event)
    {

    }
}


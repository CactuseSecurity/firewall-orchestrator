using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator;

public class EventMediator : IEventMediator
{
    private readonly Dictionary<Type, Dictionary<string, List<Action<IEvent>>>> _handlers = [];

    /// <summary>
    /// Adds the handler as an invokeable action.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="name"></param>
    /// <param name="handler"></param>
    public void Subscribe<TEvent>(string name, Action<TEvent> handler) where TEvent : class, IEvent
    {
        if(!_handlers.ContainsKey(typeof(TEvent)))
        {
            _handlers[typeof(TEvent)] = [];
        }

        if(!_handlers[typeof(TEvent)].TryGetValue(name, out List<Action<IEvent>>? value))
        {
            value = [];
            _handlers[typeof(TEvent)][name] = value;
        }

        value.Add(e => handler((TEvent)e));
    }

    /// <summary>
    /// If matching subscription handler was found it will be invoked.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="name"></param>
    /// <param name="event"></param>
    public void Publish<TEvent>(string name, TEvent @event) where TEvent : class, IEvent
    {
        if(_handlers.TryGetValue(typeof(TEvent), out Dictionary<string, List<Action<IEvent>>>? actions) 
            && actions.TryGetValue(name, out List<Action<IEvent>>? handlers))
        {
            foreach(Action<IEvent> handler in handlers)
            {
                handler(@event);
            }
        }
    }

    /// <summary>
    /// Unsubscribe of ALL events of the given type matching the name.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="event"></param>
    /// <returns>True/False if remove was successfull</returns>
    public bool Unsubscribe<TEvent>(string name) where TEvent : class, IEvent
    {
        if(_handlers.ContainsKey(typeof(TEvent)))
        {
           return _handlers.Remove(typeof(TEvent));
        }

        return false;
    }
}


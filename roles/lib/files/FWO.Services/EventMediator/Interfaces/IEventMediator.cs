namespace FWO.Services.EventMediator.Interfaces;

public interface IEventMediator
{
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IEvent;
    void Publish<TEvent>(TEvent @event) where TEvent : class, IEvent;
    void Subscribe<TEvent>(object sender, Action<TEvent> handler) where TEvent : class, IEvent;
    void Publish<TEvent>(object sender, TEvent @event) where TEvent : class, IEvent;
}


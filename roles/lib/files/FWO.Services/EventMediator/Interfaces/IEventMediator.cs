namespace FWO.Services.EventMediator.Interfaces
{
    public interface IEventMediator
    {
        void Subscribe<TEvent>(string name, Action<TEvent> handler) where TEvent : class, IEvent;
        void Publish<TEvent>(string name, TEvent @event) where TEvent : class, IEvent;
        bool Unsubscribe<TEvent>(string name) where TEvent : class, IEvent;
    }
}
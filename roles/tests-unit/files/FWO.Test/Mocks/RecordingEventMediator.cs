using FWO.Services.EventMediator.Interfaces;

namespace FWO.Test.Mocks
{
    public sealed record PublishedEventRecord(string Name, IEvent Event);

    public sealed class RecordingEventMediator : IEventMediator
    {
        public List<PublishedEventRecord> PublishedEvents { get; } = new List<PublishedEventRecord>();

        public void Subscribe<TEvent>(string name, Action<TEvent> handler) where TEvent : class, IEvent
        {
        }

        public void Publish<TEvent>(string name, TEvent @event) where TEvent : class, IEvent
        {
            PublishedEvents.Add(new PublishedEventRecord(name, @event));
        }

        public bool Unsubscribe<TEvent>(string name) where TEvent : class, IEvent
        {
            return false;
        }
    }
}

namespace FWO.Services.EventMediator.Interfaces
{
    public interface IEvent
    {
        public IEventArgs? EventArgs { get; set; }
    }
}

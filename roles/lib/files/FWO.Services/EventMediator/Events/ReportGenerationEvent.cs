using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class ReportGenerationEvent : IEvent
    {
        public IEventArgs? EventArgs { get; set; }
    }
}

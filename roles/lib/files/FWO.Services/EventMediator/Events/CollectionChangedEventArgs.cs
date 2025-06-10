using FWO.Data;
using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class CollectionChangedEventArgs(IEnumerable<dynamic>? collection = default, ErrorBaseModel? error = default) : IEventArgs
    {
        public ErrorBaseModel? Error { get; set; } = error ?? new ErrorBaseModel();
        IEnumerable<dynamic> Collection { get; } = collection ?? [];
    }
}

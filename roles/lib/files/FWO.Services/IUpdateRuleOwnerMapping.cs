using FWO.Basics;
using FWO.Services.EventMediator.Events;

namespace FWO.Services
{
    public interface IUpdateRuleOwnerMapping
    {
        OwnerMappingSourceStm Source { get; }
        Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null);
    }
}

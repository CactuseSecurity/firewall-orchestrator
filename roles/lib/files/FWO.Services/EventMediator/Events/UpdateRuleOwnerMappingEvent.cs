using FWO.Services.EventMediator.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Services.EventMediator.Events
{
    public class UpdateRuleOwnerMappingEvent : IEvent
    {
        public UpdateRuleOwnerMappingEventArgs EventArgs { get; set; }

        IEventArgs? IEvent.EventArgs
        {
            get => EventArgs;
            set => EventArgs = value as UpdateRuleOwnerMappingEventArgs ?? new UpdateRuleOwnerMappingEventArgs();
        }

        public UpdateRuleOwnerMappingEvent(UpdateRuleOwnerMappingEventArgs? args = null)
        {

            EventArgs = args ?? new UpdateRuleOwnerMappingEventArgs();
        }
    }
}

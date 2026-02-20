using FWO.Data;
using FWO.Services.EventMediator.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;


namespace FWO.Services.EventMediator.Events
{
    public class UpdateRuleOwnerMappingEventArgs : IEventArgs
    {
        public bool isFullReInitialize { get; set; } = false;

        public TaskCompletionSource<bool>? Completion { get; set; }

    }
}

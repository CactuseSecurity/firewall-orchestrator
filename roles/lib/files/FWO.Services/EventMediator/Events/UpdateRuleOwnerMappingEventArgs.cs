using FWO.Data;
using FWO.Services;
using FWO.Services.EventMediator.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;


namespace FWO.Services.EventMediator.Events
{
    public class UpdateRuleOwnerMappingEventArgs : IEventArgs
    {
        public bool isFullReInitialize { get; set; } = false;

        /// <summary>
        /// True when the full reinitialize follows a deliberate change, for instance a different marker,
        /// another mapping source or edited owner networks. The rebuilt state then differs from the stored
        /// one by design, so that difference must not be reported as drift of the incremental mapping.
        /// </summary>
        public bool TriggeredByChange { get; set; } = false;

        /// <summary>
        /// What was changed, recorded with the run so its result can be understood later on.
        /// </summary>
        public List<RuleOwnerMappingChange> Changes { get; set; } = [];

        public TaskCompletionSource<bool>? Completion { get; set; }

    }
}

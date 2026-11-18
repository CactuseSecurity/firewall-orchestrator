namespace FWO.Data.Workflow
{
    /// <summary>
    /// Defines which workflow object can be promoted explicitly in a phase.
    /// </summary>
    public enum PromotionMode
    {
        /// <summary>Promote the ticket as a whole.</summary>
        Ticket = 0,
        /// <summary>Promote individual tasks.</summary>
        Task = 1,
        /// <summary>Allow ticket and task promotion.</summary>
        Both = 2,
        /// <summary>Hide direct promotion controls and show configured action buttons only.</summary>
        ActionButtonsOnly = 3
    }
}

namespace FWO.Data.Workflow
{
    /// <summary>
    /// Defines which state determines whether a ticket is listed in a workflow phase.
    /// </summary>
    public enum PhaseVisibilityMode
    {
        AnyTask = 0,
        TicketState = 1
    }
}

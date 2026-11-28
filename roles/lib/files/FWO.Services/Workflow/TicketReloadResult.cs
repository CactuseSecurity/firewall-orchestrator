namespace FWO.Services.Workflow
{
    /// <summary>The result of reloading the active ticket of a workflow handler.</summary>
    public enum TicketReloadResult
    {
        /// <summary>The ticket was reloaded and is the active ticket.</summary>
        Refreshed,
        /// <summary>The ticket could not be read (reported to the user); the error may be passing.</summary>
        ReadFailed,
        /// <summary>The ticket is no longer visible to the user, e.g. as it moved to another responsible group.</summary>
        NotVisible,
        /// <summary>The ticket is not stored yet or there is no database access.</summary>
        NotReloadable
    }
}

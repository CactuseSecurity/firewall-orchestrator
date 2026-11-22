namespace FWO.Data.Workflow
{
    /// <summary>
    /// Display rules for the ticket reference of a preceding workflow (<see cref="WfTicketBase.PreWorkflowTicketReference"/>).
    /// The reference is only shown when a label is configured.
    /// </summary>
    public static class PreWorkflowTicketReferenceDisplay
    {
        private const string kEmptyDisplay = "-";

        /// <summary>
        /// Returns whether the reference is displayed, i.e. whether a non-empty label is configured.
        /// </summary>
        /// <param name="label">The configured label.</param>
        public static bool IsActive(string? label)
        {
            return !string.IsNullOrWhiteSpace(label);
        }

        /// <summary>
        /// Returns the reference for display in fields and table cells, "-" if it is empty.
        /// </summary>
        /// <param name="reference">The ticket reference of the preceding workflow.</param>
        public static string DisplayValue(string? reference)
        {
            string? trimmedReference = reference?.Trim();
            return string.IsNullOrEmpty(trimmedReference) ? kEmptyDisplay : trimmedReference;
        }

        /// <summary>
        /// Builds the ticket headline "&lt;ticketText&gt; &lt;reference&gt;: &lt;title&gt;".
        /// Without active display or without reference the headline is "&lt;ticketText&gt;: &lt;title&gt;".
        /// </summary>
        /// <param name="ticketText">The localized word for ticket.</param>
        /// <param name="label">The configured label of the reference.</param>
        /// <param name="ticket">The ticket to build the headline for.</param>
        public static string TicketHeadline(string ticketText, string? label, WfTicketBase ticket)
        {
            string? reference = ticket.PreWorkflowTicketReference?.Trim();
            bool showReference = IsActive(label) && !string.IsNullOrEmpty(reference);
            string prefix = showReference ? $"{ticketText} {reference}" : ticketText;
            return $"{prefix}: {ticket.Title}";
        }
    }
}

using FWO.Data;
using FWO.ExternalSystems.Tufin.SecureChange;

namespace FWO.ExternalSystems
{
    public static class ExternalTicketFactory
    {
        /// <summary>
        /// Creates the ticket implementation matching the ticket system type.
        /// </summary>
        /// <param name="ticketSystem">The external ticket system.</param>
        /// <param name="certificateChecks">The certificate checking switches per connection type.</param>
        /// <param name="scClient">An existing SecureChange client, used instead of creating one.</param>
        /// <returns>The ticket for the ticket system.</returns>
        public static ExternalTicket Create(ExternalTicketSystem ticketSystem, ExternalCertificateChecks certificateChecks,
            SCClient? scClient = null)
        {
            if (ticketSystem.IsTufinSecureChange())
            {
                return new SCTicket(ticketSystem, certificateChecks, scClient);
            }
            // Check Point is written directly by the firewall write library, not through a ticket
            throw new NotSupportedException("Automatic external request handling is supported only for Tufin SecureChange.");
        }
    }
}

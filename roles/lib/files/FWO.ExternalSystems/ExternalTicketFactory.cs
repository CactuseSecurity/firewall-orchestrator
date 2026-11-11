using FWO.Data;
using FWO.ExternalSystems.CheckPoint;
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
        /// <param name="checkPointClient">An existing Check Point client, used instead of creating one.</param>
        /// <returns>The ticket for the ticket system.</returns>
        public static ExternalTicket Create(ExternalTicketSystem ticketSystem, ExternalCertificateChecks certificateChecks,
            SCClient? scClient = null, CheckPointClient? checkPointClient = null)
        {
            if (ticketSystem.IsTufinSecureChange())
            {
                return new SCTicket(ticketSystem, certificateChecks, scClient);
            }
            if (ticketSystem.IsCheckPoint())
            {
                return new CheckPointTicket(ticketSystem, certificateChecks, checkPointClient);
            }
            throw new NotSupportedException("Automatic external request handling is supported only for Tufin SecureChange and Check Point.");
        }
    }
}

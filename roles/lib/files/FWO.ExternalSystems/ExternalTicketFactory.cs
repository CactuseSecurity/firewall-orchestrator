using FWO.Data;
using FWO.ExternalSystems.CheckPoint;
using FWO.ExternalSystems.Tufin.SecureChange;

namespace FWO.ExternalSystems
{
    public static class ExternalTicketFactory
    {
        public static ExternalTicket Create(ExternalTicketSystem ticketSystem, SCClient? scClient = null, CheckPointClient? checkPointClient = null)
        {
            if (ticketSystem.IsTufinSecureChange())
            {
                return new SCTicket(ticketSystem, scClient);
            }
            if (ticketSystem.IsCheckPoint())
            {
                return new CheckPointTicket(ticketSystem, checkPointClient);
            }
            throw new NotSupportedException("Ticket system has no supported request templates.");
        }
    }
}

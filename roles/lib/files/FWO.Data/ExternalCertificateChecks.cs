namespace FWO.Data
{
    /// <summary>
    /// The certificate checking switches for the connection types an external ticket system request can open.
    /// </summary>
    /// <remarks>
    /// Certificate checking is configured once per connection type, not per connection. A Check Point
    /// change request talks to a firewall management, so it needs the switch for firewall connections
    /// as well as the one for ticket systems.
    /// </remarks>
    /// <param name="FirewallConnections">Whether certificates of firewall management connections are checked.</param>
    /// <param name="TicketSystems">Whether certificates of external ticket systems are checked.</param>
    public sealed record ExternalCertificateChecks(bool FirewallConnections, bool TicketSystems);
}

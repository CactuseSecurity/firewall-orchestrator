namespace FWO.Ui.Services
{
    /// <summary>
    /// Aggregated view on all UI sessions currently open on this UI server.
    /// </summary>
    public class UiSessionOverview
    {
        /// <summary>
        /// Number of open sessions, including sessions that are not logged in.
        /// </summary>
        public int OpenSessions { get; set; }

        /// <summary>
        /// Number of open sessions with an established browser connection.
        /// </summary>
        public int ConnectedSessions { get; set; }

        /// <summary>
        /// Number of open sessions in which a user is logged in.
        /// </summary>
        public int AuthenticatedSessions { get; set; }

        /// <summary>
        /// Number of distinct users currently logged in.
        /// </summary>
        public int LoggedInUsers { get; set; }

        /// <summary>
        /// The open sessions, ordered by their opening time.
        /// </summary>
        public List<UiSession> Sessions { get; set; } = [];
    }
}

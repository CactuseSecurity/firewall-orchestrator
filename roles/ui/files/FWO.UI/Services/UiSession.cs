namespace FWO.Ui.Services
{
    /// <summary>
    /// One open UI session (Blazor circuit) as seen by the <see cref="UiSessionTracker"/>.
    /// </summary>
    public class UiSession
    {
        /// <summary>
        /// Identifier of the session, unique for the lifetime of the UI server process.
        /// </summary>
        public string SessionId { get; set; } = "";

        /// <summary>
        /// Login name of the authenticated user, empty as long as the session is anonymous.
        /// </summary>
        public string UserName { get; set; } = "";

        /// <summary>
        /// Distinguished name of the authenticated user, empty as long as the session is anonymous.
        /// </summary>
        public string UserDn { get; set; } = "";

        /// <summary>
        /// Point in time (UTC) at which the session was opened.
        /// </summary>
        public DateTime OpenedAt { get; set; }

        /// <summary>
        /// Point in time (UTC) of the last observed activity of the session.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// True while the browser connection of the session is established.
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>
        /// True if a user is logged in within this session.
        /// </summary>
        public bool Authenticated => !string.IsNullOrWhiteSpace(UserName);
    }
}

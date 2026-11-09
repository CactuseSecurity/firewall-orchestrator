namespace FWO.Ui.Services
{
    /// <summary>
    /// Result of asking the middleware whether a session is still live.
    /// </summary>
    public enum SessionCheckOutcome
    {
        /// <summary>
        /// The session could not be checked this time, e.g. because the middleware was unreachable; it is kept.
        /// </summary>
        Unknown,

        /// <summary>
        /// The session is live.
        /// </summary>
        Live,

        /// <summary>
        /// The session has ended: its refresh token expired or was revoked, or its user was deleted.
        /// </summary>
        Ended
    }
}

using System.Collections.Concurrent;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Keeps track of all UI sessions (Blazor circuits) currently open on this UI server.
    /// Registered as singleton and fed by the <see cref="UiSessionCircuitHandler"/>.
    /// </summary>
    public class UiSessionTracker
    {
        private readonly ConcurrentDictionary<string, UiSession> sessions = new();
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Initializes a new instance of the <see cref="UiSessionTracker"/> class.
        /// </summary>
        /// <param name="utcNow">Optional clock, mainly used to make the tracker testable.</param>
        public UiSessionTracker(Func<DateTime>? utcNow = null)
        {
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Registers a newly opened session.
        /// </summary>
        /// <param name="sessionId">Identifier of the session.</param>
        public void Register(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            DateTime now = utcNow();
            sessions[sessionId] = new UiSession
            {
                SessionId = sessionId,
                OpenedAt = now,
                LastActivity = now
            };
        }

        /// <summary>
        /// Removes a closed session.
        /// </summary>
        /// <param name="sessionId">Identifier of the session.</param>
        public void Unregister(string sessionId)
        {
            sessions.TryRemove(sessionId, out _);
        }

        /// <summary>
        /// Stores the logged in user of a session. Empty values mark the session as anonymous again.
        /// </summary>
        /// <param name="sessionId">Identifier of the session.</param>
        /// <param name="userName">Login name of the user.</param>
        /// <param name="userDn">Distinguished name of the user.</param>
        public void SetUser(string sessionId, string? userName, string? userDn = null)
        {
            Update(sessionId, session =>
            {
                session.UserName = userName?.Trim() ?? "";
                session.UserDn = userDn?.Trim() ?? "";
            });
        }

        /// <summary>
        /// Stores the browser connection state of a session.
        /// </summary>
        /// <param name="sessionId">Identifier of the session.</param>
        /// <param name="connected">True if the browser connection is established.</param>
        public void SetConnected(string sessionId, bool connected)
        {
            Update(sessionId, session => session.Connected = connected);
        }

        /// <summary>
        /// Returns the aggregated state of all currently open sessions.
        /// </summary>
        /// <returns>The session overview.</returns>
        public UiSessionOverview GetOverview()
        {
            List<UiSession> currentSessions = [.. sessions.Values.OrderBy(session => session.OpenedAt)];
            return new UiSessionOverview
            {
                OpenSessions = currentSessions.Count,
                ConnectedSessions = currentSessions.Count(session => session.Connected),
                AuthenticatedSessions = currentSessions.Count(session => session.Authenticated),
                LoggedInUsers = currentSessions.Where(session => session.Authenticated)
                    .Select(session => string.IsNullOrWhiteSpace(session.UserDn) ? session.UserName : session.UserDn)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Sessions = currentSessions
            };
        }

        private void Update(string sessionId, Action<UiSession> change)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || !sessions.TryGetValue(sessionId, out UiSession? session))
            {
                return;
            }

            change(session);
            session.LastActivity = utcNow();
        }
    }
}

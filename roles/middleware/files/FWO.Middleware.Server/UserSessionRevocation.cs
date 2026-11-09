using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Ends the sessions of local users whose permissions were reduced, by revoking all of their refresh tokens.
    /// A revoked session can no longer be refreshed, and the UI ends it at its next session check, so the reduced
    /// permissions take effect without waiting for the access token to expire (#5503).
    /// </summary>
    public class UserSessionRevocation
    {
        private const string kLogTitle = "Session Revocation";

        private readonly ApiConnection apiConnection;
        private readonly List<Ldap> ldaps;

        /// <summary>
        /// Creates the session revocation for the given API connection and directories.
        /// </summary>
        /// <param name="apiConnection">API connection with the right to revoke refresh tokens.</param>
        /// <param name="ldaps">Connected directories, used to resolve group members.</param>
        public UserSessionRevocation(ApiConnection apiConnection, List<Ldap> ldaps)
        {
            this.apiConnection = apiConnection;
            this.ldaps = ldaps;
        }

        /// <summary>
        /// Resolves user and group dns to the ids of the local users they name, following nested groups.
        /// Call it before a change that removes group memberships, as the members cannot be resolved afterwards.
        /// </summary>
        /// <param name="dns">User or group dns.</param>
        /// <returns>Ids of the local users named by the dns; empty if they could not be resolved.</returns>
        public async Task<List<int>> ResolveLocalUserIds(IEnumerable<string> dns)
        {
            try
            {
                List<string> userDns = await new WorkflowRecipientResolver(apiConnection, ldaps).ResolveUserDns(dns);
                if (userDns.Count == 0)
                {
                    return [];
                }

                HashSet<string> wantedDns = new(userDns, DistName.DnComparer);
                List<UiUser> localUsers = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUserEmails);
                return [.. localUsers.Where(user => wantedDns.Contains(user.Dn)).Select(user => user.DbId).Distinct()];
            }
            catch (Exception exception)
            {
                Log.WriteError(kLogTitle, $"Could not resolve the local users of {string.Join("; ", dns)}. Their sessions end when their access tokens expire.", exception);
                return [];
            }
        }

        /// <summary>
        /// Revokes all refresh tokens of the given local users. Failures are logged and not thrown, because the
        /// permission change that calls for the revocation has already been made.
        /// </summary>
        /// <param name="userIds">Ids of the local users.</param>
        /// <param name="reason">Change that reduced the permissions, for the audit log.</param>
        /// <returns>The number of revoked refresh tokens.</returns>
        public async Task<int> RevokeSessions(List<int> userIds, string reason)
        {
            if (userIds.Count == 0)
            {
                return 0;
            }

            try
            {
                ReturnId revoked = await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.revokeRefreshTokensOfUsers,
                    new { userIds, revokedAt = DateTime.UtcNow });
                Log.WriteAudit(kLogTitle, $"{reason}: ended {revoked.AffectedRows} session(s) of local user id(s) {string.Join(", ", userIds)}.");
                return revoked.AffectedRows;
            }
            catch (Exception exception)
            {
                Log.WriteError(kLogTitle, $"{reason}: could not end the sessions of local user id(s) {string.Join(", ", userIds)}. " +
                    "They end when their access tokens expire.", exception);
                return 0;
            }
        }

        /// <summary>
        /// Revokes all refresh tokens of one local user.
        /// </summary>
        /// <param name="userId">Id of the local user.</param>
        /// <param name="reason">Change that reduced the permissions, for the audit log.</param>
        /// <returns>The number of revoked refresh tokens.</returns>
        public async Task<int> RevokeSessionsOfUser(int userId, string reason)
        {
            List<int> userIds = new() { userId };
            return await RevokeSessions(userIds, reason);
        }

        /// <summary>
        /// Resolves the given user or group dns and revokes all refresh tokens of the local users they name.
        /// </summary>
        /// <param name="dns">User or group dns.</param>
        /// <param name="reason">Change that reduced the permissions, for the audit log.</param>
        /// <returns>The number of revoked refresh tokens.</returns>
        public async Task<int> RevokeSessionsOfDns(IEnumerable<string> dns, string reason)
        {
            return await RevokeSessions(await ResolveLocalUserIds(dns), reason);
        }
    }
}

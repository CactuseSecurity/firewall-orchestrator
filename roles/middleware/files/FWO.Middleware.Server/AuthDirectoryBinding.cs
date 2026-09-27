using System.Security.Authentication;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Keeps the authentication of a locally known user inside the directory it belongs to.
    /// A dn is unique only inside its LDAP connection: the same dn in another connection names a
    /// different subject (SEC-11). A user that is already known locally - a token refresh, a
    /// scheduled report, a request rebuilt from the caller's token - is therefore only looked up in
    /// its own directory and has to resolve to the same local user again.
    /// </summary>
    public static class AuthDirectoryBinding
    {
        private const string kLogTitle = "User Authentication";

        /// <summary>
        /// Determines the LDAP connection a user is bound to: the one given with the user, or else the
        /// one stored with its local user when a local db id is given.
        /// </summary>
        /// <param name="apiConnection">API connection used to read the stored local user.</param>
        /// <param name="user">User to authenticate.</param>
        /// <returns>The id of the LDAP connection to authenticate against, or 0 to search all LDAP connections.</returns>
        /// <exception cref="AuthenticationException">A local db id is given that does not exist.</exception>
        public static async Task<int> GetBoundLdapId(ApiConnection apiConnection, UiUser user)
        {
            int ldapId = user.LdapConnection?.Id ?? 0;
            if (ldapId > 0 || user.DbId <= 0)
            {
                return ldapId;
            }

            UiUser[] storedUsers = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = user.DbId });
            UiUser storedUser = storedUsers.FirstOrDefault()
                ?? throw new AuthenticationException("A0004 Local user not found.");
            return storedUser.LdapConnection?.Id ?? 0;
        }

        /// <summary>
        /// Selects the LDAP connections a user may be authenticated in.
        /// </summary>
        /// <param name="ldaps">All connected LDAPs.</param>
        /// <param name="boundLdapId">Id of the LDAP connection the user is bound to, or 0 if it is not bound.</param>
        /// <returns>The active LDAP connections, restricted to the bound one if there is one.</returns>
        public static List<Ldap> SelectCandidateLdaps(IEnumerable<Ldap> ldaps, int boundLdapId)
        {
            return ldaps.Where(ldap => ldap.Active && (boundLdapId <= 0 || ldap.Id == boundLdapId)).ToList();
        }

        /// <summary>
        /// Ensures that a locally known user resolved to its own local user again.
        /// </summary>
        /// <param name="expectedDbId">Local db id the user was given with, 0 if it was not known locally.</param>
        /// <param name="synchronizedUser">The user after its local user context was synchronized.</param>
        /// <exception cref="AuthenticationException">The user resolved to a different local user.</exception>
        public static void EnsureSameLocalUser(int expectedDbId, UiUser synchronizedUser)
        {
            if (expectedDbId > 0 && synchronizedUser.DbId != expectedDbId)
            {
                Log.WriteWarning(kLogTitle, $"User dn={synchronizedUser.Dn} in ldap id={synchronizedUser.LdapConnection?.Id} resolved to local user id={synchronizedUser.DbId} instead of id={expectedDbId}.");
                throw new AuthenticationException("A0003 User could not be resolved to its local user.");
            }
        }
    }
}

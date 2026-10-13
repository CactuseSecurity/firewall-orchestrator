using System.Security.Authentication;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Keeps a local user bound to one directory entry. A dn can be handed to another person, e.g. when an account is
    /// deleted and a later one gets the same name; that person must not take over the local user with its reports,
    /// templates, schedules and workflow history, nor continue its sessions (GHSA-695m-f6mv-9g72). The local user is
    /// therefore bound to the immutable object id of its directory entry, see <see cref="LdapDirectoryObjectId"/>.
    /// </summary>
    public static class UiUserDirectoryObjectBinding
    {
        private const string kLogTitle = "User Authentication";

        /// <summary>
        /// Message of the refusal when the dn of a local user now names another directory entry.
        /// </summary>
        public const string kDirectoryObjectChanged = "A0008 The directory entry of this user has changed. An administrator has to resolve the account before it can be used again.";

        /// <summary>
        /// Finds the local user of an authenticated directory entry and checks that it is still bound to that entry.
        /// </summary>
        /// <param name="apiConnection">API connection used to read and update local users.</param>
        /// <param name="user">The authenticated user with dn and directory object id as read from the directory.</param>
        /// <param name="ldapConnectionId">LDAP connection the user was authenticated in.</param>
        /// <param name="allowBinding">True to bind a local user that has no object id yet. Only a password login may do
        /// this: a refresh or a rebuild without password cannot tell whether the dn still names the same person.</param>
        /// <returns>The local user, or null if there is none yet.</returns>
        /// <exception cref="AuthenticationException">The local user of the dn is bound to another directory entry; its sessions were ended.</exception>
        public static async Task<UiUser?> ResolveLocalUser(ApiConnection apiConnection, UiUser user, int ldapConnectionId, bool allowBinding)
        {
            string? directoryObjectId = user.DirectoryObjectId;
            if (directoryObjectId != null)
            {
                UiUser? boundUser = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDirectoryObjectId,
                    new { ldapConnectionId, directoryObjectId })).FirstOrDefault();
                // compared exactly like the dn lookup below: a dn that only changed its case or escaping is followed too,
                // otherwise the lookup by the new dn finds no local user and a second one with the same object id is refused
                if (boundUser != null && !string.Equals(boundUser.Dn, user.Dn, StringComparison.Ordinal))
                {
                    return await FollowRename(apiConnection, boundUser, user, ldapConnectionId);
                }
            }

            UiUser? storedUser = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = user.Dn, ldapConnectionId })).FirstOrDefault();
            if (storedUser == null || string.Equals(storedUser.DirectoryObjectId, directoryObjectId, StringComparison.Ordinal))
            {
                return storedUser;
            }
            if (storedUser.DirectoryObjectId == null)
            {
                if (allowBinding && directoryObjectId != null)
                {
                    await Bind(apiConnection, storedUser, directoryObjectId, user.Dn);
                    Log.WriteInfo(kLogTitle, $"Bound local user id={storedUser.DbId} (dn={user.Dn}, ldap id={ldapConnectionId}) to directory object {directoryObjectId}.");
                }
                return storedUser;
            }
            await Refuse(apiConnection, storedUser, user, ldapConnectionId);
            return null;
        }

        /// <summary>
        /// Moves a local user to the new dn of its directory entry, which was renamed or moved.
        /// </summary>
        private static async Task<UiUser> FollowRename(ApiConnection apiConnection, UiUser boundUser, UiUser user, int ldapConnectionId)
        {
            UiUser? holderOfNewDn = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = user.Dn, ldapConnectionId })).FirstOrDefault();
            if (holderOfNewDn != null)
            {
                Log.WriteWarning(kLogTitle, $"Directory object {user.DirectoryObjectId} of local user id={boundUser.DbId} (dn={boundUser.Dn}) now has the dn {user.Dn}, " +
                    $"which local user id={holderOfNewDn.DbId} holds in ldap id={ldapConnectionId}. Delete one of the two local users to resolve this.");
                throw new AuthenticationException(kDirectoryObjectChanged);
            }

            await Bind(apiConnection, boundUser, user.DirectoryObjectId!, user.Dn);
            Log.WriteAudit(kLogTitle, $"Local user id={boundUser.DbId} in ldap id={ldapConnectionId} follows its renamed directory entry from dn={boundUser.Dn} to dn={user.Dn}.");
            boundUser.Dn = user.Dn;
            return boundUser;
        }

        private static async Task Bind(ApiConnection apiConnection, UiUser storedUser, string directoryObjectId, string dn)
        {
            await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.bindUiUserDirectoryObject, new { id = storedUser.DbId, directoryObjectId, uuid = dn });
            storedUser.DirectoryObjectId = directoryObjectId;
        }

        /// <summary>
        /// Ends all sessions of a local user whose dn now names another directory entry, and refuses the request.
        /// </summary>
        private static async Task Refuse(ApiConnection apiConnection, UiUser storedUser, UiUser user, int ldapConnectionId)
        {
            ReturnId revoked = await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.revokeRefreshTokensOfUser,
                new { userId = storedUser.DbId, revokedAt = DateTime.UtcNow });
            Log.WriteAudit(kLogTitle, $"Refused user dn={user.Dn} in ldap id={ldapConnectionId}: its local user id={storedUser.DbId} is bound to directory object " +
                $"{storedUser.DirectoryObjectId}, but the dn now names {user.DirectoryObjectId ?? "an entry without object id"}. Ended {revoked.AffectedRows} session(s) of the local user. " +
                "If the dn belongs to the same person again, clear uiuser.directory_object_id of the local user; otherwise delete the local user.");
            throw new AuthenticationException(kDirectoryObjectChanged);
        }
    }
}

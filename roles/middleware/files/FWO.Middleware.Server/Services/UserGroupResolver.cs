using FWO.Basics;
using FWO.Data;
using FWO.Logging;
using Novell.Directory.Ldap;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Resolves the ldap group memberships of a user.
/// The group dns are deliberately not carried in the JWT: their number grows with the group memberships of
/// the user and made the "Authorization" header exceed the web server's per-header size limit. They are
/// therefore resolved from ldap when they are actually needed.
/// </summary>
public class UserGroupResolver
{
    private readonly List<Ldap> ldaps;

    /// <summary>
    /// Constructor taking the list of configured ldap connections.
    /// </summary>
    /// <param name="ldaps">All configured ldap connections.</param>
    public UserGroupResolver(List<Ldap> ldaps)
    {
        this.ldaps = ldaps;
    }

    /// <summary>
    /// Resolves the group dns of an already loaded ldap entry.
    /// </summary>
    /// <param name="ldapUser">Ldap entry of the user.</param>
    /// <param name="hostingLdap">Ldap connection the entry was read from.</param>
    /// <param name="cancellationToken">Cancels the lookups when the login request ends or runs out of time.</param>
    /// <returns>Distinct list of group dns the user belongs to.</returns>
    public async Task<List<string>> GetGroups(LdapEntry ldapUser, Ldap hostingLdap, CancellationToken cancellationToken = default)
    {
        HashSet<string> userGroups = new(DistName.DnComparer);
        AddOwnedGroups(userGroups, hostingLdap.GetGroups(ldapUser), hostingLdap);
        AddResolvedGroupMemberships(userGroups, await GetGroupsForDn(hostingLdap, ldapUser.Dn, cancellationToken), hostingLdap);
        if (!hostingLdap.IsInternal())
        {
            await AddInternalLdapMemberships(userGroups, ldapUser.Dn, cancellationToken);
        }
        return userGroups.ToList();
    }

    /// <summary>
    /// Resolves the group dns of a user identified by its dn in the directory the user belongs to.
    /// Used outside the login flow, where no ldap entry has been read yet.
    /// </summary>
    /// <param name="userDn">Distinguished name of the user.</param>
    /// <param name="ldapId">Id of the LDAP connection of the user, taken from its local user. The dn alone does not
    /// identify the directory: the same dn in another directory names a different subject (GHSA-695m-f6mv-9g72).</param>
    /// <returns>Distinct list of group dns the user belongs to, empty if the user cannot be located in its directory.</returns>
    public async Task<List<string>> GetGroupsForUser(string userDn, int ldapId)
    {
        if (string.IsNullOrWhiteSpace(userDn))
        {
            return [];
        }

        Ldap? ldap = ldaps.FirstOrDefault(ldap => ldap.Active && ldap.Id == ldapId);
        if (ldap == null || !LdapDnNamespace.IsOwnedBy(ldaps, ldap.Id, userDn))
        {
            Log.WriteWarning("Resolve user groups", $"User {userDn} does not belong to an active ldap with id {ldapId}, assuming no group memberships.");
            return [];
        }

        // GetUserDetailsFromLdap logs and swallows connection problems itself, returning null
        LdapEntry? ldapUser = await ldap.GetUserDetailsFromLdap(userDn);
        if (ldapUser != null)
        {
            return await GetGroups(ldapUser, ldap);
        }

        Log.WriteWarning("Resolve user groups", $"User {userDn} could not be found in ldap id {ldapId}, assuming no group memberships.");
        return [];
    }

    private async Task AddInternalLdapMemberships(HashSet<string> userGroups, string userDn, CancellationToken cancellationToken)
    {
        object groupsLock = new();
        List<Task> ldapRoleRequests = [];

        foreach (Ldap currentLdap in ldaps.Where(ldap => ldap.Active && ldap.IsInternal()))
        {
            ldapRoleRequests.Add(Task.Run(async () =>
            {
                List<string> currentGroups = await GetGroupsForDn(currentLdap, userDn, cancellationToken);
                lock (groupsLock)
                {
                    AddResolvedGroupMemberships(userGroups, currentGroups, currentLdap);
                }
            }, cancellationToken));
        }
        await Task.WhenAll(ldapRoleRequests);
    }

    private static async Task<List<string>> GetGroupsForDn(Ldap ldap, string userDn, CancellationToken cancellationToken)
    {
        List<string> userDnList = [userDn];
        return await LdapAuthenticationGate.RunInSlotAsync(token => ldap.GetGroups(userDnList, token), cancellationToken);
    }

    private static string? GetGroupPath(Ldap ldap)
    {
        return !string.IsNullOrWhiteSpace(ldap.GroupSearchPath) ? ldap.GroupSearchPath : ldap.GroupWritePath;
    }

    private void AddResolvedGroupMemberships(HashSet<string> userGroups, IEnumerable<string> groupNames, Ldap ldap)
    {
        AddOwnedGroups(userGroups, Ldap.BuildGroupDns(groupNames, GetGroupPath(ldap)), ldap);
    }

    /// <summary>
    /// Adds only the group dns that the reporting ldap owns. A group dn that lies outside its paths or also
    /// belongs to another active connection would match grants recorded for a group of another directory.
    /// </summary>
    private void AddOwnedGroups(HashSet<string> userGroups, IEnumerable<string> groupDns, Ldap ldap)
    {
        foreach (string groupDn in groupDns)
        {
            if (LdapDnNamespace.IsOwnedBy(ldaps, ldap.Id, groupDn))
            {
                userGroups.Add(groupDn);
            }
            else
            {
                Log.WriteInfo("Resolve user groups", $"Ignoring group {groupDn} reported by ldap id {ldap.Id}: the dn is not within the paths of this connection or is also claimed by another active connection.");
            }
        }
    }
}

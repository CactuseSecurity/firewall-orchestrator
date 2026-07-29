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
    /// <returns>Distinct list of group dns the user belongs to.</returns>
    public async Task<List<string>> GetGroups(LdapEntry ldapUser, Ldap hostingLdap)
    {
        HashSet<string> userGroups = new(DistName.DnComparer);
        userGroups.UnionWith(hostingLdap.GetGroups(ldapUser));
        AddResolvedGroupMemberships(userGroups, await GetGroupsForDn(hostingLdap, ldapUser.Dn), GetGroupPath(hostingLdap));
        if (!hostingLdap.IsInternal())
        {
            await AddInternalLdapMemberships(userGroups, ldapUser.Dn);
        }
        return userGroups.ToList();
    }

    /// <summary>
    /// Resolves the group dns of a user identified by its dn, locating the hosting ldap first.
    /// Used outside the login flow, where no ldap entry has been read yet.
    /// </summary>
    /// <param name="userDn">Distinguished name of the user.</param>
    /// <returns>Distinct list of group dns the user belongs to, empty if the user cannot be located.</returns>
    public async Task<List<string>> GetGroupsForUserDn(string userDn)
    {
        if (string.IsNullOrWhiteSpace(userDn))
        {
            return [];
        }

        foreach (Ldap ldap in ldaps)
        {
            LdapEntry? ldapUser = await GetUserEntry(ldap, userDn);
            if (ldapUser != null)
            {
                return await GetGroups(ldapUser, ldap);
            }
        }

        Log.WriteWarning("Resolve user groups", $"User {userDn} could not be found in any ldap, assuming no group memberships.");
        return [];
    }

    private async Task AddInternalLdapMemberships(HashSet<string> userGroups, string userDn)
    {
        object groupsLock = new();
        List<Task> ldapRoleRequests = [];

        foreach (Ldap currentLdap in ldaps.Where(ldap => ldap.IsInternal()))
        {
            ldapRoleRequests.Add(Task.Run(async () =>
            {
                List<string> currentGroups = await GetGroupsForDn(currentLdap, userDn);
                lock (groupsLock)
                {
                    AddResolvedGroupMemberships(userGroups, currentGroups, GetGroupPath(currentLdap));
                }
            }));
        }
        await Task.WhenAll(ldapRoleRequests);
    }

    private static async Task<List<string>> GetGroupsForDn(Ldap ldap, string userDn)
    {
        List<string> userDnList = [userDn];
        return await ldap.GetGroups(userDnList);
    }

    private static async Task<LdapEntry?> GetUserEntry(Ldap ldap, string userDn)
    {
        try
        {
            return await ldap.GetUserDetailsFromLdap(userDn);
        }
        catch (Exception exception)
        {
            Log.WriteError("Resolve user groups", $"Could not read user {userDn} from ldap {ldap.Address}:{ldap.Port}.", exception);
            return null;
        }
    }

    private static string? GetGroupPath(Ldap ldap)
    {
        return !string.IsNullOrWhiteSpace(ldap.GroupSearchPath) ? ldap.GroupSearchPath : ldap.GroupWritePath;
    }

    private static void AddResolvedGroupMemberships(HashSet<string> userGroups, IEnumerable<string> groupNames, string? groupPath)
    {
        userGroups.UnionWith(Ldap.BuildGroupDns(groupNames, groupPath));
    }
}

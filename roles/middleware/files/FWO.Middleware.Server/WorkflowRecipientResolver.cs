using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;
using FWO.Services;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Resolves workflow recipients using middleware LDAP access and the local UI user cache.
    /// </summary>
    public class WorkflowRecipientResolver : IWorkflowRecipientResolver
    {
        private readonly ApiConnection apiConnection;
        private readonly List<Ldap> ldaps;

        /// <summary>
        /// Creates a workflow recipient resolver.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="ldaps">Configured LDAP connections.</param>
        public WorkflowRecipientResolver(ApiConnection apiConnection, List<Ldap> ldaps)
        {
            this.apiConnection = apiConnection;
            this.ldaps = ldaps;
        }

        /// <summary>
        /// Resolves mixed user or group DNs to user DNs.
        /// </summary>
        /// <param name="dns">User or group distinguished names.</param>
        /// <returns>Resolved user distinguished names.</returns>
        public async Task<List<string>> ResolveUserDns(IEnumerable<string> dns)
        {
            List<string> dnsList = dns.Where(dn => !string.IsNullOrWhiteSpace(dn)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (dnsList.Count == 0)
            {
                return [];
            }

            HashSet<string> resolvedDns = new(StringComparer.OrdinalIgnoreCase);
            foreach (Ldap ldap in ldaps.Where(ldap => ldap.HasGroupHandling()))
            {
                foreach (string resolvedDn in await ldap.ResolveUsersFromDns(dnsList))
                {
                    if (!string.IsNullOrWhiteSpace(resolvedDn))
                    {
                        resolvedDns.Add(resolvedDn);
                    }
                }
            }

            AddDirectUserDns(dnsList, resolvedDns);
            return resolvedDns.ToList();
        }

        /// <summary>
        /// Resolves mixed user or group DNs to UI users with email addresses when available.
        /// </summary>
        /// <param name="dns">User or group distinguished names.</param>
        /// <returns>Resolved UI users.</returns>
        public async Task<List<UiUser>> ResolveUsers(IEnumerable<string> dns)
        {
            List<string> resolvedDns = await ResolveUserDns(dns);
            if (resolvedDns.Count == 0)
            {
                return [];
            }

            Dictionary<string, UiUser> uiUsersByDn = (await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUserEmails))
                .Where(user => !string.IsNullOrWhiteSpace(user.Dn))
                .GroupBy(user => user.Dn, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            List<UiUser> resolvedUsers = [];
            foreach (string dn in resolvedDns)
            {
                if (uiUsersByDn.TryGetValue(dn, out UiUser? existingUser) && !string.IsNullOrWhiteSpace(existingUser.Email))
                {
                    resolvedUsers.Add(existingUser);
                    continue;
                }

                UiUser? ldapUser = await ResolveLdapUser(dn);
                if (ldapUser != null)
                {
                    await UiUserHandler.UpsertUiUser(apiConnection, ldapUser, false);
                    if (string.IsNullOrWhiteSpace(ldapUser.Email))
                    {
                        Log.WriteWarning("Workflow Recipients", $"LDAP user '{ldapUser.Dn}' was resolved but has no email address.");
                    }
                    resolvedUsers.Add(ldapUser);
                    continue;
                }

                if (existingUser != null)
                {
                    Log.WriteWarning("Workflow Recipients", $"User '{existingUser.Dn}' exists in uiuser but has no email address and could not be resolved from LDAP.");
                    resolvedUsers.Add(existingUser);
                }
                else
                {
                    Log.WriteWarning("Workflow Recipients", $"DN '{dn}' could not be resolved to a uiuser or LDAP user.");
                }
            }

            return resolvedUsers;
        }

        private void AddDirectUserDns(List<string> dnsList, HashSet<string> resolvedDns)
        {
            List<string> groupSearchPaths = ldaps
                .Where(ldap => ldap.HasGroupHandling())
                .SelectMany(ldap => new[] { ldap.GroupSearchPath, ldap.GroupWritePath })
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string dn in dnsList)
            {
                bool isGroupDn = groupSearchPaths.Any(path => dn.EndsWith(path, StringComparison.OrdinalIgnoreCase));
                if (!isGroupDn)
                {
                    resolvedDns.Add(dn);
                }
            }
        }

        private async Task<UiUser?> ResolveLdapUser(string dn)
        {
            foreach (Ldap ldap in ldaps)
            {
                if (!CanContainUser(ldap, dn))
                {
                    continue;
                }

                var ldapUser = await ldap.GetUserDetailsFromLdap(dn);
                if (ldapUser == null || Ldap.IsGroupEntry(ldapUser))
                {
                    continue;
                }

                return new()
                {
                    LdapConnection = new() { Id = ldap.Id },
                    Dn = ldapUser.Dn,
                    Name = Ldap.GetName(ldapUser),
                    Firstname = Ldap.GetFirstName(ldapUser),
                    Lastname = Ldap.GetLastName(ldapUser),
                    Email = Ldap.GetEmail(ldapUser),
                    Tenant = null
                };
            }
            return null;
        }

        private static bool CanContainUser(Ldap ldap, string dn)
        {
            return string.IsNullOrWhiteSpace(ldap.UserSearchPath)
                || dn.EndsWith(ldap.UserSearchPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}

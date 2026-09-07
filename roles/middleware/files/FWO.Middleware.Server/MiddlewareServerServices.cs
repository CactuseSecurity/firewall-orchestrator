using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class to execute handling of external requests
    /// </summary>
    public static class MiddlewareServerServices
    {
        /// <summary>
        /// get user groups from ldap
        /// </summary>
        /// <param name="ApiConnection"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public static async Task<List<UserGroup>> GetInternalGroups(ApiConnection ApiConnection)
        {
            List<Ldap> connectedLdaps = await ApiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
            return await GetInternalGroups(connectedLdaps);
        }

        /// <summary>
        /// get user groups from already loaded ldap connections
        /// </summary>
        /// <param name="connectedLdaps"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public static async Task<List<UserGroup>> GetInternalGroups(List<Ldap> connectedLdaps)
        {
            Ldap internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new KeyNotFoundException("No internal Ldap with group handling found.");

            List<GroupGetReturnParameters> allGroups = await internalLdap.GetAllInternalGroups();
            return BuildOwnerGroups(allGroups);
        }

        /// <summary>
        /// Converts LDAP group query results into owner groups.
        /// </summary>
        /// <param name="allGroups">LDAP group results to convert.</param>
        /// <returns>Owner groups derived from the LDAP data.</returns>
        private static List<UserGroup> BuildOwnerGroups(List<GroupGetReturnParameters> allGroups)
        {
            List<UserGroup> ownerGroups = [];
            foreach (GroupGetReturnParameters ldapUserGroup in allGroups)
            {
                if (!ldapUserGroup.OwnerGroup)
                {
                    continue;
                }

                UserGroup group = new()
                {
                    Dn = ldapUserGroup.GroupDn,
                    Name = new DistName(ldapUserGroup.GroupDn).Group,
                    OwnerGroup = ldapUserGroup.OwnerGroup
                };
                foreach (string userDn in ldapUserGroup.Members)
                {
                    UiUser newUser = new() { Dn = userDn, Name = new DistName(userDn).UserName };
                    group.Users.Add(newUser);
                }
                ownerGroups.Add(group);
            }
            return ownerGroups;
        }
    }
}

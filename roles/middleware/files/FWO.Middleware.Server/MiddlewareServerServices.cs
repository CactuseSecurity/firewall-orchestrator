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
            Ldap internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new KeyNotFoundException("No internal Ldap with group handling found.");

            List<GroupGetReturnParameters> allGroups = await internalLdap.GetAllInternalGroups();
            List<UserGroup> ownerGroups = [];
            foreach (var ldapUserGroup in allGroups)
            {
                if (ldapUserGroup.OwnerGroup)
                {
                    UserGroup group = new()
                    {
                        Dn = ldapUserGroup.GroupDn,
                        Name = new DistName(ldapUserGroup.GroupDn).Group,
                        OwnerGroup = ldapUserGroup.OwnerGroup
                    };
                    foreach (var userDn in ldapUserGroup.Members)
                    {
                        UiUser newUser = new() { Dn = userDn, Name = new DistName(userDn).UserName };
                        group.Users.Add(newUser);
                    }
                    ownerGroups.Add(group);
                }
            }
            return ownerGroups;
        }
    }
}

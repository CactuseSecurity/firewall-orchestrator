using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for tenant api
	/// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GroupController : ControllerBase
    {
        private readonly List<Ldap> ldaps;
        private readonly UserSessionRevocation sessionRevocation;

        /// <summary>
        /// Constructor needing ldap list and connection
        /// </summary>
        public GroupController(List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.ldaps = ldaps;
            sessionRevocation = new UserSessionRevocation(apiConnection, ldaps);
        }

        /// <summary>
        /// Get all groups
        /// </summary>
        /// <returns>List of groups</returns>
        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        public async Task<ActionResult<List<GroupGetReturnParameters>>> Get()
        {
            try
            {
                ConcurrentBag<GroupGetReturnParameters> allGroups = [];
                List<Task> ldapGroupRequests = [];

                foreach (Ldap currentLdap in ldaps)
                {
                    if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                    {
                        ldapGroupRequests.Add(Task.Run(async () =>
                        {
                            // Get all groups from internal Ldap
                            List<GroupGetReturnParameters> currentGroups = await currentLdap.GetAllInternalGroups();
                            foreach (GroupGetReturnParameters currentGroup in currentGroups)
                                allGroups.Add(currentGroup);
                        }));
                    }
                }
                await Task.WhenAll(ldapGroupRequests);

                // Return status and result
                return Ok(allGroups.ToList());
            }
            catch (Exception e)
            {
                return Problem(e.Message);
            }
        }

        // GET: GroupController/Create
        /// <summary>
        /// Add group to internal Ldap
        /// </summary>
        /// <remarks>
        /// GroupName (required) &#xA;
        /// OwnerGroup (optional) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupAddDeleteParameters</param>
        /// <returns>Dn of new group, empty string if no group could be created</returns>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<string> Create([FromBody] GroupAddDeleteParameters parameters)
        {
            string groupDn = "";
            List<Task> workers = [];

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add group to current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(async () =>
                    {
                        string actDn = await currentLdap.AddGroup(parameters.GroupName, parameters.OwnerGroup);
                        if (actDn != "")
                        {
                            groupDn = actDn;
                            Log.WriteAudit("AddGroup", $"group {parameters.GroupName} successfully added to {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            // Return status and result
            return groupDn;
        }

        // POST: GroupController/Delete/5
        /// <summary>
        /// Delete group in internal Ldap
        /// </summary>
        /// <remarks>
        /// GroupName (required) &#xA;
        /// OwnerGroup (optional) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupAddDeleteParameters</param>
        /// <returns>true if group deleted</returns>
        [HttpDelete]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> Delete([FromBody] GroupAddDeleteParameters parameters)
        {
            bool groupDeleted = false;
            List<Task> workers = [];
            // the members lose what the group granted them, and cannot be resolved once the group is gone
            List<int> affectedUserIds = await sessionRevocation.ResolveLocalUserIds(GetWritableGroupDns(parameters.GroupName));

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(async () =>
                    {
                        if (await currentLdap.DeleteGroup(parameters.GroupName))
                        {
                            groupDeleted = true;
                            Log.WriteAudit("DeleteGroup", $"Group {parameters.GroupName} deleted from {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            if (groupDeleted)
            {
                await sessionRevocation.RevokeSessions(affectedUserIds, $"Deleted group {parameters.GroupName}");
            }

            // Return status and result
            return groupDeleted;
        }

        /// <summary>
        /// Returns the dns the group of the given name has in the directories in which groups can be deleted.
        /// </summary>
        private List<string> GetWritableGroupDns(string groupName)
        {
            return [.. ldaps
                .Where(currentLdap => currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                .Select(currentLdap => $"cn={groupName},{currentLdap.GroupWritePath}")];
        }

        // POST: GroupController/Edit/5
        /// <summary>
        /// Update group (name) in internal Ldap
        /// </summary>
        /// <remarks>
        /// OldGroupName (required) &#xA;
        /// NewGroupName (required) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupEditParameters</param>
        /// <returns>Dn of updated group</returns>
        [HttpPut]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<string> Edit([FromBody] GroupEditParameters parameters)
        {
            string groupUpdatedDn = "";
            List<Task> workers = [];

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to update group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(async () =>
                    {
                        string newDn = await currentLdap.UpdateGroup(parameters.OldGroupName, parameters.NewGroupName);
                        if (newDn != "")
                        {
                            groupUpdatedDn = newDn;
                            Log.WriteAudit("UpdateGroup", $"Group {parameters.OldGroupName} updated to {parameters.NewGroupName} in {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            // Return status and result
            return groupUpdatedDn;
        }

        /// <summary>
        /// Search group in specified Ldap
        /// </summary>
        /// <remarks>
        /// LdapId (required): id of the LDAP connection to search. A group dn names a group only inside its directory,
        /// so the search covers exactly one connection. &#xA;
        /// SearchPattern (optional) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupGetParameters</param>
        /// <returns>List of groups</returns>
        /// <response code="400">LdapId does not name a connected LDAP.</response>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<string>>> Get([FromBody] GroupGetParameters parameters)
        {
            Ldap? ldap = ldaps.FirstOrDefault(ldap => ldap.Id == parameters.LdapId);
            if (parameters.LdapId <= 0 || ldap == null)
            {
                Dictionary<string, string[]> errors = [];
                errors.Add("ldapId", new List<string> { "LdapId has to name a connected LDAP connection." }.ToArray());
                return BadRequest(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
            }

            // Return status and result
            return ldap.HasGroupHandling() ? await ldap.GetAllGroups(parameters.SearchPattern) : [];
        }

        /// <summary>
        /// Get members of a group by dn if ldap supports group handling
        /// </summary>
        /// <param name="parameters">GroupMemberGetParameters</param>
        /// <returns>List of member dns</returns>
        [HttpPost("Members")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        public async Task<List<string>> GetMembers([FromBody] GroupMemberGetParameters parameters)
        {
            List<string> members = [];

            if (string.IsNullOrWhiteSpace(parameters.GroupDn))
            {
                return members;
            }

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.HasGroupHandling() && !string.IsNullOrWhiteSpace(currentLdap.GroupSearchPath)
                    && parameters.GroupDn.EndsWith(currentLdap.GroupSearchPath!, StringComparison.OrdinalIgnoreCase))
                {
                    members = await currentLdap.GetGroupMembers(parameters.GroupDn);
                    break;
                }
            }

            return members;
        }

        /// <summary>
        /// Get all group memberships for a user across connected ldaps
        /// </summary>
        /// <param name="parameters">GroupMembershipGetParameters</param>
        /// <returns>List of group dns</returns>
        [HttpPost("Memberships")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        public async Task<List<string>> GetMemberships([FromBody] GroupMembershipGetParameters parameters)
        {
            HashSet<string> memberships = new(DistName.DnComparer);
            if (string.IsNullOrWhiteSpace(parameters.UserDn) && string.IsNullOrWhiteSpace(parameters.UserName))
            {
                return [];
            }

            object membershipsLock = new();
            List<Task> ldapRequests = [];

            foreach (Ldap currentLdap in ldaps)
            {
                string? groupPath = !string.IsNullOrWhiteSpace(currentLdap.GroupSearchPath)
                    ? currentLdap.GroupSearchPath
                    : currentLdap.GroupWritePath;
                if (!currentLdap.HasGroupHandling() || string.IsNullOrWhiteSpace(groupPath))
                {
                    continue;
                }

                ldapRequests.Add(Task.Run(async () =>
                {
                    List<string> currentGroups = await GetMembershipsFromLdap(currentLdap, parameters);
                    if (currentGroups.Count == 0)
                    {
                        return;
                    }

                    lock (membershipsLock)
                    {
                        foreach (string groupDn in currentGroups)
                        {
                            memberships.Add(groupDn);
                        }
                    }
                }));
            }

            await Task.WhenAll(ldapRequests);
            return new List<string>(memberships);
        }

        /// <summary>
        /// Resolve user dns from a list of user or group dns across connected ldaps
        /// </summary>
        /// <param name="parameters">GroupResolveParameters</param>
        /// <returns>List of user dns</returns>
        [HttpPost("Resolve")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        public async Task<List<string>> ResolveMembers([FromBody] GroupResolveParameters parameters)
        {
            if (parameters == null || parameters.Dns.Count == 0)
            {
                return [];
            }

            HashSet<string> resolved = new(DistName.DnComparer);
            object resolvedLock = new();
            await ResolveFromLdaps(parameters.Dns, resolved, resolvedLock);
            AddDirectDns(parameters.Dns, resolved, GetGroupSearchPaths());

            return resolved.ToList();
        }

        private async Task ResolveFromLdaps(List<string> dns, HashSet<string> resolved, object resolvedLock)
        {
            List<Task> ldapRequests = [];

            foreach (Ldap currentLdap in ldaps)
            {
                if (!currentLdap.HasGroupHandling())
                {
                    continue;
                }

                ldapRequests.Add(Task.Run(async () =>
                {
                    List<string> currentResolved = await currentLdap.ResolveUsersFromDns(dns);
                    if (currentResolved.Count == 0)
                    {
                        return;
                    }

                    lock (resolvedLock)
                    {
                        foreach (string dn in currentResolved)
                        {
                            if (!string.IsNullOrWhiteSpace(dn))
                            {
                                resolved.Add(dn);
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(ldapRequests);
        }

        private List<string> GetGroupSearchPaths()
        {
            return ldaps
                .Where(ldap => ldap.HasGroupHandling() && !string.IsNullOrWhiteSpace(ldap.GroupSearchPath))
                .Select(ldap => ldap.GroupSearchPath!)
                .ToList();
        }

        private static void AddDirectDns(List<string> dns, HashSet<string> resolved, List<string> groupSearchPaths)
        {
            foreach (string dn in dns)
            {
                if (string.IsNullOrWhiteSpace(dn))
                {
                    continue;
                }

                bool isGroupDn = groupSearchPaths.Any(path => dn.EndsWith(path, StringComparison.OrdinalIgnoreCase));
                if (!isGroupDn)
                {
                    resolved.Add(dn);
                }
            }
        }

        private async Task<List<string>> GetMembershipsFromLdap(Ldap currentLdap, GroupMembershipGetParameters parameters)
        {
            List<string> memberships = [];
            UiUser userToSearch = new() { Dn = parameters.UserDn, Name = parameters.UserName };
            LdapEntry? ldapUser = null;

            if (!string.IsNullOrWhiteSpace(parameters.UserDn)
                && !string.IsNullOrWhiteSpace(currentLdap.UserSearchPath)
                && parameters.UserDn.EndsWith(currentLdap.UserSearchPath, StringComparison.OrdinalIgnoreCase))
            {
                ldapUser = await currentLdap.GetLdapEntry(userToSearch, false);
            }
            else if (!string.IsNullOrWhiteSpace(parameters.UserName))
            {
                userToSearch.Dn = "";
                ldapUser = await currentLdap.GetLdapEntry(userToSearch, false);
            }

            if (ldapUser != null)
            {
                memberships = currentLdap.GetGroups(ldapUser);
                if (memberships.Count == 0)
                {
                    memberships = await GetMembershipsByMemberDn(currentLdap, ldapUser.Dn);
                }
            }
            else if (!string.IsNullOrWhiteSpace(parameters.UserDn))
            {
                memberships = await GetMembershipsByMemberDn(currentLdap, parameters.UserDn);
            }

            return memberships;
        }

        private async Task<List<string>> GetMembershipsByMemberDn(Ldap currentLdap, string userDn)
        {
            List<string> memberships = [];
            string? groupPath = !string.IsNullOrWhiteSpace(currentLdap.GroupSearchPath)
                ? currentLdap.GroupSearchPath
                : currentLdap.GroupWritePath;
            if (string.IsNullOrWhiteSpace(userDn) || string.IsNullOrWhiteSpace(groupPath))
            {
                return memberships;
            }

            List<string> groupNames = await currentLdap.GetGroups([userDn]);
            foreach (string groupName in groupNames)
            {
                memberships.Add($"cn={groupName},{groupPath}");
                if (!string.IsNullOrWhiteSpace(currentLdap.GroupSearchPath)
                    && !string.IsNullOrWhiteSpace(currentLdap.GroupWritePath)
                    && !currentLdap.GroupSearchPath.Equals(currentLdap.GroupWritePath, StringComparison.OrdinalIgnoreCase))
                {
                    memberships.Add($"cn={groupName},{currentLdap.GroupWritePath}");
                }
            }
            return memberships;
        }

        // GET: GroupController/
        /// <summary>
        /// Add user to group
        /// </summary>
        /// <remarks>
        /// UserDn (required) &#xA;
        /// GroupDn (required) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupAddDeleteUserParameters</param>
        /// <returns>true if user could be added to group</returns>
        [HttpPost("User")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> AddUser([FromBody] GroupAddDeleteUserParameters parameters)
        {
            bool userAdded = false;
            List<Task> workers = [];

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add user to group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(async () =>
                    {
                        if (await currentLdap.AddUserToEntry(parameters.UserDn, parameters.GroupDn))
                        {
                            userAdded = true;
                            Log.WriteAudit("AddUserToGroup", $"user {parameters.UserDn} successfully added to group {parameters.GroupDn} in {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            // Return status and result
            return userAdded;
        }

        // GET: GroupController/Details/5
        /// <summary>
        /// Remove user from group
        /// </summary>
        /// <remarks>
        /// UserDn (required) &#xA;
        /// GroupDn (required) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupAddDeleteUserParameters</param>
        /// <returns>true if user could be removed from group</returns>        [HttpDelete("User")]
        [HttpDelete("User")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> RemoveUser([FromBody] GroupAddDeleteUserParameters parameters)
        {
            bool userRemoved = false;
            List<Task> workers = [];
            // the dn may name a nested group, whose members leave the group as well
            List<string> removedDns = new() { parameters.UserDn };
            List<int> affectedUserIds = await sessionRevocation.ResolveLocalUserIds(removedDns);

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to remove user from group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(async () =>
                    {
                        if (await currentLdap.RemoveUserFromEntry(parameters.UserDn, parameters.GroupDn))
                        {
                            userRemoved = true;
                            Log.WriteAudit("RemoveUserFromGroup", $"Removed user {parameters.UserDn} from {parameters.GroupDn} in {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            if (userRemoved)
            {
                await sessionRevocation.RevokeSessions(affectedUserIds, $"Removed {parameters.UserDn} from group {parameters.GroupDn}");
            }

            // Return status and result
            return userRemoved;
        }
    }
}

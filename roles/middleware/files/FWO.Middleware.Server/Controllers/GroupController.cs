using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Constructor needing ldap list, connection and the global configuration holding the maximum page size
        /// </summary>
        public GroupController(List<Ldap> ldaps, ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.ldaps = ldaps;
            this.globalConfig = globalConfig;
            sessionRevocation = new UserSessionRevocation(apiConnection, ldaps);
        }

        /// <summary>
        /// Maximum and default page size of the list endpoints, read from the configuration at request time.
        /// </summary>
        private int MaxLimit => ListPaging.GetMaxObjectLimit(globalConfig);

        /// <summary>
        /// Get all groups
        /// </summary>
        /// <remarks>
        /// Results are ordered by group dn, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> groups (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further groups follow.
        /// The query parameters <c>limit</c> and <c>offset</c> select the page.
        /// </remarks>
        /// <param name="paging">Optional query parameters <c>limit</c> and <c>offset</c>.</param>
        /// <returns>List of groups</returns>
        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
        [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<GroupGetReturnParameters>>> Get([FromQuery] PagingParameters paging)
        {
            int maxLimit = MaxLimit;
            if (!ListPaging.TryValidate(paging, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

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
                IEnumerable<GroupGetReturnParameters> orderedGroups = allGroups
                    .OrderBy(group => group.GroupDn, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(group => group.GroupDn, StringComparer.Ordinal);
                return Ok(ListPaging.TakePage(HttpContext, orderedGroups, paging, maxLimit));
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
        /// Limit (optional), Offset (optional) &#xA;
        /// Results are ordered by group dn, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> groups (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further groups follow.
        /// </remarks>
        /// <param name="parameters">GroupGetParameters</param>
        /// <returns>List of groups</returns>
        /// <response code="400">LdapId does not name a connected LDAP, or limit or offset is out of range.</response>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
        [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<string>>> Get([FromBody] GroupGetParameters parameters)
        {
            int maxLimit = MaxLimit;
            Ldap? ldap = ldaps.FirstOrDefault(ldap => ldap.Id == parameters.LdapId);
            Dictionary<string, string[]> errors = ListPaging.ValidatePaging(parameters.Limit, parameters.Offset, maxLimit);
            if (parameters.LdapId <= 0 || ldap == null)
            {
                errors.Add("ldapId", new List<string> { "LdapId has to name a connected LDAP connection." }.ToArray());
            }
            if (errors.Count > 0)
            {
                return BadRequest(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
            }

            // Return status and result
            List<string> groups = ldap!.HasGroupHandling() ? await ldap.GetAllGroups(parameters.SearchPattern) : [];
            return ListPaging.TakePage(HttpContext, OrderDns(groups), parameters, maxLimit);
        }

        /// <summary>
        /// Get members of a group by dn if ldap supports group handling
        /// </summary>
        /// <remarks>
        /// Results are ordered by member dn, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> members (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further members follow.
        /// </remarks>
        /// <param name="parameters">GroupMemberGetParameters</param>
        /// <returns>List of member dns</returns>
        [HttpPost("Members")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
        [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<string>>> GetMembers([FromBody] GroupMemberGetParameters parameters)
        {
            int maxLimit = MaxLimit;
            if (!ListPaging.TryValidate(parameters, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

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

            return ListPaging.TakePage(HttpContext, OrderDns(members), parameters, maxLimit);
        }

        /// <summary>
        /// Get all group memberships for a user across connected ldaps
        /// </summary>
        /// <remarks>
        /// Results are ordered by group dn, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> groups (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further groups follow.
        /// </remarks>
        /// <param name="parameters">GroupMembershipGetParameters</param>
        /// <returns>List of group dns</returns>
        [HttpPost("Memberships")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
        [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<string>>> GetMemberships([FromBody] GroupMembershipGetParameters parameters)
        {
            int maxLimit = MaxLimit;
            if (!ListPaging.TryValidate(parameters, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

            HashSet<string> memberships = new(DistName.DnComparer);
            if (string.IsNullOrWhiteSpace(parameters.UserDn) && string.IsNullOrWhiteSpace(parameters.UserName))
            {
                return new List<string>();
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
            return ListPaging.TakePage(HttpContext, OrderDns(memberships), parameters, maxLimit);
        }

        /// <summary>
        /// Resolve user dns from a list of user or group dns across connected ldaps
        /// </summary>
        /// <remarks>
        /// Results are ordered by user dn, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> user dns (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further user dns follow.
        /// </remarks>
        /// <param name="parameters">GroupResolveParameters</param>
        /// <returns>List of user dns</returns>
        [HttpPost("Resolve")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Recertifier}, {Roles.Modeller}")]
        [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
        [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<string>>> ResolveMembers([FromBody] GroupResolveParameters parameters)
        {
            if (parameters == null || parameters.Dns.Count == 0)
            {
                return new List<string>();
            }

            int maxLimit = MaxLimit;
            if (!ListPaging.TryValidate(parameters, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

            HashSet<string> resolved = new(DistName.DnComparer);
            object resolvedLock = new();
            await ResolveFromLdaps(parameters.Dns, resolved, resolvedLock);
            AddDirectDns(parameters.Dns, resolved, GetGroupSearchPaths());

            return ListPaging.TakePage(HttpContext, OrderDns(resolved), parameters, maxLimit);
        }

        /// <summary>
        /// Orders dns deterministically for paging: case-insensitively, ties broken by the exact spelling.
        /// </summary>
        private static IEnumerable<string> OrderDns(IEnumerable<string> dns)
        {
            return dns.OrderBy(dn => dn, StringComparer.OrdinalIgnoreCase).ThenBy(dn => dn, StringComparer.Ordinal);
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

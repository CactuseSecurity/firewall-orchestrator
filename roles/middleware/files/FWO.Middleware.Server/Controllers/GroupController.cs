using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Logging;
using FWO.Middleware.RequestParameters;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace FWO.Middleware.Controllers
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

		/// <summary>
		/// Constructor needing ldap list
		/// </summary>
        public GroupController(List<Ldap> ldaps)
        {
            this.ldaps = ldaps;
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
                ConcurrentBag<GroupGetReturnParameters> allGroups = new ConcurrentBag<GroupGetReturnParameters>();
                List<Task> ldapGroupRequests = new List<Task>();

                foreach (Ldap currentLdap in ldaps)
                {
                    if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                    {
                        ldapGroupRequests.Add(Task.Run(() =>
                        {
                            // Get all groups from internal Ldap
                            List<GroupGetReturnParameters> currentGroups = currentLdap.GetAllInternalGroups();
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
            List<Task> workers = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add group to current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        string actDn = currentLdap.AddGroup(parameters.GroupName, parameters.OwnerGroup);
                        if(actDn != "")
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
            List<Task> workers = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        if(currentLdap.DeleteGroup(parameters.GroupName))
                        {
                            groupDeleted = true;
                            Log.WriteAudit("DeleteGroup", $"Group {parameters.GroupName} deleted from {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            // Return status and result
            return groupDeleted;
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
            List<Task> workers = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to update group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        string newDn = currentLdap.UpdateGroup(parameters.OldGroupName, parameters.NewGroupName);
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
        /// LdapId (required) &#xA;
        /// SearchPattern (optional) &#xA;
        /// </remarks>
        /// <param name="parameters">GroupGetParameters</param>
        /// <returns>List of groups</returns>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        public async Task<List<string>> Get([FromBody] GroupGetParameters parameters)
        {
            List<string> allGroups = new List<string>();

            foreach (Ldap currentLdap in ldaps)
            {
                if ((currentLdap.Id == parameters.LdapId || parameters.LdapId == 0) && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        // Get all groups from current Ldap
                        allGroups = currentLdap.GetAllGroups(parameters.SearchPattern);
                    });
                }
            }

            // Return status and result
            return allGroups;
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
            List<Task> workers = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add user to group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        if(currentLdap.AddUserToEntry(parameters.UserDn, parameters.GroupDn))
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
            List<Task> workers = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to remove user from group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        if(currentLdap.RemoveUserFromEntry(parameters.UserDn, parameters.GroupDn))
                        {
                            userRemoved = true;
                            Log.WriteAudit("RemoveUserFromGroup", $"Removed user {parameters.UserDn} from {parameters.GroupDn} in {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(workers);

            // Return status and result
            return userRemoved;
        }
    }
}

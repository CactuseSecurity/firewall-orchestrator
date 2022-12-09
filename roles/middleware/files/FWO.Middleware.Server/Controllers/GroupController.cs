using FWO.Logging;
using FWO.Middleware.RequestParameters;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace FWO.Middleware.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GroupController : ControllerBase
    {
        private readonly List<Ldap> ldaps;

        public GroupController(List<Ldap> ldaps)
        {
            this.ldaps = ldaps;
        }

        [HttpGet]
        [Authorize(Roles = "admin, auditor")]
        public async Task<ActionResult<List<GroupGetReturnParameters>>> Get()
        {
            bool admin = User.IsInRole("admin");
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
        [HttpPost]
        [Authorize(Roles = "admin")]
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
        [HttpDelete]
        [Authorize(Roles = "admin")]
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
        [HttpPut]
        [Authorize(Roles = "admin")]
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

        [HttpPost("Get")]
        [Authorize(Roles = "admin, auditor")]
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
        [HttpPost("User")]
        [Authorize(Roles = "admin")]
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
        [HttpDelete("User")]
        [Authorize(Roles = "admin")]
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

using FWO.Api.Data;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

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

        [HttpGet("{ldapDn}")]
        [Authorize(Roles = "admin")]
        public async Task<List<string>> GetAsync(string ldapDn, string searchPattern)
        {
            List<string> allGroups = new List<string>();

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.Host() == ldapDn && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        // Get all groups from current Ldap
                        allGroups = currentLdap.GetAllGroups(searchPattern);
                    });
                }
            }

            // Return status and result
            return allGroups;
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<KeyValuePair<string, List<string>>[]> GetInternalAsync()
        {
            ConcurrentBag<KeyValuePair<string, List<string>>> allGroups = new ConcurrentBag<KeyValuePair<string, List<string>>>();
            List<Task> ldapGroupRequests = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                {
                    ldapGroupRequests.Add(Task.Run(() =>
                    {
                        // Get all groups from internal Ldap
                        List<KeyValuePair<string, List<string>>> currentGroups = currentLdap.GetAllInternalGroups();
                        foreach (KeyValuePair<string, List<string>> currentGroup in currentGroups)
                            allGroups.Add(currentGroup);
                    }));
                }
            }

            await Task.WhenAll(ldapGroupRequests);

            // Return status and result
            return allGroups.ToArray();
        }

        // GET: GroupController/Create
        [HttpPost("{groupDn}")]
        [Authorize(Roles = "admin")]
        public async Task<string> Create(string groupDn)
        {
            List<Task> workers = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add group to current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        string groupAdded = currentLdap.AddGroup(groupDn);
                        if (groupAdded != "") Log.WriteAudit("AddGroup", $"group {groupAdded} successfully added to {currentLdap.Host()}");
                    }));
                }
            }

            await Task.WhenAll(workers);

            // Return status and result
            return "Success";
        }

        // POST: GroupController/Delete/5
        [HttpDelete("{groupDn}")]
        [Authorize(Roles = "admin")]
        public async Task<bool> Delete(string groupDn)
        {
            List<Task<bool>> workers = new List<Task<bool>>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        bool groupDeleted = currentLdap.DeleteGroup(groupDn);
                        if (groupDeleted) Log.WriteAudit("DeleteGroup", $"Group {groupDn} deleted from {currentLdap.Host()}");
                        return groupDeleted;
                    }));
                }
            }
            await Task.WhenAll(workers);

            // If group was deleted on any ldap => success
            bool result = false;
            workers.ForEach(worker => result = result || worker.Result);

            // Return status and result
            return result;
        }


        // POST: GroupController/Edit/5
        [HttpPut("{oldDn}")]
        [Authorize(Roles = "admin")]
        public async Task<string> Edit(string oldDn, string newDn)
        {
            string groupUpdated = "";

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to update group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        groupUpdated = currentLdap.UpdateGroup(oldDn, newDn);
                        if (groupUpdated != "") Log.WriteAudit("UpdateGroup", $"Group {oldDn} updated to {newDn} in {currentLdap.Host()}");
                    });
                }
            }

            // Return status and result
            return groupUpdated;
        }

        public class Test
        {
            public string Dn { get; set; }
            public string groupDn { get; set; }
        }

        // GET: GroupController/
        [HttpPost("{groupDn}/{userDn}")]
        [Authorize(Roles = "admin")]
        public async Task<bool> AddUser(string userDn, string groupDn)
        {
            List<Task<bool>> workers = new List<Task<bool>>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add user to group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        bool userAdded = currentLdap.AddUserToEntry(userDn, groupDn);
                        if (userAdded) Log.WriteAudit("AddUserToGroup", $"user {userDn} successfully added to group {groupDn} in {currentLdap.Host()}");
                        return userAdded;
                    }));
                }
            }
            await Task.WhenAll(workers);

            // If user was added on any ldap => success
            bool result = false;
            workers.ForEach(worker => result = result || worker.Result);

            // Return status and result
            return result;
        }

        // GET: GroupController/Details/5
        [HttpDelete("{groupDn}/{userDn}")]
        [Authorize(Roles = "admin")]
        public async Task<bool> RemoveUser(Test user)
        {
            List<Task<bool>> workers = new List<Task<bool>>();
            string userDn = user.Dn;
            string groupDn = user.groupDn;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to remove user from group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    workers.Add(Task.Run(() =>
                    {
                        bool userRemoved = currentLdap.RemoveUserFromEntry(userDn, groupDn);
                        if (userRemoved) Log.WriteAudit("RemoveUserFromGroup", $"Removed user {userDn} from {groupDn} in {currentLdap.Host()}");
                        return userRemoved;
                    }));
                }
            }
            await Task.WhenAll(workers);

            // If group was deleted on any ldap => success
            bool result = false;
            workers.ForEach(worker => result = result || worker.Result);

            // Return status and result
            return result;
        }
    }
}

﻿using FWO.Logging;
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
    public class RoleController : ControllerBase
    {
        private readonly List<Ldap> ldaps;

        public RoleController(List<Ldap> ldaps)
        {
            this.ldaps = ldaps;
        }

        // GET: api/<ValuesController>
        [HttpGet]
        [Authorize(Roles = "admin, auditor")]
        public async Task<KeyValuePair<string, List<KeyValuePair<string, string>>>[]> Get()
        {
            // No parameters
            ConcurrentBag<KeyValuePair<string, List<KeyValuePair<string, string>>>> allRoles = new ConcurrentBag<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
            ConcurrentBag<Task> ldapRoleRequests = new ConcurrentBag<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(() =>
                    {
                        // if current Ldap has roles stored: Get all roles from current Ldap
                        List<KeyValuePair<string, List<KeyValuePair<string, string>>>> currentRoles = currentLdap.GetAllRoles();
                        foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> role in currentRoles)
                            allRoles.Add(role);
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return allRoles.ToArray();
        }

        [HttpPost("User")]
        [Authorize(Roles = "admin")]
        public async Task<bool> AddUser([FromBody] RoleAddDeleteUserParameters parameters)
        {
            bool userAdded = false;
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add user to role in current Ldap
                if (currentLdap.IsWritable() && currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(() =>
                    {
                        if (currentLdap.AddUserToEntry(parameters.UserDn, parameters.Role))
                        {
                            userAdded = true;
                            Log.WriteAudit("AddUserToRole", $"user {parameters.UserDn} successfully added to role {parameters.Role} in {currentLdap.Host()}");
                        }
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return userAdded;
        }

        [HttpDelete("User")]
        [Authorize(Roles = "admin")]
        public async Task<bool> RemoveUser([FromBody] RoleAddDeleteUserParameters parameters)
        {
            bool userRemoved = false;
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to remove user from role in current Ldap
                if (currentLdap.IsWritable() && currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(() =>
                    {
                        if (currentLdap.RemoveUserFromEntry(parameters.UserDn, parameters.Role))
                        {
                            userRemoved = true;
                            Log.WriteAudit("RemoveUserFromRole", $"Removed user {parameters.UserDn} from {parameters.Role} in {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return userRemoved;
        }
    }
}

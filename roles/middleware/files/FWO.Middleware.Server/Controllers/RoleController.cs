using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for role api
	/// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly List<Ldap> ldaps;
        private readonly UserSessionRevocation sessionRevocation;

        /// <summary>
        /// Constructor needing ldap list and connection
        /// </summary>
        public RoleController(List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.ldaps = ldaps;
            sessionRevocation = new UserSessionRevocation(apiConnection, ldaps);
        }

        // GET: api/<ValuesController>
        /// <summary>
        /// Get all roles
        /// </summary>
        /// <returns>List of roles</returns>
        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.WorkflowRolesList}")]
        public async Task<List<RoleGetReturnParameters>> Get()
        {
            // No parameters
            ConcurrentBag<RoleGetReturnParameters> allRoles = [];
            ConcurrentBag<Task> ldapRoleRequests = [];

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(async () =>
                    {
                        // if current Ldap has roles stored: Get all roles from current Ldap
                        List<RoleGetReturnParameters> currentRoles = await currentLdap.GetAllRoles();
                        foreach (RoleGetReturnParameters role in currentRoles)
                            allRoles.Add(role);
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return [.. allRoles];
        }

        /// <summary>
        /// Add user to role
        /// </summary>
        /// <remarks>
        /// Role (required) &#xA;
        /// UserDn (required) &#xA;
        /// </remarks>
        /// <param name="parameters">RoleAddDeleteUserParameters</param>
        /// <returns>true if user could be added to role</returns>
        [HttpPost("User")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> AddUser([FromBody] RoleAddDeleteUserParameters parameters)
        {
            bool userAdded = false;
            List<Task> ldapRoleRequests = [];

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add user to role in current Ldap
                if (currentLdap.IsWritable() && currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(async () =>
                    {
                        if (await currentLdap.AddUserToEntry(parameters.UserDn, parameters.Role))
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

        /// <summary>
        /// Remove user from role
        /// </summary>
        /// <remarks>
        /// Role (required) &#xA;
        /// UserDn (required) &#xA;
        /// </remarks>
        /// <param name="parameters">RoleAddDeleteUserParameters</param>
        /// <returns>true if user could be removed from role</returns>
        [HttpDelete("User")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> RemoveUser([FromBody] RoleAddDeleteUserParameters parameters)
        {
            bool userRemoved = false;
            List<Task> ldapRoleRequests = [];
            // the dn may name a group, whose members lose the role as well
            List<string> removedDns = new() { parameters.UserDn };
            List<int> affectedUserIds = await sessionRevocation.ResolveLocalUserIds(removedDns);

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to remove user from role in current Ldap
                if (currentLdap.IsWritable() && currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(async () =>
                    {
                        if (await currentLdap.RemoveUserFromEntry(parameters.UserDn, parameters.Role))
                        {
                            userRemoved = true;
                            Log.WriteAudit("RemoveUserFromRole", $"Removed user {parameters.UserDn} from {parameters.Role} in {currentLdap.Host()}");
                        }
                    }));
                }
            }
            await Task.WhenAll(ldapRoleRequests);

            if (userRemoved)
            {
                await sessionRevocation.RevokeSessions(affectedUserIds, $"Removed {parameters.UserDn} from role {parameters.Role}");
            }

            // Return status and result
            return userRemoved;
        }
    }
}

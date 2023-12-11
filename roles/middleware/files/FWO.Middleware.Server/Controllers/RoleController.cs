using FWO.Config.Api;
using FWO.Logging;
using FWO.Middleware.RequestParameters;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace FWO.Middleware.Controllers
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

		/// <summary>
		/// Constructor needing ldap list
		/// </summary>
        public RoleController(List<Ldap> ldaps)
        {
            this.ldaps = ldaps;
        }

        // GET: api/<ValuesController>
        /// <summary>
        /// Get all roles
        /// </summary>
        /// <returns>List of roles</returns>
        [HttpGet]
        [Authorize(Roles = $"{GlobalConst.kAdmin}, {GlobalConst.kAuditor}, {GlobalConst.kFwAdmin}, {GlobalConst.kRequester}, {GlobalConst.kApprover}, {GlobalConst.kPlanner}, {GlobalConst.kImplementer}, {GlobalConst.kReviewer}")]
        public async Task<List<RoleGetReturnParameters>> Get()
        {
            // No parameters
            ConcurrentBag<RoleGetReturnParameters> allRoles = new ConcurrentBag<RoleGetReturnParameters>();
            ConcurrentBag<Task> ldapRoleRequests = new ConcurrentBag<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(() =>
                    {
                        // if current Ldap has roles stored: Get all roles from current Ldap
                        List<RoleGetReturnParameters> currentRoles = currentLdap.GetAllRoles();
                        foreach (RoleGetReturnParameters role in currentRoles)
                            allRoles.Add(role);
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return allRoles.ToList();
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
        [Authorize(Roles = $"{GlobalConst.kAdmin}")]
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
        [Authorize(Roles = $"{GlobalConst.kAdmin}")]
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

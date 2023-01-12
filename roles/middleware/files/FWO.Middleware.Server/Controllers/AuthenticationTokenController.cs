using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Mvc;
using FWO.Middleware.RequestParameters;
using System.Security.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Novell.Directory.Ldap;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace FWO.Middleware.Controllers
{
    /// <summary>
    /// Authentication token generation. Token is of type JSON web token (JWT).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationTokenController : ControllerBase
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;

		/// <summary>
		/// Constructor needing jwt writer, ldap list and connection
		/// </summary>
        public AuthenticationTokenController(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Generates an authentication token (jwt) given valid credentials.  
        /// </summary>
        /// <remarks>
        /// Username (required)&#xA;
        /// Password (required)
        /// </remarks>
        /// <param name="parameters">Credentials</param>
        /// <returns>Jwt, if credentials are vaild.</returns>
        [HttpPost("Get")]
        public async Task<ActionResult<string>> GetAsync([FromBody] AuthenticationTokenGetParameters parameters)
        {
            try
            {
                UiUser? user = null;

                if (parameters != null)
                {
                    string? username = parameters.Username;
                    string? password = parameters.Password;

                    // Create User from given parameters / If user does not provide login data => anonymous login
                    if (username != null && password != null)
                        user = new UiUser { Name = username, Password = password };
                }

                AuthManager authManager = new AuthManager(jwtWriter, ldaps, apiConnection);

                // Authenticate user
                string jwt = await authManager.AuthorizeUserAsync(user, validatePassword: true);

                return Ok(jwt);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Generates an authentication token (jwt) for the specified user given valid admin credentials.  
        /// </summary>
        /// <remarks>
        /// AdminUsername (required) - Example: "admin" &#xA;
        /// AdminPassword (required) - Example: "password" &#xA;
        /// Lifetime (optional) - Example: "365.12:02:00" ("days.hours:minutes:seconds") &#xA;
        /// TargetUserDn OR TargetUserName (required) - Example: "uid=demo_user,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal" OR "demo_user" 
        /// </remarks>
        /// <param name="parameters">Admin Credentials, Lifetime, User</param>
        /// <returns>User jwt, if credentials are vaild.</returns>
        [HttpPost("GetForUser")]
        public async Task<ActionResult<string>> GetAsyncForUser([FromBody] AuthenticationTokenGetForUserParameters parameters)
        {
            try
            {
                string adminUsername = parameters.AdminUsername;
                string adminPassword = parameters.AdminPassword;
                TimeSpan lifetime = parameters.Lifetime;
                string targetUserName = parameters.TargetUserName;
                string targetUserDn = parameters.TargetUserDn;

                AuthManager authManager = new AuthManager(jwtWriter, ldaps, apiConnection);
                UiUser adminUser = new UiUser() { Name = adminUsername, Password = adminPassword };
                // Check if admin valids are valid
                try
                {
                    await authManager.AuthorizeUserAsync(adminUser, validatePassword: true);
                    if (!adminUser.Roles.Contains("admin"))
                    {
                        throw new AuthenticationException("Provided credentials do not belong to a user with role admin.");
                    }
                }
                catch (Exception e)
                {
                    throw new AuthenticationException("Error while validating admin credentials: " + e.Message);
                }
                // Check if username is valid and generate jwt
                try
                {
                    UiUser targetUser = new UiUser { Name = targetUserName, Dn = targetUserDn };
                    string jwt = await authManager.AuthorizeUserAsync(targetUser, validatePassword: false, lifetime);
                    return Ok(jwt);
                }
                catch (Exception e)
                {
                    throw new AuthenticationException("Error while validating user credentials (user name): " + e.Message);
                }
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }

    class AuthManager
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;

        public AuthManager(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Validates user credentials and retrieves user information. Returns a jwt containing it.
        /// </summary>
        /// <param name="user">User to validate. Must contain username / dn and password if <paramref name="validatePassword"/> == true.</param>
        /// <param name="validatePassword">Check password if true.</param>
        /// <param name="lifetime">Set the lifetime of the jwt (optional)</param>
        /// <returns>Jwt, User infos (dn, email, groups, roles, tenant), if credentials are valid.</returns>
        public async Task<string> AuthorizeUserAsync(UiUser? user, bool validatePassword, TimeSpan? lifetime = null)
        {
            // Case: anonymous user
            if (user == null)
                return await jwtWriter.CreateJWT();

            // Retrieve ldap entry for user (throws exception if credentials are invalid)
            (LdapEntry ldapUser, Ldap ldap) = await GetLdapEntry(user, validatePassword);

            // Get dn of user
            user.Dn = ldapUser.Dn;

            // Get email of user
            user.Email = ldap.GetEmail(ldapUser);

            // Get groups of user
            user.Groups = ldap.GetGroups(ldapUser);

            // Get roles of user
            user.Roles = await GetRoles(user);

            // Get tenant of user
            user.Tenant = await GetTenantAsync(ldapUser, ldap);

            // Remember the hosting ldap
            user.LdapConnection.Id = ldap.Id;

            // Create JWT for validated user with roles and tenant
            return await jwtWriter.CreateJWT(user, lifetime);
        }

        public async Task<(LdapEntry, Ldap)> GetLdapEntry(UiUser user, bool validatePassword)
        {
            Log.WriteDebug("User Authentication", $"Trying to ldap entry for user: {user.Name + " " + user.Dn}...");

            if (user.Dn == "" && user.Name == "")
            {
                throw new Exception("A0001 Invalid credentials. Username / User DN must not be empty.");
            }

            else
            {
                LdapEntry? ldapEntry = null;
                Ldap? ldap = null;
                List<Task> ldapValidationRequests = new List<Task>();
                object dnLock = new object();
                bool ldapFound = false;

                foreach (Ldap currentLdap in ldaps.Where(x => x.Active))
                {
                    ldapValidationRequests.Add(Task.Run(() =>
                    {
                        Log.WriteDebug("User Authentication", $"Trying to authenticate {user.Name + " " + user.Dn} against LDAP {currentLdap.Address}:{currentLdap.Port} ...");

                        try
                        {
                            LdapEntry? currentLdapEntry = currentLdap.GetLdapEntry(user, validatePassword);

                            if (currentLdapEntry != null)
                            {
                                // User was successfully authenticated via this LDAP
                                Log.WriteInfo("User Authentication", $"User {user.Name + " " + currentLdapEntry.Dn} found.");

                                lock (dnLock)
                                {
                                    if (!ldapFound)
                                    {
                                        ldapEntry = currentLdapEntry;
                                        ldap = currentLdap;
                                        ldapFound = true;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // this Ldap can't validate user, but maybe another one can
                        }
                    }));
                }

                while (ldapValidationRequests.Count > 0)
                {
                    Task finishedDnRequest = await Task.WhenAny(ldapValidationRequests);

                    if (ldapEntry != null && ldap != null)
                    {
                        return (ldapEntry, ldap);
                    }

                    ldapValidationRequests.Remove(finishedDnRequest);
                }
            }

            // Invalid User Credentials
            throw new Exception("A0002 Invalid credentials");
        }

        public async Task<List<string>> GetRoles(UiUser user)
        {
            List<string> dnList = new() { user.Dn };
            // search all groups where user is member for group associated roles
            dnList.AddRange(user.Groups);

            List<string> userRoles = new List<string>();
            object rolesLock = new object();

            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in ldaps)
            {
                // if current Ldap has roles stored
                if (currentLdap.HasRoleHandling())
                {
                    ldapRoleRequests.Add(Task.Run(() =>
                    {
                        // Get roles from current Ldap
                        List<string> currentRoles = currentLdap.GetRoles(dnList);

                        lock (rolesLock)
                        {
                            userRoles.AddRange(currentRoles);
                        }
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            // If no roles found
            if (userRoles.Count == 0)
            {
                // Use anonymous role
                Log.WriteWarning("Missing roles", $"No roles for user \"{user.Dn}\" could be found. Using anonymous role.");
                userRoles.Add("anonymous");
            }

            return userRoles;
        }

        public async Task<Tenant?> GetTenantAsync(LdapEntry user, Ldap ldap)
        {
            Tenant tenant = new Tenant();
            if (ldap.TenantId != null)
            {
                Log.WriteDebug("Get Tenant", $"This LDAP has the fixed tenant {ldap.TenantId.Value}");
                tenant.Id = ldap.TenantId.Value;
            }
            else
            {
                tenant.Name = new DistName(user.Dn).getTenant(ldap.TenantLevel);
                if (tenant.Name == "")
                {
                    return null;
                }
                Log.WriteDebug("Get Tenant", $"extracting TenantName as: {tenant.Name} from {user.Dn}");
                if (tenant.Name == ldap.GlobalTenantName)
                {
                    tenant.Id = 1;
                }
                else
                {
                    var tenNameObj = new { tenant_name = tenant.Name };
                    Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, tenNameObj, "getTenantId");
                    if (tenants.Length > 0)
                    {
                        tenant.Id = tenants[0].Id;
                    }
                    else
                    {
                        // tenant unknown: create in db. This should only happen for users from external Ldaps
                        try
                        {
                            var Variables = new
                            {
                                name = tenant.Name,
                                project = "",
                                comment = "",
                                viewAllDevices = false,
                                create = DateTime.Now
                            };
                            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(AuthQueries.addTenant, Variables)).ReturnIds;
                            if (returnIds != null)
                            {
                                tenant.Id = returnIds[0].NewId;
                                // no further search for devices etc necessary
                                return tenant;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        catch (Exception exception)
                        {
                            Log.WriteError("AddTenant", $"Adding Tenant {tenant.Name} locally failed: {exception.Message}");
                            return null;
                        }
                    }
                }
            }

            var tenIdObj = new { tenantId = tenant.Id };

            Device[] deviceIds = await apiConnection.SendQueryAsync<Device[]>(AuthQueries.getVisibleDeviceIdsPerTenant, tenIdObj, "getVisibleDeviceIdsPerTenant");
            tenant.VisibleDevices = Array.ConvertAll(deviceIds, device => device.Id);

            Management[] managementIds = await apiConnection.SendQueryAsync<Management[]>(AuthQueries.getVisibleManagementIdsPerTenant, tenIdObj, "getVisibleManagementIdsPerTenant");
            tenant.VisibleManagements = Array.ConvertAll(managementIds, management => management.Id);

            return tenant;
        }
    }
}

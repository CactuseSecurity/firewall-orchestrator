using FWO.Api.Data;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationTokenController : ControllerBase
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly APIConnection apiConnection;

        public AuthenticationTokenController(JwtWriter jwtWriter, List<Ldap> ldaps, APIConnection apiConnection)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        public class AuthenticationTokenGetParameters
        {
            public string? Username { get; set; }
            public string? Password {  get; set; }
        }

        // GET: api/<JwtController>
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

                    // Create User from given parameters / If user no login data provided => anonymous login
                    if (username != null && password != null)
                        user = new UiUser { Name = username, Password = password };
                }

                AuthManager authManager = new AuthManager(jwtWriter, ldaps, apiConnection);

                // Authenticate user
                string jwt = await authManager.AuthorizeUserAsync(user);

                return Ok(jwt);
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
        private readonly APIConnection apiConnection;
        private Ldap loggedInLdap = new Ldap();

        public AuthManager(JwtWriter jwtWriter, List<Ldap> ldaps, APIConnection apiConnection)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns>jwt if credentials are valid</returns>
        public async Task<string> AuthorizeUserAsync(UiUser? user)
        {
            // Case: anonymous user
            if (user == null)
                return await jwtWriter.CreateJWT();

            // Validate user credentials and get ldap distinguish name
            user.Dn = await GetLdapDistinguishedName(user);

            // Get roles of user
            user.Roles = await GetRoles(user);

            // Get tenant of user
            user.Tenant = await GetTenantAsync(user);

            user.LdapConnection.Id = loggedInLdap.Id;

            // Create JWT for validated user with roles and tenant
            return (await jwtWriter.CreateJWT(user));
        }

        private async Task<string> GetLdapDistinguishedName(UiUser user)
        {
            Log.WriteDebug("User validation", $"Trying to validate {user.Name}...");

            if (user.Name == "")
            {
                throw new Exception("A0001 Invalid credentials. Username must not be empty");
            }

            else
            {
                string userDn = "";
                List<Task> ldapDnRequests = new List<Task>();
                object dnLock = new object();

                foreach (Ldap currentLdap in ldaps.Where(x => x.Active))
                {
                    ldapDnRequests.Add(Task.Run(() =>
                    {
                        Log.WriteDebug("User Authentication", $"Trying to authenticate \"{user.Dn}\" against LDAP {currentLdap.Address}:{currentLdap.Port} ...");

                        try
                        {
                            string currentDn = currentLdap.ValidateUser(user);

                            if (currentDn != "")
                            {
                                // User was successfully authenticated via this LDAP
                                Log.WriteInfo("User Authentication", $"User successfully validated as {user} with DN {currentDn}");

                                lock (dnLock)
                                {
                                    loggedInLdap = currentLdap;
                                    userDn = currentDn;
                                }
                            }
                        }
                        catch
                        {
                            // this Ldap can't validate user, but maybe another one can
                        }
                    }));
                }

                while (ldapDnRequests.Count > 0)
                {
                    Task finishedDnRequest = await Task.WhenAny(ldapDnRequests);

                    if (!string.IsNullOrWhiteSpace(userDn))
                        return userDn;

                    ldapDnRequests.Remove(finishedDnRequest);
                }
            }

            // Invalid User Credentials
            throw new Exception("A0002 Invalid credentials");
        }

        public async Task<List<string>> GetRoles(UiUser user)
        {
            List<string> dnList = new List<string>();
            dnList.Add(user.Dn);
            if (user.Groups != null && user.Groups.Count > 0)
            {
                dnList.AddRange(user.Groups);
            }

            List<string> UserRoles = new List<string>();
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
                            UserRoles.AddRange(currentRoles);
                        }
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            // If no roles found
            if (UserRoles.Count == 0)
            {
                // Use anonymous role
                Log.WriteWarning("Missing roles", $"No roles for user \"{user.Dn}\" could be found. Using anonymous role.");
                UserRoles.Add("anonymous");
            }

            return UserRoles;
        }

        public async Task<Tenant?> GetTenantAsync(UiUser user)
        {
            Tenant tenant = new Tenant();
            if (loggedInLdap.TenantId != null)
            {
                Log.WriteDebug("Get Tenant", $"This LDAP has the fixed tenant {loggedInLdap.TenantId.Value}");
                tenant.Id = loggedInLdap.TenantId.Value;
            }
            else
            {
                tenant.Name = new DistName(user.Dn).getTenant(loggedInLdap.TenantLevel);
                if (tenant.Name == "")
                {
                    return null;
                }
                Log.WriteDebug("Get Tenant", $"extracting TenantName as: {tenant.Name} from {user.Dn}");
                if(loggedInLdap.GlobalTenantName != null && tenant.Name == loggedInLdap.GlobalTenantName)
                {
                    tenant.Id = 1;
                }
                else
                {
                    var tenNameObj = new { tenant_name = tenant.Name };
                    Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, tenNameObj, "getTenantId");
                    if (tenants.Count() > 0)
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
                            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.ApiClient.Queries.AuthQueries.addTenant, Variables)).ReturnIds;
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
                            Log.WriteAudit("AddTenant", $"Adding Tenant {tenant.Name} locally failed: {exception.Message}");
                            return null;
                        }
                    }
                }
            }

            // TODO: Both api calls in this method can be shortened to to a single query / api call
            var tenIdObj = new { tenantId = tenant.Id };

            Device[] deviceIds = await apiConnection.SendQueryAsync<Device[]>(AuthQueries.getVisibleDeviceIdsPerTenant, tenIdObj, "getVisibleDeviceIdsPerTenant");
            tenant.VisibleDevices = Array.ConvertAll(deviceIds, device => device.Id);

            Management[] managementIds = await apiConnection.SendQueryAsync<Management[]>(AuthQueries.getVisibleManagementIdsPerTenant, tenIdObj, "getVisibleManagementIdsPerTenant");
            tenant.VisibleManagements = Array.ConvertAll(managementIds, management => management.Id);

            return tenant;
        }
    }
}

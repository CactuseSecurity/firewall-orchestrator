using FWO.Middleware.Server.Data;
using FWO.Logging;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class AuthenticationRequestHandler : RequestHandler
    {
        private readonly JwtWriter tokenGenerator;
        private APIConnection ApiConn;
        private int tenantLevel = 1;
        private int? fixedTenantId;

        private object rolesLock = new object();
        private object dnLock = new object();

        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public AuthenticationRequestHandler(List<Ldap> Ldaps, JwtWriter tokenGenerator, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.tokenGenerator = tokenGenerator;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Username", "Password" from Type string
            string username = GetRequestParameter<string>("Username", notNull: true);
            string password = GetRequestParameter<string>("Password", notNull: true);

            // Create User from given parameters
            User user = new User() { Name = username, Password = password };

            // Authenticate user
            string jwt = await AuthorizeUserAsync(user);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("jwt", jwt));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <param name="tokenGenerator"></param>
        /// <returns>jwt if credentials are valid</returns>
        private async Task<string> AuthorizeUserAsync(User user)
        {
            // Validate user credentials and get ldap distinguish name
            user.Dn = await GetLdapDistinguishName(user);

            // User has valid credentials / is anonymous user. Otherwise exception would have been thrown and handled in base class

            // Get roles of user
            user.Roles = await GetRoles(user);

            // Get tenant of user
            user.Tenant = await GetTenantAsync(user);

            // Create JWT for validated user with roles and tenant
            return tokenGenerator.CreateJWT(user);
        }

        public async Task<string> GetLdapDistinguishName(User user)
        {
            Log.WriteDebug("User validation", $"Trying to validate {user.Name}...");

            // Anonymous case
            if (user.Name == "")
            {
                Log.WriteWarning("Anonymous/empty user", "No username was provided. Using anonymous username.");
                return "anonymous";
            }

            else
            {
                string userDn = null;
                List<Task> ldapDnRequests = new List<Task>();

                foreach (Ldap currentLdap in Ldaps)
                {
                    ldapDnRequests.Add(Task.Run(() =>
                    {
                        Log.WriteDebug("User Authentication", $"Trying to authenticate {user} against LDAP {currentLdap.Address}:{currentLdap.Port} ...");

                        string currentDn = currentLdap.ValidateUser(user);

                        if (currentDn != "")
                        {
                            // User was successfully authenticated via LDAP
                            Log.WriteInfo("User Authentication", $"User successfully validated as {user} with DN {currentDn}");

                            lock(dnLock)
                            {
                                tenantLevel = currentLdap.TenantLevel;
                                userDn = currentDn;
                                fixedTenantId = currentLdap.TenantId;
                            }
                        }
                    }));
                }

                while (ldapDnRequests.Count > 0)
                {
                    Task finishedDnRequest = await Task.WhenAny(ldapDnRequests);

                    if (userDn != null)
                        return userDn;

                    ldapDnRequests.Remove(finishedDnRequest);
                }
            }

            // Invalid User Credentials
            throw new Exception("Invalid credentials.");
        }

        public async Task<string[]> GetRoles(User user)
        {
            string UserDn = user.Dn;

            List<string> UserRoles = new List<string>();

            // Anonymous case
            if (UserDn == "anonymous")
            {
                Log.WriteWarning("Anonymous/empty user", $"Using anonymous role.");
                UserRoles.Add("anonymous");
            }
            else
            {
                List<Task> ldapRoleRequests = new List<Task>();

                foreach (Ldap currentLdap in Ldaps)
                {
                    ldapRoleRequests.Add(Task.Run(() =>
                    {
                        // if current Ldap has roles stored
                        if (currentLdap.RoleSearchPath != "")
                        {
                            // Get roles from current Ldap
                            string[] currentRoles = currentLdap.GetRoles(UserDn);

                            lock(rolesLock)
                            {
                                UserRoles.AddRange(currentRoles);
                            }
                        }
                    }));
                }

                await Task.WhenAll(ldapRoleRequests);
            }

            // If no roles found
            if (UserRoles.Count == 0)
            {
                // Use anonymous role
                Log.WriteWarning("Missing roles", $"No roles for user \"{UserDn}\" could be found. Using anonymous role.");
                UserRoles.Add("anonymous");
            }

            return UserRoles.ToArray();
        }

        public async Task<Tenant> GetTenantAsync(User user)
        {
            Tenant tenant = new Tenant();
            if(fixedTenantId != null)
            {
                Log.WriteDebug("Get Tenant", $"This LDAP has the fixed tenant {fixedTenantId.Value}");
                tenant.Id = fixedTenantId.Value;
                // todo: do we also need the tenant name here?
            }
            else
            {
                tenant.Name = (new FWO.Api.Data.DistName(user.Dn)).getTenant(tenantLevel);
                if(tenant.Name == "")
                {
                    return null;
                }
                Log.WriteDebug("Get Tenant", $"extracting TenantName as: {tenant.Name} from {user.Dn}");

                var tenNameObj = new { tenant_name = tenant.Name };
                tenant = (await ApiConn.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, tenNameObj, "getTenantId"))[0];
            }

            var tenIdObj = new { tenantId = tenant.Id };

            DeviceId[] deviceIds = await ApiConn.SendQueryAsync<DeviceId[]>(AuthQueries.getVisibleDeviceIdsPerTenant, tenIdObj, "getVisibleDeviceIdsPerTenant");
            tenant.VisibleDevices = new int[deviceIds.Length];
            for(int i = 0; i < deviceIds.Length; ++i)
            {
                tenant.VisibleDevices[i] = deviceIds[i].Id;
            }
            
            ManagementId[] managementIds = await ApiConn.SendQueryAsync<ManagementId[]>(AuthQueries.getVisibleManagementIdsPerTenant, tenIdObj, "getVisibleManagementIdsPerTenant");
            tenant.VisibleManagements = new int[managementIds.Length];
            for(int i = 0; i < managementIds.Length; ++i)
            {
                tenant.VisibleManagements[i] = managementIds[i].Id;
            }
            return tenant;
        }
    }
}

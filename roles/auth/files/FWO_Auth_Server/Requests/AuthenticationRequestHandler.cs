using FWO.Auth.Server.Data;
using FWO.Logging;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Auth.Server.Requests
{
    class AuthenticationRequestHandler : RequestHandler
    {
        private readonly JwtWriter tokenGenerator;
        private APIConnection ApiConn;
        private int tenantLevel = 1;

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
            user.Dn = GetLdapDistinguishName(user);

            // User has valid credentials / is anonymous user. Otherwise exception would have been thrown and handled in base class

            // Get roles of user
            user.Roles = GetRoles(user);

            // Get tenant of user
            user.Tenant = await GetTenantAsync(user);

            // Create JWT for validated user with roles and tenant
            return tokenGenerator.CreateJWT(user);
        }

        public string GetLdapDistinguishName(User user)
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
                foreach (Ldap currentLdap in Ldaps)
                {
                    Log.WriteDebug("User Authentication", $"Trying to authenticate {user} against LDAP {currentLdap.Address}:{currentLdap.Port} ...");

                    string UserDn = currentLdap.ValidateUser(user);

                    if (UserDn != "")
                    {
                        // User was successfully authenticated via LDAP
                        Log.WriteInfo("User Authentication", $"User successfully validated as {user} with DN {UserDn}");
                        tenantLevel = currentLdap.TenantLevel;
                        return UserDn;
                    }
                }
            }

            // Invalid User Credentials
            throw new Exception("Invalid credentials.");
        }

        public string[] GetRoles(User user)
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
                foreach (Ldap currentLdap in Ldaps)
                {
                    // if current Ldap has roles stored
                    if (currentLdap.RoleSearchPath != "")
                    {
                        // Get roles from current Ldap
                        UserRoles.AddRange(currentLdap.GetRoles(UserDn));
                    }
                }
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
            tenant.Name = ExtractTenantName(user.Dn, tenantLevel);

            var tenNameObj = new { tenant_name = tenant.Name };

            tenant = (await ApiConn.SendQuery<Tenant>(BasicQueries.getTenantId, tenNameObj, "getTenantId"))[0];

            var tenIdObj = new { tenantId = tenant.Id };

            DeviceId[] deviceIds = await ApiConn.SendQuery<DeviceId>(BasicQueries.getVisibleDeviceIdsPerTenant, tenIdObj, "getVisibleDeviceIdsPerTenant");
            tenant.VisibleDevices = new int[deviceIds.Length];
            for(int i = 0; i < deviceIds.Length; ++i)
            {
                tenant.VisibleDevices[i] = deviceIds[i].Id;
            }
            
            ManagementId[] managementIds = await ApiConn.SendQuery<ManagementId>(BasicQueries.getVisibleManagementIdsPerTenant, tenIdObj, "getVisibleManagementIdsPerTenant");
            tenant.VisibleManagements = new int[managementIds.Length];
            for(int i = 0; i < managementIds.Length; ++i)
            {
                tenant.VisibleManagements[i] = managementIds[i].Id;
            }

            return tenant;
        }

        private string ExtractTenantName(string userDN, int ldapTenantLevel)
        {
            string localString = userDN;
            string beginSeparator = "ou=";
            string endSeparator = ",";
            int beginSeparatorIndex = 0;
            int endSeparatorIndex = 0;
            string tenantName = "";

            if (userDN=="anonymous") 
            {
                // user anonymous gets assigned the only reliably existing tenant0 - might lead to anonymous able to see too much!
                tenantName = "tenant0";
            }
            else {
                for(int i = 0; i < ldapTenantLevel; ++i)
                {
                    localString = localString.Substring(endSeparatorIndex);
                    beginSeparatorIndex = localString.IndexOf(beginSeparator);
                    endSeparatorIndex = localString.Substring(beginSeparatorIndex).IndexOf(endSeparator);
                }
                if((beginSeparatorIndex >= 0) && (endSeparatorIndex >= 0))
                {
                    tenantName = localString.Substring(beginSeparatorIndex + beginSeparator.Length, endSeparatorIndex - 3);
                }
                Log.WriteDebug("Get Tenant", $"extracting TenantName as: {tenantName} from {userDN}");
            }
            return tenantName;
        }
    }
}

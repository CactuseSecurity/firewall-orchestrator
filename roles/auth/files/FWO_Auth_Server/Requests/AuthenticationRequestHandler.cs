using FWO.Auth.Server.Data;
using FWO.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FWO.Auth.Server.Requests
{
    class AuthenticationRequestHandler : RequestHandler
    {
        private readonly JwtWriter tokenGenerator;

        public AuthenticationRequestHandler(ref List<Ldap> Ldaps, JwtWriter tokenGenerator)
        {
            this.Ldaps = Ldaps;
            this.tokenGenerator = tokenGenerator;
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
                        return UserDn;
                    }
                }
            }

            // Invalid User Credentials
            throw new Exception("Invalid credentials.");
        }

        public Tenant GetTenant(User user)
        {
            /*
            necessary queries:
               query getTenantId($tenant_name: String) { tenant(where: {tenant_name: {_eq: $tenant_name}}) { tenant_id: Int } }
               query getVisibleDeviceIdsPerTenant($tenantId: Int!) { visibleDevices: get_visible_devices_per_tenant(args: {arg_1: $tenantId}) { id } }
               query getVisibleManagementIdsPerTenant($tenantId: Int!) { visibleManagements: get_visible_managements_per_tenant(args: {arg_1: $tenantId}) { id } }
            
            Tenant tenant = new Tenant();
            tenant.Name = UserDn; //only part of it (first ou)

            // need to resolve tenant_name from DN to tenant_id first 
            // get visible devices

            string variables = $"\"tenant_id\":{tenant.Id}";
            tenant.VisibleDevices = APICall(query,variables);

            variables: {"tenant_id":3}
            tenant.VisibleManagements = APICall();

            tenantInformation.VisibleDevices = {};
            tenantInformation.VisibleManagements = [];

            user.tenant = tenant;
            */
            return null;            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <param name="tokenGenerator"></param>
        /// <returns>jwt if credentials are valid</returns>
        private string AuthorizeUser(User user)
        {
            // Validate user credentials and get ldap distinguish name
            user.Dn = GetLdapDistinguishName(user);

            // User has valid credentials / is anonymous user. Otherwise exception would have been thrown and handeled in base class

            // Get roles of user
            user.Roles = GetRoles(user);

            // Get tenant of user
            user.Tenant = GetTenant(user);

            // Create JWT for validated user with roles and tenant
            return tokenGenerator.CreateJWT(user);
        }

        protected override (HttpStatusCode status, string wrappedResult) HandleRequestInternal(HttpListenerRequest request)
        {
            User user;

            // Get parameters from request. Expected parameters: "Username", "Password" from Type string
            string username = GetRequestParameter<string>("Username", notNull: true);
            string password = GetRequestParameter<string>("Password", notNull: true);

            // Create User from given parameters
            user = new User() { Name = username, Password = password };

            // Authenticate user
            string jwt = AuthorizeUser(user);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("jwt", jwt)); 
        }
    }
}

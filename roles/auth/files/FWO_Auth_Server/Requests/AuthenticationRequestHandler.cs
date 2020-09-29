using FWO_Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FWO_Auth_Server.Requests
{
    class AuthenticationRequestHandler : RequestHandler
    {
        private readonly JwtWriter tokenGenerator;

        public AuthenticationRequestHandler(ref List<Ldap> Ldaps, JwtWriter tokenGenerator)
        {
            this.Ldaps = Ldaps;
            this.tokenGenerator = tokenGenerator;
        }

        public Role[] SetUserRoles(User user)
        {
            string UserDn = user.Dn;

            List<Role> UserRoles = new List<Role>();

            // Anonymous case
            if (UserDn == "anonymous")
            {
                Log.WriteWarning("Anonymous/empty user", $"Using anonymous role.");
                UserRoles.Add(new Role("anonymous"));
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
                UserRoles.Add(new Role("anonymous"));
            }

            return UserRoles.ToArray();
        }

        public User ValidateUserCredentials(User user)
        {
            Log.WriteDebug("User validation", $"Trying to validate {user.Name}...");

            // Anonymous case
            if (user.Name == "")
            {
                Log.WriteWarning("Anonymous/empty user", "No username was provided. Using anonymous username.");
                user.Dn = "anonymous";
                return user;
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
                        user.Dn = UserDn;
                        return user;
                    }
                }
            }

            // Invalid User Credentials
            throw new Exception("Invalid credentials.");
        }

        public string SetUserTenant(User user)
        {
            /*
            Tenant tenant = new Tenant();
            tenant.Name = UserDn; //only part of it (first ou)

            // need to make APICalls available as common library

            // need to resolve tenant_name from DN to tenant_id first 
            // query get_tenant_id($tenant_name: String) { tenant(where: {tenant_name: {_eq: $tenant_name}}) { tenant_id } }
            // variables: {"tenant_name": "forti"}
            tenant.Id = 0; // todo: replace with APICall() result

            // get visible devices with the following queries:

            // query get_visible_mgm_per_tenant($tenant_id:Int!){  get_visible_managements_per_tenant(args: {arg_1: $tenant_id})  id } }
            string variables = $"\"tenant_id\":{tenant.Id}";
            // tenant.VisibleDevices = APICall(query,variables);

            // query get_visible_devices_per_tenant($tenant_id:Int!){ get_visible_devices_per_tenant(args: {arg_1: $tenant_id}) { id }}
            // variables: {"tenant_id":3}
            // tenant.VisibleDevices = APICall();

            // tenantInformation.VisibleDevices = {};
            // tenantInformation.VisibleManagements = [];

            UserData userData = new UserData();
            userData.tenant = tenant;
            responseString = TokenGenerator.CreateJWT(User, userData, roleLdap.GetRoles(UserDN));
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
            user = ValidateUserCredentials(user);

            // User has valid credentials / is anonymous user. Otherwise exception would have been thrown

            // Get roles of user
            SetUserRoles(user);

            // Get tenant of user
            SetUserTenant(user);

            // Create JWT for validated user with roles and tenant
            return tokenGenerator.CreateJWT(user);
        }

        protected override (HttpStatusCode status, string wrappedResult) HandleRequestInternal(HttpListenerRequest request)
        {
            User user;

            try
            {
                // Try to read username and password parameters
                user = new User() { Name = (string)Parameters["Username"], Password = (string)Parameters["Password"] };
            }
            catch (Exception)
            {
                throw new Exception("Parameter username/password was not found or bad formatted.");
            }

            // Authenticate user
            string jwt = AuthorizeUser(user);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("jwt", jwt)); 
        }
    }
}

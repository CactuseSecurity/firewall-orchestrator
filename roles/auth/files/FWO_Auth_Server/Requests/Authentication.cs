using FWO_Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Auth_Server.Requests
{
    static class Authentication
    {
        public static Role[] GetUserRoles(this IEnumerable<Ldap> Ldaps, string UserDn)
        {
            List<Role> UserRoles = new List<Role>();

            // Anonymous case
            if (UserDn == "anonymous") 
            {
                Log.WriteWarning("Anonymous/empty user", $"No roles for user \"{UserDn}\" could be found. Using anonymous role.");

                UserRoles.Add(new Role("anonymous"));
            }

            else
            {
                foreach (Ldap currentLdap in Ldaps)
                {
                    if (currentLdap.RoleSearchPath != "")
                    {
                        return currentLdap.GetRoles(UserDn);
                    }
                }
            }

            return UserRoles.ToArray();
        }

        public static string ValidateUserCredentials(this IEnumerable<Ldap> Ldaps, User user)
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
            return "";
        }

        public static string GetUserTenant(this IEnumerable<Ldap> Ldaps, string UserDn)
        {

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
            break;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns>
        /// <para>If user credentials are valid : Jwt.</para>
        /// <para>If error: Error message.</para>
        /// <para>If user credentials are invalid: "".</para>
        /// </returns>
        private static string AuthenticateUser(this IEnumerable<Ldap> Ldaps, User user, TokenGenerator tokenGenerator)
        {
            string Jwt = "InvalidCredentials";

            Log.WriteDebug("User validation", $"Trying to validate {user.Name}...");

            string userDn = Ldaps.ValidateUserCredentials(user);
            Role[] roles = Ldaps.GetUserRoles(userDn);
            Ldaps.GetUserTenant(userDn);

            Jwt = tokenGenerator.CreateJWT(user, null, roles);

            Log.WriteDebug("User validation", $"Succesfully validated.");

            return Jwt;
        }
    }
}

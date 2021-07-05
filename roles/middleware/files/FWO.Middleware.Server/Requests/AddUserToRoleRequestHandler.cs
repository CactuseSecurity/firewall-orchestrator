﻿using FWO.ApiClient;
using FWO.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class AddUserToRoleRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public AddUserToRoleRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Username", "Role" from Type string
            string userDn = GetRequestParameter<string>("Username", notNull: true);
            string role = GetRequestParameter<string>("Role", notNull: true);

            bool userAdded = false;
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in Ldaps)
            {
                ldapRoleRequests.Add(Task.Run(() =>
                {
                    // if current Ldap has roles stored: Try to add user to role in current Ldap
                    if (currentLdap.RoleSearchPath != null && currentLdap.RoleSearchPath != "" && currentLdap.AddUserToEntry(userDn, role))
                    {
                        userAdded = true;
                        Log.WriteAudit("AddUserToRole", $"user {userDn} successfully added to group {role}");                        
                    }
                }));
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userAdded", userAdded));
        }
    }
}

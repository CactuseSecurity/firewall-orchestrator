using FWO.ApiClient;
using FWO.ApiClient.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class AddLdapRequestHandler : RequestHandler
    {
        string apiUri;
        List<Ldap> connectedLdaps;

        public AddLdapRequestHandler(string apiUri, ref List<Ldap> connectedLdaps)
        {
            this.apiUri = apiUri;
            this.connectedLdaps = connectedLdaps;
        }

        protected async override Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request.
            string jwt = GetRequestParameter<string>("Password", notNull: true);
            var ldapData = new
            {
                address = GetRequestParameter<string>("Address", notNull: true),
                port = GetRequestParameter<string>("Port", notNull: true),
                searchUser = GetRequestParameter<string>("SearchUser", notNull: true),
                tls = GetRequestParameter<string>("Tls", notNull: true),
                tenantLevel = GetRequestParameter<string>("TenantLevel", notNull: true),
                searchUserPwd = GetRequestParameter<string>("SearchUserPassword", notNull: true),
                searchpathForUsers = GetRequestParameter<string>("SearchPathForUsers", notNull: true),
                searchpathForRoles = GetRequestParameter<string>("SearchPathForRoles", notNull: true),
                writeUser = GetRequestParameter<string>("WriteUser", notNull: true),
                writeUserPwd = GetRequestParameter<string>("WriteUserPassword", notNull: true),
                tenantId = GetRequestParameter<string>("TenantId", notNull: true)
            };

            // Create Api connection with given jwt
            APIConnection apiConnection = new APIConnection(apiUri, jwt);

            // Add ldap to DB and to middleware ldap list
            Ldap addedLdap = (await apiConnection.SendQueryAsync<Ldap[]>(AuthQueries.newLdapConnection, ldapData))[0];
            connectedLdaps.Add(addedLdap);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("Ldap", addedLdap));
        }
    }
}

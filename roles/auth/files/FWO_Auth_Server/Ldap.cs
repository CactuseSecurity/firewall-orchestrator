using FWO_Logging;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;


namespace FWO_Auth_Server
{
    public class Ldap
    {
        // The following properties are retrieved from the database api:
        // ldap_server ldap_port ldap_search_user ldap_tls ldap_tenant_level ldap_connection_id ldap_search_user_pwd ldap_searchpath_for_users ldap_searchpath_for_roles    

        [JsonPropertyName("ldap_server")]
        public string Address { get; set; }

        [JsonPropertyName("ldap_port")]
        public int Port { get; set; }

        [JsonPropertyName("ldap_search_user")]
        public string SearchUser { get; set; }

        [JsonPropertyName("ldap_tls")]
        public bool Tls { get; set; }

        [JsonPropertyName("ldap_tenant_level")]
        public int TenantLevel { get; set; }

        [JsonPropertyName("ldap_search_user_pwd")]
        public string SearchUserPwd { get; set; }

        [JsonPropertyName("ldap_searchpath_for_users")]
        public string UserSearchPath { get; set; }

        [JsonPropertyName("ldap_searchpath_for_roles")]
        public string RoleSearchPath { get; set; }

        private const int timeOutInMs = 200;

        /// <summary>
        /// Builds a connection to the specified Ldap server.
        /// </summary>
        /// <returns>Connection to the specified Ldap server.</returns>
        private LdapConnection Connect()
        {
            try
            {
                LdapConnection connection = new LdapConnection { SecureSocketLayer = Tls, ConnectionTimeout = timeOutInMs };

                if (Tls)
                {
                    connection.UserDefinedServerCertValidationDelegate += (object sen, X509Certificate cer, X509Chain cha, SslPolicyErrors err) => true;  // todo: allow cert validation
                }

                connection.Connect(Address, Port);

                return connection;
            }

            catch (Exception exception)
            {
                throw new Exception($"Error while trying to reach LDAP server {Address}:{Port}", exception);
            }
        }

        public string ValidateUser(User user)
        {
            Log.WriteInfo("User Validation", $"Validating User: \"{user.Name}\" ...");
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as search user
                    connection.Bind(SearchUser, SearchUserPwd);

                    // Search for users in ldap with same name as user to validate
                    LdapSearchResults possibleUsers = (LdapSearchResults)connection.Search(
                        UserSearchPath,             // top-level path under which to search for user
                        LdapConnection.ScopeSub,    // search all levels beneath
                        $"(|(&(sAMAccountName={user.Name})(objectClass=person))(&(objectClass=inetOrgPerson)(uid:dn:={user.Name})))", // matching both AD and openldap filter
                        null,
                        typesOnly: false
                    );

                    while (possibleUsers.HasMore())
                    {
                        LdapEntry currentUser = possibleUsers.Next();
                      
                        try
                        {
                            Log.WriteDebug("User Validation", $"Trying to validate user with distinguished name: \"{ currentUser.Dn}\" ...");

                            // Try to authenticate as user with given password
                            connection.Bind(currentUser.Dn, user.Password);

                            // If authentication was successful (user is bound)
                            if (connection.Bound)
                            {
                                // Return ldap dn
                                Console.WriteLine($"Successful authentication for \"{ currentUser.Dn}\"");
                                return currentUser.Dn;
                            }

                            else
                            {
                                // Incorrect password - do nothing, assume its another user with the same username
                                Log.WriteDebug("", $"Found user with matching uid but different pwd: \"{ currentUser.Dn}\".");
                            }
                        }
                        catch (LdapException)
                        {
                            // Incorrect password - do nothing, assume its another user with the same username
                            Log.WriteDebug("", $"Found user with matching uid but different pwd: \"{ currentUser.Dn}\".");
                        } 
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("Ldap exception", "Unexpected error while trying to validate user", exception);
            }

            Log.WriteDebug("Invalid Credentials", $"Invalid login credentials!");

            return "";
        }

        public Role[] GetRoles(string userDn)
        {
            List<Role> userRoles = new List<Role>();

            // If this Ldap is containing roles
            if (RoleSearchPath != null)
            {
                // Connect to Ldap
                using (LdapConnection connection = Connect())
                {     
                    // Authenticate as search user
                    connection.Bind(SearchUser, SearchUserPwd);

                    // Search for Ldap roles in given directory          
                    int searchScope = LdapConnection.ScopeSub; // TODO: Correct search scobe?
                    string searchFilter = $"(&(objectClass=groupOfUniqueNames)(cn=*))";
                    LdapSearchResults searchResults = (LdapSearchResults)connection.Search(RoleSearchPath, searchScope, searchFilter, null, false);                

                    // Foreach found role
                    foreach (LdapEntry entry in searchResults)
                    {
                        Log.WriteDebug("Ldap Roles", $"Try to get roles from ldap entry {entry.GetAttribute("cn").StringValue}");

                        // Get dn of users having current role
                        LdapAttribute roleMembers = entry.GetAttribute("uniqueMember");
                        string[] roleMemberDn = roleMembers.StringValueArray;

                        // Foreach user 
                        foreach (string currentDn in roleMemberDn)
                        {
                            Log.WriteDebug("Ldap Roles", $"Checking if current Dn: {currentDn} is user Dn. Then user has current role.");

                            // Check if current user dn is matching with given user dn => Given user has current role
                            if (currentDn == userDn)
                            {
                                // Get role name and add it to list of roles of given user
                                string RoleName = entry.GetAttribute("cn").StringValue;
                                userRoles.Add(new Role { Name = RoleName });
                            }
                        }
                    }
                }
            }

            Log.WriteDebug($"Found the following roles for user {userDn}:", string.Join("\n", userRoles));
            return userRoles.ToArray();
        }
    }
}

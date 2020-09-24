using Novell.Directory.Ldap;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;


namespace FWO_Auth_Server
{
    public class Ldap
    {
        // The following parameters are retrieved from the query and set in this class:
        // ldap_server ldap_port ldap_search_user ldap_tls ldap_tenant_level ldap_connection_id ldap_search_user_pwd ldap_searchpath_for_users ldap_searchpath_for_roles    

        [JsonPropertyName("ldap_server")]
        public string Address { get; set; }

        [JsonPropertyName("ldap_port")]
        public int Port { get; set; }

        [JsonPropertyName("ldap_search_user")]
        public string SearchUser { get; set; }

        [JsonPropertyName("ldap_tls")]
        public bool UseTls { get; set; }

        [JsonPropertyName("ldap_tenant_level")]
        public int TenantLevel { get; set; }

        [JsonPropertyName("ldap_search_user_pwd")]
        public string SearchUserPwd { get; set; }

        [JsonPropertyName("ldap_searchpath_for_users")]
        public string UserSearchPath { get; set; }

        [JsonPropertyName("ldap_searchpath_for_roles")]
        public string RoleSearchPath { get; set; }

        public LdapConnection Connect()
        {
            try
            {
                LdapConnection connection = new LdapConnection { SecureSocketLayer = UseTls, ConnectionTimeout = 200 };
                connection.UserDefinedServerCertValidationDelegate += (object sen, X509Certificate cer, X509Chain cha, SslPolicyErrors err) => true;  // todo: allow cert validation

                connection.Connect(Address, Port);

                return connection;
            }

            catch (Exception exConn)
            {
                // TODO: Ldap Server not reachable
                throw new Exception($"Error while trying to reach LDAP server {Address}:{Port} #### Message #### \n {exConn.Message} \n #### Stack Trace #### \n {exConn.StackTrace}");
            }
        }

        public string ValidateUser(User user)
        {      
            Console.WriteLine($"Validating User: \"{user.Name}\" ...");
            try         
            {
                using (LdapConnection connection = Connect())
                {
                    connection.Bind(SearchUser, SearchUserPwd);
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
#if DEBUG
                        Console.WriteLine($"Trying distinguished name: \"{ currentUser.Dn}\" ...");
#endif
                        try
                        {
                            connection.Bind(currentUser.Dn, user.Password);
                            if (connection.Bound)
                            {
                                Console.WriteLine($"Successful authentication for \"{ currentUser.Dn}\"");
                                return currentUser.Dn;
                            }
                        }
                        catch (LdapException exInner)
                        {
#if DEBUG
                            Console.WriteLine($"Found user with same uid but different pwd distinguished name: \"{ currentUser.Dn}\" ...");
                            Console.Write($"\n Error while trying LDAP Connection #### Message #### \n {exInner.Message} \n #### Stack Trace #### \n {exInner.StackTrace} \n");
#endif
                        } // Incorrect password - do nothing, assuming another user with the same username
                    }
                }
            }
            catch (LdapException ex)
            {
                Console.Write($"\n Error while trying LDAP Connection #### Message #### \n {ex.Message} \n #### Stack Trace #### \n {ex.StackTrace} \n");
                // Log exception
            }

            Console.WriteLine($"User \"{user.Name}\": invalid login credentials!");

            return "";
        }

        public Role[] GetRoles(string userDn)
        {
            List<Role> roleList = new List<Role>();
            using (LdapConnection connection = Connect())
            {
                if (!(RoleSearchPath is null))
                {
                    connection.Bind(SearchUser, SearchUserPwd);
                    int searchScope = LdapConnection.ScopeOne;
                    string searchFilter = $"(&(objectClass=groupOfUniqueNames)(cn=*))";
                    LdapSearchResults searchResults = (LdapSearchResults)connection.Search(RoleSearchPath, searchScope, searchFilter, null, false);

                    foreach (LdapEntry entry in searchResults)
                    {
                        LdapAttribute membersAttribute = entry.GetAttribute("uniqueMember");
                        string[] RoleMemberDn = membersAttribute.StringValueArray;
#if DEBUG
                        Console.WriteLine($"Ldap::GetRoles:dealing with ldap entry {entry.GetAttribute("cn").StringValue}");
#endif
                        foreach (string currentDn in RoleMemberDn)
                        {
#if DEBUG
                            FWO_Logging.Log.WriteInfo("Ldap Roles", $"Checking if current Dn: {currentDn} is user Dn.");
#endif
                            if (currentDn == userDn)
                            {
                                string RoleName = entry.GetAttribute("cn").StringValue;
                                roleList.Add(new Role { Name = RoleName });
                            }
                        }
                    }
                }
            }
            Role[] roles = roleList.ToArray();
#if DEBUG
            Console.WriteLine($"Ldap::GetRoles:Found the following roles for user {userDn}:");
            for (int i = 0; i < roles.Length; i++)
                Console.WriteLine($"RoleListElement: { roles[i].Name}");
#endif
            return roles;
        }
    }
}

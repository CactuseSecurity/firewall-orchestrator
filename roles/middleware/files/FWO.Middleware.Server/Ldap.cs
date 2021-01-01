using FWO.Middleware.Server.Data;
using FWO.Logging;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;


namespace FWO.Middleware.Server
{
    public class Ldap
    {
        // The following properties are retrieved from the database api:
        // ldap_server ldap_port ldap_search_user ldap_tls ldap_tenant_level ldap_connection_id ldap_search_user_pwd ldap_searchpath_for_users ldap_searchpath_for_roles    

        [JsonPropertyName("ldap_connection_id")]
        public int Id { get; set; }

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

        [JsonPropertyName("ldap_write_user")]
        public string WriteUser { get; set; }

        [JsonPropertyName("ldap_write_user_pwd")]
        public string WriteUserPwd { get; set; }

        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        private const int timeOutInMs = 200; // TODO: MOVE TO API

        /// <summary>
        /// Builds a connection to the specified Ldap server.
        /// </summary>
        /// <returns>Connection to the specified Ldap server.</returns>
        private LdapConnection Connect()
        {
            try
            {
                LdapConnection connection = new LdapConnection { SecureSocketLayer = Tls, ConnectionTimeout = timeOutInMs };
                if (Tls) connection.UserDefinedServerCertValidationDelegate += (object sen, X509Certificate cer, X509Chain cha, SslPolicyErrors err) => true;  // todo: allow cert validation                
                connection.Connect(Address, Port);

                return connection;
            }

            catch (Exception exception)
            {
                throw new Exception($"Error while trying to reach LDAP server {Address}:{Port}", exception);
            }
        }

        public bool IsInternal()
        {
            return (WriteUser != null && WriteUser != "");
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
                                Log.WriteDebug("User Validation", $"\"{ currentUser.Dn}\" successfully authenticated in {Address}.");
                                if (currentUser.GetAttributeSet().ContainsKey("mail"))
                                {
                                    user.Email = currentUser.GetAttribute("mail").StringValue;
                                }
                                return currentUser.Dn;
                            }

                            else
                            {
                                // this will probably never be reached as an error is thrown before
                                // Incorrect password - do nothing, assume its another user with the same username
                                Log.WriteDebug($"User Validation {Address}", $"Found user with matching uid but different pwd: \"{ currentUser.Dn}\".");
                            }
                        }
                        catch (LdapException exc)
                        {
                            if (exc.ResultCode == 49)  // 49 = InvalidCredentials
                                Log.WriteDebug($"Duplicate user {Address}", $"Found user with matching uid but different pwd: \"{ currentUser.Dn}\".");
                            else
                                Log.WriteError($"Ldap exception {Address}", "Unexpected error while trying to validate user \"{ currentUser.Dn}\".");
                        } 
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}", "Unexpected error while trying to validate user", exception);
            }

            Log.WriteInfo("Invalid Credentials", $"Invalid login credentials - could not authenticate user \"{ user.Name}\".");
            return "";
        }

        public string[] GetRoles(string userDn)
        {
            List<string> userRoles = new List<string>();

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
                            Log.WriteDebug("Ldap Roles", $"Checking if current Dn: \"{currentDn}\" is user Dn. Then user has current role.");

                            // Check if current user dn is matching with given user dn => Given user has current role
                            if (currentDn == userDn)
                            {
                                // Get role name and add it to list of roles of given user
                                string role = entry.GetAttribute("cn").StringValue;
                                userRoles.Add(role);
                                break;
                            }
                        }
                    }
                }
            }

            Log.WriteDebug($"Found the following roles for user {userDn} in {Address}:", string.Join("\n", userRoles));
            return userRoles.ToArray();
        }

        public List<KeyValuePair<string, List<KeyValuePair<string, string>>>> GetAllRoles()
        {
            List<KeyValuePair<string, List<KeyValuePair<string, string>>>> roleUsers = new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();

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
                        List<KeyValuePair<string, string>> attributes = new List<KeyValuePair<string, string>>();
                        string roleDesc = entry.GetAttribute("description").StringValue;
                        attributes.Add(new KeyValuePair<string, string>("description", roleDesc));

                        string[] roleMemberDn = entry.GetAttribute("uniqueMember").StringValueArray;
                        foreach (string currentDn in roleMemberDn)
                        {
                            if (currentDn != "")
                            {
                                attributes.Add(new KeyValuePair<string, string>("user", currentDn));
                            }
                        }
                        roleUsers.Add(new KeyValuePair<string, List<KeyValuePair<string, string>>>(entry.Dn, attributes));
                    }
                }
            }
            return roleUsers;
        }

        public List<KeyValuePair<string, string>> GetAllUsers()
        {
            List<KeyValuePair<string, string>> allUsers = new List<KeyValuePair<string, string>>();

            // Connect to Ldap
            using (LdapConnection connection = Connect())
            {     
                // Authenticate as search user
                connection.Bind(SearchUser, SearchUserPwd);

                // Search for Ldap users in given directory          
                int searchScope = LdapConnection.ScopeSub;
                string searchFilter = $"(&(objectClass=person)(uid=*))";
                LdapSearchResults searchResults = (LdapSearchResults)connection.Search(UserSearchPath, searchScope, searchFilter, null, false);                

                foreach (LdapEntry entry in searchResults)
                {
                    allUsers.Add(new KeyValuePair<string, string> (entry.Dn, (entry.GetAttributeSet().ContainsKey("mail") ? entry.GetAttribute("mail").StringValue : "")));
                }
            }
            return allUsers;
        }

        public bool AddUser(string userDn , string password, string email)
        {
            Log.WriteInfo("Add User", $"Trying to add User: \"{userDn}\"");
            bool userAdded = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    connection.Bind(WriteUser, WriteUserPwd);

                    string userName = (new FWO.Api.Data.DistName(userDn)).UserName;
                    LdapAttributeSet attributeSet = new LdapAttributeSet();
                    attributeSet.Add( new LdapAttribute("objectclass", "inetOrgPerson"));
                    attributeSet.Add( new LdapAttribute("sn", userName));
                    attributeSet.Add( new LdapAttribute("cn", userName));
                    attributeSet.Add( new LdapAttribute("uid", userName));
                    attributeSet.Add( new LdapAttribute("userPassword", password));
                    attributeSet.Add( new LdapAttribute("mail", email));

                    LdapEntry newEntry = new LdapEntry( userDn, attributeSet );

                    try
                    {
                        //Add the entry to the directory
                        connection.Add(newEntry);
                        userAdded = true;
                        Log.WriteDebug("Add user", $"User {userName} added in {Address}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Add User", $"couldn't add user to LDAP {Address}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}", "Unexpected error while trying to add user", exception);
            }
            return userAdded;
        }

        public bool UpdateUser(string userDn, string email)
        {
            Log.WriteInfo("Update User", $"Trying to update User: \"{userDn}\"");
            bool userUpdated = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    connection.Bind(WriteUser, WriteUserPwd);

                    LdapAttribute attribute = new LdapAttribute("mail", email);
                    LdapModification[] mods = { new LdapModification(LdapModification.Replace, attribute) };

                    try
                    {
                        //Add the entry to the directory
                        connection.Modify(userDn, mods);
                        userUpdated = true;
                        Log.WriteDebug("Update user", $"User {userDn} updated in {Address}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Update User", $"couldn't update user in LDAP {Address}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}", "Unexpected error while trying to update user", exception);
            }
            return userUpdated;
        }

        public bool DeleteUser(string userDn)
        {
            Log.WriteInfo("Delete User", $"Trying to delete User: \"{userDn}\"");
            bool userDeleted = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    connection.Bind(WriteUser, WriteUserPwd);

                    try
                    {
                        //Delete the entry in the directory
                        connection.Delete(userDn);
                        userDeleted = true;
                        Log.WriteDebug("Delete user", $"User {userDn} deleted in {Address}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Delete User", $"couldn't delete user in LDAP {Address}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}", "Unexpected error while trying to delete user", exception);
            }
            return userDeleted;
        }

        public bool AddUserToRole(string userDn, string role)
        {
            Log.WriteInfo("Add User to Role", $"Trying to add User: \"{userDn}\" to Role: \"{role}\"");
            return ModifyUserInRole(userDn, role, LdapModification.Add);
        }
        
        public bool RemoveUserFromRole(string userDn, string role)
        {
            Log.WriteInfo("Remove User from Role", $"Trying to remove User: \"{userDn}\" from Role: \"{role}\"");
            return ModifyUserInRole(userDn, role, LdapModification.Delete);
        }

        public bool ModifyUserInRole(string userDn, string role, int LdapModification)
        {
            bool userModified = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    connection.Bind(WriteUser, WriteUserPwd);

                    // Add a new value to the description attribute
                    LdapAttribute attribute = new LdapAttribute("uniquemember", userDn);
                    LdapModification[] mods = { new LdapModification(LdapModification, attribute) }; 

                    try
                    {
                        //Modify the entry in the directory
                        connection.Modify (role, mods);
                        userModified = true;
                        Log.WriteDebug("Modify Role", $"Role {role} modified in {Address}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Modify Role", $"maybe role doesn't exist in this LDAP {Address}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}", "Unexpected error while trying to modify user", exception);
            }
            return userModified;
        }
    }
}

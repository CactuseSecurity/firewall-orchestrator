using FWO.Logging;
using Novell.Directory.Ldap;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using FWO.GlobalConstants;
using FWO.Encryption;
using FWO.Api.Data;
using FWO.Middleware.RequestParameters;
using Microsoft.IdentityModel.Tokens;

namespace FWO.Middleware.Server
{
    /// <summary>
	/// Class handling the ldap transactions
	/// </summary>
    public class Ldap : LdapConnectionBase
    {
        // The following properties are retrieved from the database api:
        // ldap_server ldap_port ldap_search_user ldap_tls ldap_tenant_level ldap_connection_id ldap_search_user_pwd ldap_searchpath_for_users ldap_searchpath_for_roles    
        private const int timeOutInMs = 3000;

		/// <summary>
		/// Default constructor
		/// </summary>
        public Ldap()
        {}

		/// <summary>
		/// Constructor from parameter struct
		/// </summary>
        public Ldap(LdapGetUpdateParameters ldapGetUpdateParameters) : base(ldapGetUpdateParameters)
        {}

        /// <summary>
        /// Builds a connection to the specified Ldap server.
        /// </summary>
        /// <returns>Connection to the specified Ldap server.</returns>
        private LdapConnection Connect()
        {
            try
            {
                LdapConnectionOptions ldapOptions = new LdapConnectionOptions();
                if (Tls) ldapOptions.ConfigureRemoteCertificateValidationCallback((object sen, X509Certificate? cer, X509Chain? cha, SslPolicyErrors err) => true); // todo: allow real cert validation     
                LdapConnection connection = new LdapConnection(ldapOptions) { SecureSocketLayer = Tls, ConnectionTimeout = timeOutInMs };           
                connection.Connect(Address, Port);

                return connection;
            }

            catch (Exception exception)
            {
                Log.WriteDebug($"Could not connect to LDAP server {Address}:{Port}: ", exception.Message);
                throw new Exception($"Error while trying to reach LDAP server {Address}:{Port}", exception);
            }
        }

        /// <summary>
        /// try an ldap bind, decrypting pwd before bind; using pwd as is if it cannot be decrypted
        /// false if bind fails
        /// </summary>
        private bool TryBind(LdapConnection connection, string user, string password)
        {
            string decryptedPassword = password; 
            try 
            {
                decryptedPassword = AesEnc.Decrypt(password, AesEnc.GetMainKey());
            }
            catch
            {
                Log.WriteDebug("TryBind", $"Could not decrypt password");
                // assuming we already have an unencrypted password, trying this
            }
            connection.Bind(user, decryptedPassword);
            return connection.Bound;
        }

        /// <summary>
        /// Test a connection to the specified Ldap server.
        /// Throws exception if not successful
        /// </summary>
        public void TestConnection()
        {
            using (LdapConnection connection = Connect())
            {
                if (!string.IsNullOrEmpty(SearchUser))
                {
                    if (!TryBind(connection, SearchUser, SearchUserPwd)) throw new Exception("Binding failed for search user");
                }
                if (!string.IsNullOrEmpty(WriteUser))
                {
                    if (!TryBind(connection, WriteUser, WriteUserPwd)) throw new Exception("Binding failed for write user");
                }
            }
        }

        private string getUserSearchFilter(string searchPattern)
        {
            string userFilter;
            string searchFilter;
            if(Type == (int)LdapType.ActiveDirectory)
            {
                userFilter = "(&(objectclass=user)(!(objectclass=computer)))";
                searchFilter = $"(|(cn={searchPattern})(sAMAccountName={searchPattern}))";
            }
            else if(Type == (int)LdapType.OpenLdap)
            {
                userFilter = "(|(objectclass=user)(objectclass=person)(objectclass=inetOrgPerson)(objectclass=organizationalPerson))";
                searchFilter = $"(|(cn={searchPattern})(uid={searchPattern}))";
            }
            else // LdapType.Default
            {
                userFilter = "(&(|(objectclass=user)(objectclass=person)(objectclass=inetOrgPerson)(objectclass=organizationalPerson))(!(objectclass=computer)))";
                searchFilter = $"(|(cn={searchPattern})(uid={searchPattern})(userPrincipalName={searchPattern})(mail={searchPattern}))";
            }
            return ((searchPattern == null || searchPattern == "") ? userFilter : $"(&{userFilter}{searchFilter})");
        }

        private string getGroupSearchFilter(string searchPattern)
        {
            string groupFilter;
            string searchFilter;
            if(Type == (int)LdapType.ActiveDirectory)
            {
                groupFilter = "(objectClass=group)";
                searchFilter = $"(|(cn={searchPattern})(name={searchPattern}))";
            }
            else if(Type == (int)LdapType.OpenLdap)
            {
                groupFilter = "(|(objectclass=group)(objectclass=groupofnames)(objectclass=groupofuniquenames))";
                searchFilter = $"(cn={searchPattern})";
            }
            else // LdapType.Default
            {
                groupFilter = "(|(objectclass=group)(objectclass=groupofnames)(objectclass=groupofuniquenames))";
                searchFilter = $"(|(dc={searchPattern})(o={searchPattern})(ou={searchPattern})(cn={searchPattern})(uid={searchPattern})(mail={searchPattern}))";
            }
            return ((searchPattern == null || searchPattern == "") ? groupFilter : $"(&{groupFilter}{searchFilter})");
        }

        /// <summary>
        /// Get the LdapEntry for the given user with option to validate credentials
        /// </summary>
        /// <returns>LdapEntry for the given user if found</returns>
        public LdapEntry? GetLdapEntry(UiUser user, bool validateCredentials)
        {
            Log.WriteDebug("User Validation", $"Validating User: \"{user.Name}\" ...");
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    TryBind(connection, SearchUser, SearchUserPwd);

                    LdapSearchConstraints cons = connection.SearchConstraints;
                    cons.ReferralFollowing = true;
                    connection.Constraints = cons;

                    List<LdapEntry> possibleUserEntries = new List<LdapEntry>();

                    // If dn was already provided
                    if (!user.Dn.IsNullOrEmpty())
                    {
                        // Try to read user entry directly
                        LdapEntry? userEntry = connection.Read(user.Dn);
                        if (userEntry != null)
                        {
                            possibleUserEntries.Add(userEntry);
                        }
                    }
                    else // Dn was not provided, search for user name
                    {
                        string[] attrList = new string[] { "*", "memberof" };
                        string userSearchFilter = getUserSearchFilter(user.Name);

                        // Search for users in ldap with same name as user to validate
                        possibleUserEntries = ((LdapSearchResults)connection.Search(
                            UserSearchPath,             // top-level path under which to search for user
                            LdapConnection.ScopeSub,    // search all levels beneath
                            userSearchFilter,
                            attrList,
                            typesOnly: false
                        )).ToList();
                    }

                    // If credentials are not checked return user that was found first
                    // It could happen that multiple users with the same name were found (impossible if dn was provided)
                    if (!validateCredentials && possibleUserEntries.Count > 0)
                    {
                        return possibleUserEntries.First();
                    }
                    // If credentials should be checked
                    else if (validateCredentials)
                    {
                        // Multiple users with the same name could have been found (impossible if dn was provided)
                        foreach (LdapEntry possibleUserEntry in possibleUserEntries)
                        {
                            // Check credentials - if multiple users were found and the credentials are valid this is most definitely the correct user
                            if (CredentialsValid(connection, possibleUserEntry.Dn, user.Password))
                            {
                                return possibleUserEntry;
                            }
                        }
                    }
                }
            }
            catch (LdapException ldapException)
            {
                Log.WriteInfo("Ldap entry exception", $"Ldap entry search at \"{Address}:{Port}\" lead to exception: {ldapException.Message}");
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception \"{Address}:{Port}\"", "Unexpected error while trying to validate user", exception);
            }

            Log.WriteDebug("Invalid Credentials", $"Invalid login credentials - could not authenticate user \"{ user.Name}\" on {Address}:{Port}.");
            return null;
        }

        private bool CredentialsValid(LdapConnection connection, string dn, string password)
        {
            try
            {
                Log.WriteDebug("User Validation", $"Trying to validate user with distinguished name: \"{dn}\" ...");

                // Try to authenticate as user with given password
                if (TryBind(connection, dn, password))
                {
                    // Return ldap dn
                    Log.WriteDebug("User Validation", $"\"{dn}\" successfully authenticated in {Address}:{Port}.");
                    return true;
                }
                else
                {
                    // this will probably never be reached as an error is thrown before
                    // Incorrect password - do nothing, assume its another user with the same username
                    Log.WriteDebug($"User Validation {Address}:{Port}", $"Found user with matching uid but different pwd: \"{dn}\".");
                }
            }
            catch (LdapException exc)
            {
                if (exc.ResultCode == 49)  // 49 = InvalidCredentials
                    Log.WriteDebug($"Duplicate user {Address}:{Port}", $"Found user with matching uid but different pwd: \"{dn}\".");
                else
                    Log.WriteError($"Ldap exception {Address}:{Port}", $"Unexpected error while trying to validate user \"{dn}\".");
            }
            return false;
        }

        /// <summary>
        /// Get the EmailAddress for the given user
        /// </summary>
        /// <returns>EmailAddress of the given user</returns>
        public string GetEmail(LdapEntry user)
        {
            return user.GetAttributeSet().ContainsKey("mail") ? user.GetAttribute("mail").StringValue : "";
        }

        /// <summary>
        /// Get the groups for the given user
        /// </summary>
        /// <returns>list of groups for the given user</returns>
        public List<string> GetGroups(LdapEntry user)
        {
            // Simplest way as most ldap types should provide the memberof attribute.
            // - Probably this doesn't work for nested groups.
            // - Some systems may only save the "primaryGroupID", then we would have to resolve the name.
            // - Some others may force us to look into all groups to find the membership.
            List<string> groups = new List<string>();
            foreach (var attribute in user.GetAttributeSet())
            {
                if (attribute.Name.ToLower() == "memberof")
                {
                    foreach (string membership in attribute.StringValueArray)
                    {
                        if (GroupSearchPath != null && membership.EndsWith(GroupSearchPath))
                        {
                            groups.Add(membership);
                        }
                    }
                }
            }
            return groups;
        }

        /// <summary>
        /// Change the password of the given user
        /// </summary>
        /// <returns>error message if not successful</returns>
        public string ChangePassword(string userDn, string oldPassword, string newPassword)
        {
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Try to authenticate as user with old password
                    if (TryBind(connection, userDn, oldPassword))
                    {
                        // authentication was successful (user is bound): set new password
                        LdapAttribute attribute = new LdapAttribute("userPassword", newPassword);
                        LdapModification[] mods = { new LdapModification(LdapModification.Replace, attribute) };

                        connection.Modify(userDn, mods);
                        Log.WriteDebug("Change password", $"Password for user {userDn} changed in {Address}:{Port}");
                    }
                    else
                    {
                        return "wrong old password";
                    }
                }
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            return "";
        }

        /// <summary>
        /// Set the password of the given user
        /// </summary>
        /// <returns>error message if not successful</returns>
        public string SetPassword(string userDn, string newPassword)
        {
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    if (TryBind(connection, WriteUser, WriteUserPwd))
                    {
                        // authentication was successful: set new password
                        LdapAttribute attribute = new LdapAttribute("userPassword", newPassword);
                        LdapModification[] mods = { new LdapModification(LdapModification.Replace, attribute) };

                        connection.Modify(userDn, mods);
                        Log.WriteDebug("Change password", $"Password for user {userDn} changed in {Address}:{Port}");
                    }
                    else
                    {
                        return "error in write user authentication";
                    }
                }
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            return "";
        }

        /// <summary>
        /// Get the roles for the given DN list
        /// </summary>
        /// <returns>list of roles for the given DN list</returns>
        public List<string> GetRoles(List<string> dnList)
        {
            return GetMemberships(dnList, RoleSearchPath);
        }

        /// <summary>
        /// Get the groups for the given DN list
        /// </summary>
        /// <returns>list of groups for the given DN list</returns>
        public List<string> GetGroups(List<string> dnList)
        {
            return GetMemberships(dnList, GroupSearchPath);
        }

        private List<string> GetMemberships(List<string> dnList, string? searchPath)
        {
            List<string> userMemberships = new List<string>();

            // If this Ldap is containing roles / groups
            if (searchPath != null && searchPath != "")
            {
                try
                {
                    // Connect to Ldap
                    using (LdapConnection connection = Connect())
                    {     
                        // Authenticate as search user
                        connection.Bind(SearchUser, AesEnc.Decrypt(SearchUserPwd, AesEnc.GetMainKey()));

                        // Search for Ldap roles / groups in given directory          
                        int searchScope = LdapConnection.ScopeSub; // TODO: Correct search scope?
                        string searchFilter = $"(&(objectClass=groupOfUniqueNames)(cn=*))";
                        LdapSearchResults searchResults = (LdapSearchResults)connection.Search(searchPath, searchScope, searchFilter, null, false);                

                        // convert dnList to lower case to avoid case problems
                        dnList = dnList.ConvertAll(dn => dn.ToLower());

                        // Foreach found role / group
                        foreach (LdapEntry entry in searchResults)
                        {
                            Log.WriteDebug("Ldap Roles/Groups", $"Try to get roles / groups from ldap entry {entry.GetAttribute("cn").StringValue}");

                            // Get dn of users having current role / group
                            LdapAttribute members = entry.GetAttribute("uniqueMember");
                            string[] memberDn = members.StringValueArray;

                            // Foreach user 
                            foreach (string currentDn in memberDn)
                            {
                                Log.WriteDebug("Ldap Roles/Groups", $"Checking if current Dn: \"{currentDn}\" is user Dn. Then user has current role / group.");

                                // Check if current user dn is matching with given user dn => Given user has current role / group
                                if (dnList.Contains(currentDn.ToLower()))
                                {
                                    // Get name and add it to list of roles / groups of given user
                                    string name = entry.GetAttribute("cn").StringValue;
                                    userMemberships.Add(name);
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get memberships", exception);
                }
            }

            Log.WriteDebug($"Found the following roles / groups for user {dnList.FirstOrDefault()} in {Address}:{Port}:", string.Join("\n", userMemberships));
            return userMemberships;
        }

        /// <summary>
        /// Get all roles
        /// </summary>
        /// <returns>list of roles</returns>
        public List<RoleGetReturnParameters> GetAllRoles()
        {
            List<RoleGetReturnParameters> roleUsers = new List<RoleGetReturnParameters>();

            // If this Ldap is containing roles
            if (HasRoleHandling())
            {
                try
                {
                    // Connect to Ldap
                    using (LdapConnection connection = Connect())
                    {     
                        // Authenticate as search user
                        TryBind(connection, SearchUser, SearchUserPwd);

                        // Search for Ldap roles in given directory          
                        int searchScope = LdapConnection.ScopeSub; // TODO: Correct search scope?
                        string searchFilter = $"(&(objectClass=groupOfUniqueNames)(cn=*))";
                        LdapSearchResults searchResults = (LdapSearchResults)connection.Search(RoleSearchPath, searchScope, searchFilter, null, false);                

                        // Foreach found role
                        foreach (LdapEntry entry in searchResults)
                        {
                            List<RoleAttribute> attributes = new List<RoleAttribute>();
                            string roleDesc = entry.GetAttribute("description").StringValue;
                            attributes.Add(new RoleAttribute(){ Key = "description", Value = roleDesc });

                            string[] roleMemberDn = entry.GetAttribute("uniqueMember").StringValueArray;
                            foreach (string currentDn in roleMemberDn)
                            {
                                if (currentDn != "")
                                {
                                    attributes.Add(new RoleAttribute(){ Key = "user", Value = currentDn });
                                }
                            }
                            roleUsers.Add(new RoleGetReturnParameters(){ Role = entry.Dn, Attributes = attributes});
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all roles", exception);
                }
            }
            return roleUsers;
        }

        /// <summary>
        /// Search all groups with search pattern
        /// </summary>
        /// <returns>list of groups</returns>
        public List<string> GetAllGroups(string searchPattern)
        {
            List<string> allGroups = new List<string>();
            try
            {
                // Connect to Ldap
                using (LdapConnection connection = Connect())
                {     
                    // Authenticate as search user
                    connection.Bind(SearchUser, SearchUserPwd);

                    // Search for Ldap groups in given directory          
                    int searchScope = LdapConnection.ScopeSub;
                    LdapSearchResults searchResults = (LdapSearchResults)connection.Search(GroupSearchPath, searchScope, getGroupSearchFilter(searchPattern), null, false);                

                    foreach (LdapEntry entry in searchResults)
                    {
                        allGroups.Add(entry.Dn);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all groups", exception);
            }
            return allGroups;
        }

        /// <summary>
        /// Get all internal groups
        /// </summary>
        /// <returns>list of groups</returns>
        public List<GroupGetReturnParameters> GetAllInternalGroups()
        {
            List<GroupGetReturnParameters> allGroups = new List<GroupGetReturnParameters>();

            try
            {
                // Connect to Ldap
                using (LdapConnection connection = Connect())
                {     
                    // Authenticate as search user
                    connection.Bind(SearchUser, SearchUserPwd);

                    // Search for Ldap groups in given directory          
                    int searchScope = LdapConnection.ScopeSub;
                    LdapSearchResults searchResults = (LdapSearchResults)connection.Search(GroupSearchPath, searchScope, getGroupSearchFilter(""), null, false);                

                    foreach (LdapEntry entry in searchResults)
                    {
                        List<string> members = new List<string>();
                        string[] groupMemberDn = entry.GetAttribute("uniqueMember").StringValueArray;
                        foreach (string currentDn in groupMemberDn)
                        {
                            if (currentDn != "")
                            {
                                members.Add(currentDn);
                            }
                        }
                        allGroups.Add(new GroupGetReturnParameters()
                        {
                            GroupDn = entry.Dn, 
                            Members = members, 
                            OwnerGroup = (entry.GetAttributeSet().ContainsKey("businessCategory") ? (entry.GetAttribute("businessCategory").StringValue.ToLower() == "ownergroup") : false)
                        });
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all internal groups", exception);
            }
            return allGroups;
        }

        /// <summary>
        /// Search all users with search pattern
        /// </summary>
        /// <returns>list of users</returns>
        public List<LdapUserGetReturnParameters> GetAllUsers(string searchPattern)
        {
            Log.WriteDebug("GetAllUsers", $"Looking for users with pattern {searchPattern} in {Address}:{Port}");
            List<LdapUserGetReturnParameters> allUsers = new List<LdapUserGetReturnParameters>();

            try
            {
                // Connect to Ldap
                using (LdapConnection connection = Connect())
                {     
                    // Authenticate as search user
                    connection.Bind(SearchUser, SearchUserPwd);

                    // Search for Ldap users in given directory          
                    int searchScope = LdapConnection.ScopeSub;

                    LdapSearchConstraints cons = connection.SearchConstraints;
                    cons.ReferralFollowing = true;
                    connection.Constraints = cons;

                    LdapSearchResults searchResults = (LdapSearchResults)connection.Search(UserSearchPath, searchScope, getUserSearchFilter(searchPattern), null, false);

                    foreach (LdapEntry entry in searchResults)
                    {
                        allUsers.Add(new LdapUserGetReturnParameters()
                        {
                            UserDn = entry.Dn,
                            Email = (entry.GetAttributeSet().ContainsKey("mail") ? entry.GetAttribute("mail").StringValue : null)
                        });
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all users", exception);
            }
            return allUsers;
        }

        /// <summary>
        /// Add new user
        /// </summary>
        /// <returns>true if user added</returns>
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
                    //connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    string userName = (new FWO.Api.Data.DistName(userDn)).UserName;
                    LdapAttributeSet attributeSet = new LdapAttributeSet
                    {
                        new LdapAttribute("objectclass", "inetOrgPerson"),
                        new LdapAttribute("sn", userName),
                        new LdapAttribute("cn", userName),
                        new LdapAttribute("uid", userName),
                        new LdapAttribute("userPassword", password),
                        new LdapAttribute("mail", email)
                    };

                    LdapEntry newEntry = new LdapEntry( userDn, attributeSet );

                    try
                    {
                        //Add the entry to the directory
                        connection.Add(newEntry);
                        userAdded = true;
                        Log.WriteDebug("Add user", $"User {userName} added in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Add User", $"couldn't add user to LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to add user", exception);
            }
            return userAdded;
        }

        /// <summary>
        /// Update user
        /// </summary>
        /// <returns>true if user updated</returns>
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
                    TryBind(connection, WriteUser, WriteUserPwd);
                    // connection.Bind(WriteUser, WriteUserPwd);

                    LdapAttribute attribute = new LdapAttribute("mail", email);
                    LdapModification[] mods = { new LdapModification(LdapModification.Replace, attribute) };

                    try
                    {
                        //Add the entry to the directory
                        connection.Modify(userDn, mods);
                        userUpdated = true;
                        Log.WriteDebug("Update user", $"User {userDn} updated in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Update User", $"couldn't update user in LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to update user", exception);
            }
            return userUpdated;
        }

        /// <summary>
        /// Delete user
        /// </summary>
        /// <returns>true if user deleted</returns>
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
                    TryBind(connection, WriteUser, WriteUserPwd);
                    // connection.Bind(WriteUser, WriteUserPwd);

                    try
                    {
                        //Delete the entry in the directory
                        connection.Delete(userDn);
                        userDeleted = true;
                        Log.WriteDebug("Delete user", $"User {userDn} deleted in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Delete User", $"couldn't delete user in LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to delete user", exception);
            }
            return userDeleted;
        }

        /// <summary>
        /// Add new group
        /// </summary>
        /// <returns>group DN if user added</returns>
        public string AddGroup(string groupName, bool ownerGroup)
        {
            Log.WriteInfo("Add Group", $"Trying to add Group: \"{groupName}\"");
            bool groupAdded = false;
            string groupDn = "";
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    // connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    groupDn = $"cn={groupName},{GroupSearchPath}";
                    LdapAttributeSet attributeSet = new LdapAttributeSet();
                    attributeSet.Add( new LdapAttribute("objectclass", "groupofuniquenames"));
                    attributeSet.Add( new LdapAttribute("uniqueMember", ""));
                    if (ownerGroup)
                    {
                        attributeSet.Add( new LdapAttribute("businessCategory", "ownergroup"));
                    }

                    LdapEntry newEntry = new LdapEntry( groupDn, attributeSet );

                    try
                    {
                        //Add the entry to the directory
                        connection.Add(newEntry);
                        groupAdded = true;
                        Log.WriteDebug("Add group", $"Group {groupName} added in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Add Group", $"couldn't add group to LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to add group", exception);
            }
            return (groupAdded ? groupDn : "");
        }

        /// <summary>
        /// Update group name
        /// </summary>
        /// <returns>new group DN if group updated</returns>
        public string UpdateGroup(string oldName, string newName)
        {
            Log.WriteInfo("Update Group", $"Trying to update Group: \"{oldName}\"");
            bool groupUpdated = false;
            string oldGroupDn = $"cn={oldName},{GroupSearchPath}";
            string newGroupRdn = $"cn={newName}";

            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    // connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    try
                    {
                        //Add the entry to the directory
                        connection.Rename(oldGroupDn, newGroupRdn, true);
                        groupUpdated = true;
                        Log.WriteDebug("Update group", $"Group {oldName} renamed to {newName} in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Update Group", $"couldn't update group in LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to update group", exception);
            }
            return (groupUpdated ? $"{newGroupRdn},{GroupSearchPath}" : "");
        }

        /// <summary>
        /// Delete group
        /// </summary>
        /// <returns>true if group deleted</returns>
        public bool DeleteGroup(string groupName)
        {
            Log.WriteInfo("Delete Group", $"Trying to delete Group: \"{groupName}\"");
            bool groupDeleted = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    // connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    try
                    {
                        //Delete the entry in the directory
                        string groupDn = $"cn={groupName},{GroupSearchPath}";
                        connection.Delete(groupDn);
                        groupDeleted = true;
                        Log.WriteDebug("Delete group", $"Group {groupName} deleted in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Delete Group", $"couldn't delete group in LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to delete group", exception);
            }
            return groupDeleted;
        }

        /// <summary>
        /// Add user to entry
        /// </summary>
        /// <returns>true if user added</returns>
        public bool AddUserToEntry(string userDn, string entry)
        {
            Log.WriteInfo("Add User to Entry", $"Trying to add User: \"{userDn}\" to Entry: \"{entry}\"");
            return ModifyUserInEntry(userDn, entry, LdapModification.Add);
        }
        
        /// <summary>
        /// Remove user from entry
        /// </summary>
        /// <returns>true if user removed</returns>
        public bool RemoveUserFromEntry(string userDn, string entry)
        {
            Log.WriteInfo("Remove User from Entry", $"Trying to remove User: \"{userDn}\" from Entry: \"{entry}\"");
            return ModifyUserInEntry(userDn, entry, LdapModification.Delete);
        }

        /// <summary>
        /// Remove user from all entries
        /// </summary>
        /// <returns>true if user removed from all entries</returns>
        public bool RemoveUserFromAllEntries(string userDn)
        {
            List<string> dnList = new List<string>();
            dnList.Add(userDn); // group memberships do not need to be regarded here
            List<string> roles = GetRoles(dnList);
            bool allRemoved = true;
            foreach(var role in roles)
            {
                allRemoved &= RemoveUserFromEntry(userDn, $"cn={role},{RoleSearchPath}");
            }
            List<string> groups = GetGroups(dnList);
            foreach(var group in groups)
            {
                allRemoved &= RemoveUserFromEntry(userDn, $"cn={group},{GroupSearchPath}");
            }
            return allRemoved;
        }

        private bool ModifyUserInEntry(string userDn, string entry, int LdapModification)
        {
            bool userModified = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    // connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    // Add a new value to the description attribute
                    LdapAttribute attribute = new LdapAttribute("uniquemember", userDn);
                    LdapModification[] mods = { new LdapModification(LdapModification, attribute) }; 

                    try
                    {
                        //Modify the entry in the directory
                        connection.Modify(entry, mods);
                        userModified = true;
                        Log.WriteDebug("Modify Entry", $"Entry {entry} modified in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Modify Entry", $"maybe entry doesn't exist in this LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to modify user", exception);
            }
            return userModified;
        }

        /// <summary>
        /// Add new tenant
        /// </summary>
        /// <returns>true if tenant added</returns>
        public bool AddTenant(string tenantName)
        {
            Log.WriteInfo("Add Tenant", $"Trying to add Tenant: \"{tenantName}\"");
            bool tenantAdded = false;
            string tenantDn = "";
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    // connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    tenantDn = $"ou={tenantName},{UserSearchPath}";
                    LdapAttributeSet attributeSet = new LdapAttributeSet();
                    attributeSet.Add( new LdapAttribute("objectclass", "organizationalUnit"));

                    LdapEntry newEntry = new LdapEntry( tenantDn, attributeSet );

                    try
                    {
                        //Add the entry to the directory
                        connection.Add(newEntry);
                        tenantAdded = true;
                        Log.WriteDebug("Add tenant", $"Tenant {tenantName} added in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Add Tenant", $"couldn't add tenant to LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to add tenant", exception);
            }
            return tenantAdded;
        }

        /// <summary>
        /// Delete tenant
        /// </summary>
        /// <returns>true if tenant deleted</returns>
        public bool DeleteTenant(string tenantName)
        {
            Log.WriteDebug("Delete Tenant", $"Trying to delete Tenant: \"{tenantName}\" from Ldap");
            bool tenantDeleted = false;
            try         
            {
                // Connecting to Ldap
                using (LdapConnection connection = Connect())
                {
                    // Authenticate as write user
                    // connection.Bind(WriteUser, WriteUserPwd);
                    TryBind(connection, WriteUser, WriteUserPwd);

                    try
                    {
                        string tenantDn = "ou=" + tenantName + "," + UserSearchPath;

                        //Delete the entry in the directory
                        connection.Delete(tenantDn);
                        tenantDeleted = true;
                        Log.WriteDebug("Delete Tenant", $"tenant {tenantDn} deleted in {Address}:{Port}");
                    }
                    catch(Exception exception)
                    {
                        Log.WriteInfo("Delete Tenant", $"couldn't delete tenant in LDAP {Address}:{Port}: {exception.ToString()}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to delete tenant", exception);
            }
            return tenantDeleted;
        }
    }
}

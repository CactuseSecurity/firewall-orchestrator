using FWO.Data;
using FWO.Data.Middleware;
using FWO.Encryption;
using FWO.Logging;
using Novell.Directory.Ldap;
using System.Text.RegularExpressions;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the ldap transactions
	/// </summary>
	public partial class Ldap : LdapConnectionBase
	{

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

        [GeneratedRegex(@"(\bcn|\bou|\bdc|\bo|\bc|\bst|\bl)=(.*?)(?=,[A-Za-z]+=|$)", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex MyRegex();

        private static string ConvertHexCommaToComma(string dn)
        {
            return MyRegex().Replace(dn, match =>
            {
                string attribute = match.Groups[1].Value; // RDN type (e.g., CN, OU, DC)
                string value = match.Groups[2].Value; // RDN value

                // Convert hex-escaped commas to commas
                value = Regex.Replace(value, @"\\2c", "\\,");

                return $"{attribute}={value}";
            });
        }

		private List<string> GetMemberships(List<string> dnList, string? searchPath)
		{
			List<string> userMemberships = [];

			// If this Ldap is containing roles / groups
			if (searchPath != null && searchPath != "")
			{
				try
				{
                    using LdapConnection connection = Connect();
                    // Authenticate as search user
                    TryBind(connection, SearchUser, AesEnc.Decrypt(SearchUserPwd, AesEnc.GetMainKey()));

                    // Search for Ldap roles / groups in given directory          
                    int searchScope = LdapConnection.ScopeSub; // TODO: Correct search scope?
                    string searchFilter = $"(&(objectClass=groupOfUniqueNames)(cn=*))";
                    LdapSearchResults allExistingGroupsAndRoles = (LdapSearchResults)connection.Search(searchPath, searchScope, searchFilter, null, false);

                    // convert dnList to lower case to avoid case problems
                    dnList = dnList.ConvertAll(dn => dn.ToLower());

                    // Foreach found role / group
                    foreach (LdapEntry entry in allExistingGroupsAndRoles)
                    {
                        Log.WriteDebug("Ldap Roles/Groups", $"Try to get roles / groups from ldap entry {entry.GetAttribute("cn").StringValue}");

                        // Get dn of users having current role / group
                        LdapAttribute members = entry.GetAttribute("uniqueMember");
                        string[] memberDn = members.StringValueArray;

                        // Foreach user (member) of the current role/group:
                        foreach (string currentDn in memberDn)
                        {
                            if (currentDn != "") // ignore empty dn (could be caused by empty lines in LDAP)
                            {
                                string currentUserDnEscapedLower = ConvertHexCommaToComma(currentDn.ToLower());
                                Log.WriteDebug("Ldap Roles/Groups", $"Checking if current Dn: \"{currentUserDnEscapedLower}\" is user Dn. Then user has current role / group.");
                                // Check if current user dn is matching with given user dn => Given user has current role / group
                                if (dnList.Contains(currentUserDnEscapedLower))
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
			List<RoleGetReturnParameters> roleUsers = [];

			// If this Ldap is containing roles
			if (HasRoleHandling())
			{
				try
				{
                    using LdapConnection connection = Connect();
                    // Authenticate as search user
                    TryBind(connection, SearchUser, SearchUserPwd);

                    // Search for Ldap roles in given directory          
                    int searchScope = LdapConnection.ScopeSub; // TODO: Correct search scope?
                    string searchFilter = $"(&(objectClass=groupOfUniqueNames)(cn=*))";
                    LdapSearchResults searchResults = (LdapSearchResults)connection.Search(RoleSearchPath, searchScope, searchFilter, null, false);

                    // Foreach found role
                    foreach (LdapEntry entry in searchResults)
                    {
                        List<RoleAttribute> attributes = [];
                        string roleDesc = entry.GetAttribute("description").StringValue;
                        attributes.Add(new () { Key = "description", Value = roleDesc });

                        string[] roleMemberDn = entry.GetAttribute("uniqueMember").StringValueArray;
                        foreach (string currentDn in roleMemberDn)
                        {
                            if (currentDn != "")
                            {
                                attributes.Add(new () { Key = "user", Value = currentDn });
                            }
                        }
                        roleUsers.Add(new RoleGetReturnParameters() { Role = entry.Dn, Attributes = attributes });
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
			List<string> allGroups = [];
			try
			{
                using LdapConnection connection = Connect();
                // Authenticate as search user
                TryBind(connection, SearchUser, SearchUserPwd);

                // Search for Ldap groups in given directory          
                int searchScope = LdapConnection.ScopeSub;
                LdapSearchResults searchResults = (LdapSearchResults)connection.Search(GroupSearchPath, searchScope, GetGroupSearchFilter(searchPattern), null, false);

                foreach (LdapEntry entry in searchResults)
                {
                    allGroups.Add(entry.Dn);
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
			List<GroupGetReturnParameters> allGroups = [];

			try
			{
                using LdapConnection connection = Connect();
                // Authenticate as search user
                TryBind(connection, SearchUser, SearchUserPwd);

                // Search for Ldap groups in given directory          
                int searchScope = LdapConnection.ScopeSub;
                LdapSearchResults searchResults = (LdapSearchResults)connection.Search(GroupSearchPath, searchScope, GetGroupSearchFilter(""), null, false);

                foreach (LdapEntry entry in searchResults)
                {
                    List<string> members = [];
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
                        OwnerGroup = entry.GetAttributeSet().ContainsKey("businessCategory") && entry.GetAttribute("businessCategory").StringValue.Equals("ownergroup", StringComparison.CurrentCultureIgnoreCase)
                    });
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all internal groups", exception);
			}
			return allGroups;
		}

		/// <summary>
		/// Get all groups of an LDAP server matching a specific pattern
		/// </summary>
		/// <returns>list of groups</returns>
		public List<GroupGetReturnParameters> GetAllGroupObjects(string groupPattern)
		{
			List<GroupGetReturnParameters> allGroups = [];

			try
			{
                using LdapConnection connection = Connect();
                // Authenticate as search user
                TryBind(connection, SearchUser, SearchUserPwd);

                // Search for Ldap groups in given directory          
                int searchScope = LdapConnection.ScopeSub;
				string searchFilter = GetGroupSearchFilter(groupPattern);
                LdapSearchResults searchResults = (LdapSearchResults)connection.Search(GroupSearchPath, searchScope, searchFilter, null, false);

                foreach (LdapEntry entry in searchResults)
                {
                    List<string> members = [];
					if (entry.GetAttributeSet().ContainsKey(GetMemberKey()))
					{
						string[] groupMemberDn = entry.GetAttribute(GetMemberKey()).StringValueArray;
						foreach (string currentDn in groupMemberDn)
						{
							if (currentDn != "")
							{
								members.Add(currentDn);
							}
						}
					}
                    allGroups.Add(new GroupGetReturnParameters()
                    {
                        GroupDn = entry.Dn,
                        Members = members,
                        OwnerGroup = entry.GetAttributeSet().ContainsKey("businessCategory") && entry.GetAttribute("businessCategory").StringValue.Equals("ownergroup", StringComparison.CurrentCultureIgnoreCase)
                    });
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all internal groups", exception);
			}
			return allGroups;
		}

		/// <summary>
		/// Get member key depending on the LDAP type
		/// </summary>
		/// <returns>string with member key</returns>
		private string GetMemberKey()
		{
			string memberKey = "uniqueMember";
			if ((LdapType)Type == LdapType.ActiveDirectory)
			{
				memberKey = "member";
			}
			return memberKey;
		}

		/// <summary>
		/// Get members of an ldap group
		/// </summary>
		/// <returns>list of members</returns>
		public List<string> GetGroupMembers(string groupDn)
		{
			List<string> allMembers = [];

			if (!string.IsNullOrEmpty(GroupSearchPath) && groupDn.Contains(GroupSearchPath))
			{
				try
				{
                    using LdapConnection connection = Connect();
                    // Authenticate as search user
                    TryBind(connection, SearchUser, SearchUserPwd);
                    LdapEntry entry = connection.Read(groupDn);

                    if (entry != null)
                    {
                        string[] groupMemberDn = entry.GetAttribute(GetMemberKey()).StringValueArray;
                        foreach (string currentDn in groupMemberDn)
                        {
                            if (currentDn != "")
                            {
                                allMembers.Add(currentDn);
                            }
                        }
                    }
                }
				catch (Exception exception)
				{
					Log.WriteError($"Non-LDAP exception {Address}:{Port}", $"Unexpected error while trying to get all group members of group {groupDn}", exception);
				}
			}
			return allMembers;
		}

		/// <summary>
		/// Add new group
		/// </summary>
		/// <returns>group DN if user added</returns>
		public string AddGroup(string groupName, bool ownerGroup)
		{
			Log.WriteInfo("Add Group", $"Trying to add Group: \"{groupName}\"");
			bool groupAdded = false;
			string groupDn = groupName;
			try
			{
                using LdapConnection connection = Connect();
                // Authenticate as write user
                TryBind(connection, WriteUser, WriteUserPwd);

				if (!IsFullyQualifiedDn(groupDn))
				{
					groupDn = $"cn={groupName},{GroupSearchPath}";
				}
				LdapAttributeSet attributeSet = new ();
				attributeSet.Add(new LdapAttribute("objectclass", "groupofuniquenames"));
				attributeSet.Add(new LdapAttribute("uniqueMember", ""));
				if (ownerGroup)
				{
					attributeSet.Add(new LdapAttribute("businessCategory", "ownergroup"));
				}

                LdapEntry newEntry = new (groupDn, attributeSet);

                try
                {
                    //Add the entry to the directory
                    connection.Add(newEntry);
                    groupAdded = true;
                    Log.WriteDebug("Add group", $"Group {groupName} added in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Add Group", $"couldn't add group to LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to add group", exception);
			}
			return groupAdded ? groupDn : "";
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
                using LdapConnection connection = Connect();
                // Authenticate as write user
                TryBind(connection, WriteUser, WriteUserPwd);

                try
                {
                    //Add the entry to the directory
                    connection.Rename(oldGroupDn, newGroupRdn, true);
                    groupUpdated = true;
                    Log.WriteDebug("Update group", $"Group {oldName} renamed to {newName} in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Update Group", $"couldn't update group in LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to update group", exception);
			}
			return groupUpdated ? $"{newGroupRdn},{GroupSearchPath}" : "";
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
                using LdapConnection connection = Connect();
                // Authenticate as write user
                TryBind(connection, WriteUser, WriteUserPwd);

                try
                {
                    //Delete the entry in the directory
                    string groupDn = $"cn={groupName},{GroupSearchPath}";
                    connection.Delete(groupDn);
                    groupDeleted = true;
                    Log.WriteDebug("Delete group", $"Group {groupName} deleted in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Delete Group", $"couldn't delete group in LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to delete group", exception);
			}
			return groupDeleted;
		}

    }
}

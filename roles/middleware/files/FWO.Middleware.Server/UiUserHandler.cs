using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using FWO.Config.File;
using FWO.GlobalConstants;
using FWO.Api.Data;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Helper class to read config value for expiration time
	/// </summary>
	public class ConfExpirationTime
	{
		/// <summary>
		/// config value for expiration time
		/// </summary>
		[JsonProperty("config_value"), JsonPropertyName("config_value")]
		public int ExpirationValue { get; set; }
	}

	/// <summary>
	/// Handler class for local Ui user
	/// </summary>
	public class UiUserHandler
	{
		private readonly string jwtToken;
		private ApiConnection apiConn;

		/// <summary>
		/// Constructor needing the jwt token
		/// </summary>
		public UiUserHandler(string jwtToken)
		{
			this.jwtToken = jwtToken;
			apiConn = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwtToken);
		}

		/// <summary>
		/// Get the configurated value for the session timeout.
		/// </summary>
		/// <returns>session timeout value in minutes</returns>
		public async Task<int> GetExpirationTime()
		{
			int expirationTime = 60 * 12;
			try
			{
				List<ConfExpirationTime> resultList = await apiConn.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, new { key = "sessionTimeout" });
				if (resultList.Count > 0)
				{
					expirationTime = resultList[0].ExpirationValue;
				}
			}
			catch (Exception exeption)
			{
				Log.WriteError("Get ExpirationTime Error", $"Error while trying to find config value in database. Taking default value", exeption);
			}
			return expirationTime;
		}

		/// <summary>
		/// if the user logs in for the first time, user details (excluding password) are written to DB bia API
		/// the database id is retrieved and added to the user 
		/// the user id is needed for allowing access to report_templates
		/// </summary>
		/// <returns> user including its db id </returns>
		public async Task<UiUser> HandleUiUserAtLogin(UiUser user)
		{
			ApiConnection apiConn = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwtToken);
			bool userSetInDb = false;
			try
			{
				UiUser[] existingUsers = await apiConn.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = user.Dn });

				if (existingUsers.Length > 0)
				{
					user.DbId = existingUsers[0].DbId;
					user.PasswordMustBeChanged = await UpdateLastLogin(apiConn, user.DbId);
					userSetInDb = true;
				}
				else
				{
					Log.WriteDebug("User not found", $"Couldn't find {user.Name} in internal database");
				}
				await GetOwnerships(apiConn, user);
			}
			catch (Exception exeption)
			{
				Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", exeption);
			}

			if (!userSetInDb)
			{
				Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to internal database.");
				await UpsertUiUser(apiConn, user, true);
			}
			return user;
		}

		/// <summary>
		/// add the ownerships to the given user
		/// </summary>
		public static async Task GetOwnerships(ApiConnection apiConn, UiUser user)
		{
			try
			{
				List<FwoOwner> dirOwnerships = await apiConn.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForUser, new { userDn = user.Dn });
				foreach (var owner in dirOwnerships)
				{
					user.Ownerships.Add(owner.Id);
				}

				if (user.Groups != null)
				{
					List<FwoOwner> apps = await apiConn.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
					foreach (var grp in user.Groups)
					{
						string grpName = new DistName(grp).Group;
						if (grpName.StartsWith(GlobalConst.kModellerGroup))
						{
							FwoOwner? owner = apps.FirstOrDefault(x => x.ExtAppId == grpName.Substring(GlobalConst.kModellerGroup.Length));
							if (owner != null)
							{
								user.Ownerships.Add(owner.Id);
							}
						}
					}
				}
			}
			catch (Exception exeption)
			{
				Log.WriteError("Get ownerships", $"Ownerships could not be detemined for User {user.Name}.", exeption);
			}
		}

		/// <summary>
		/// add user to uiuser - either with or without current login time
		/// </summary>
		/// <returns>void</returns> 
		public static async Task UpsertUiUser(ApiConnection apiConn, UiUser user, bool loginHappened = false)
		{
			try
			{
				// add new user to uiuser
				if (loginHappened)
				{
					var VariablesWithLogin = new
					{
						uuid = user.Dn,
						uiuser_username = user.Name,
						uiuser_first_name = user.Firstname,
						uiuser_last_name = user.Lastname,
						email = user.Email,
						tenant = user.Tenant != null ? user.Tenant.Id : (int?)null,
						passwordMustBeChanged = false,
						ldapConnectionId = user.LdapConnection.Id,
						loginTime = DateTime.UtcNow
					};
					ReturnId[]? returnIds = (await apiConn.SendQueryAsync<NewReturning>(AuthQueries.upsertUiUser, VariablesWithLogin)).ReturnIds;
					if (returnIds != null)
					{
						user.DbId = returnIds[0].NewId;
					}
				}
				else
				{
					var VariablesWithoutLogin = new
					{
						uuid = user.Dn,
						uiuser_username = user.Name,
						uiuser_first_name = user.Firstname,
						uiuser_last_name = user.Lastname,
						email = user.Email,
						tenant = user.Tenant != null ? user.Tenant.Id : (int?)null,
						passwordMustBeChanged = false,
						ldapConnectionId = user.LdapConnection.Id
					};
					ReturnId[]? returnIds = (await apiConn.SendQueryAsync<NewReturning>(AuthQueries.upsertUiUser, VariablesWithoutLogin)).ReturnIds;
					if (returnIds != null)
					{
						user.DbId = returnIds[0].NewId;
					}
				}
			}
			catch (Exception exeption)
			{
				Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", exeption);
			}
		}

		private static async Task<bool> UpdateLastLogin(ApiConnection apiConn, int id)
		{
			try
			{
				var Variables = new
				{
					id = id,
					loginTime = DateTime.UtcNow
				};
				return (await apiConn.SendQueryAsync<ReturnId>(AuthQueries.updateUserLastLogin, Variables)).PasswordMustBeChanged;
			}
			catch (Exception exeption)
			{
				Log.WriteError("Update User Error", $"User {id} could not be updated in database.", exeption);
			}
			return true;
		}

		/// <summary>
		/// Update the passwordMustBeChanged flag.
		/// </summary>
		public static async Task UpdateUserPasswordChanged(ApiConnection apiConn, string userDn, bool passwordMustBeChanged = false)
		{
			try
			{
				var Variables = new
				{
					dn = userDn,
					passwordMustBeChanged = passwordMustBeChanged,
					changeTime = DateTime.UtcNow
				};
				await apiConn.SendQueryAsync<ReturnId>(AuthQueries.updateUserPasswordChange, Variables);
			}
			catch (Exception exeption)
			{
				Log.WriteError("Update User Error", $"User {userDn} could not be updated in database.", exeption);
			}
		}
	}
}

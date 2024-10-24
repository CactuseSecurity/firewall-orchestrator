using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using FWO.Middleware.RequestParameters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers
{
	/// <summary>
	/// Controller class for user api
	/// </summary>

	//[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly List<Ldap> ldaps;
		private readonly ApiConnection apiConnection;

		/// <summary>
		/// Constructor needing ldap list and connection
		/// </summary>
		public UserController(List<Ldap> ldaps, ApiConnection apiConnection)
		{
			this.ldaps = ldaps;
			this.apiConnection = apiConnection;
		}

		// GET: api/<UserController>
		/// <summary>
		/// Get all locally known users.
		/// </summary>
		/// <returns>List of all locally known users</returns>
		[HttpGet]
		[Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
		public async Task<List<UserGetReturnParameters>> Get()
		{
			List<UiUser> users = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUsers)).ToList();
			List<UserGetReturnParameters> userList = [];
			foreach (UiUser user in users)
			{
				if (user.DbId != 0)
				{
					userList.Add(user.ToApiParams());
				}
			}
			return userList;
		}

		// GET api/<ValuesController>/5
		/// <summary>
		/// Search user in specified Ldap
		/// </summary>
		/// <remarks>
		/// LdapId (required) &#xA;
		/// SearchPattern (optional) &#xA;
		/// </remarks>
		/// <param name="parameters">LdapUserGetParameters</param>
		/// <returns>List of users</returns>
		[HttpPost("Get")]
		[Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
		public async Task<List<LdapUserGetReturnParameters>> Get([FromBody] LdapUserGetParameters parameters)
		{
			List<LdapUserGetReturnParameters> allUsers = [];

			foreach (Ldap currentLdap in ldaps)
			{
				if (currentLdap.Id == parameters.LdapId)
				{
					await Task.Run(() =>
					{
						// Get all users from current Ldap
						allUsers = currentLdap.GetAllUsers(parameters.SearchPattern);
					});
				}
			}

			// Return status and result
			return allUsers;
		}

		// POST api/<ValuesController>
		/// <summary>
		/// Add user to specified Ldap
		/// </summary>
		/// <remarks>
		/// LdapId (required) &#xA;
		/// UserDn (required) &#xA;
		/// Password (required) &#xA;
		/// Email (optional) &#xA;
		/// TenantId (required) &#xA;
		/// PwChangeRequired (required) &#xA;
		/// </remarks>
		/// <param name="parameters">UserAddParameters</param>
		/// <returns>Id of new user, 0 if no user could be created</returns>
		[HttpPost]
		[Authorize(Roles = $"{Roles.Admin}")]
		public async Task<int> Add([FromBody] UserAddParameters parameters)
		{
			string email = parameters.Email ?? "";

			bool userAdded = false;
			int userId = 0;

			foreach (Ldap currentLdap in ldaps)
			{
				// Try to add user to current Ldap
				if ((currentLdap.Id == parameters.LdapId || parameters.LdapId == 0) && currentLdap.IsWritable())
				{
					await Task.Run(() =>
					{
						if (currentLdap.AddUser(parameters.UserDn, parameters.Password, email))
						{
							userAdded = true;
							Log.WriteAudit("AddUser", $"user {parameters.UserDn} successfully added to Ldap Id: {parameters.LdapId} Name: {currentLdap.Host()}");
						}
					});
				}
			}
			if (userAdded)
			{
				// Try to add user to local db
				try
				{
					var Variables = new
					{
						uuid = parameters.UserDn,
						uiuser_username = new DistName(parameters.UserDn).UserName,
						email = email,
						uiuser_first_name = parameters.Firstname,
						uiuser_last_name = parameters.Lastname,
						tenant = parameters.TenantId,
						passwordMustBeChanged = parameters.PwChangeRequired,
						ldapConnectionId = parameters.LdapId != 0 ? parameters.LdapId : (int?)null
					};
					ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(AuthQueries.upsertUiUser, Variables)).ReturnIds;
					if (returnIds != null)
					{
						userId = returnIds[0].NewId;
					}
				}
				catch (Exception exception)
				{
					userId = 0;
					Log.WriteAudit("AddUser", $"Adding User {parameters.UserDn} locally failed: {exception.Message}");
				}
			}
			return userId;
		}

		// PUT api/<ValuesController>/5
		/// <summary>
		/// Update user (email) in specified Ldap
		/// </summary>
		/// <remarks>
		/// LdapId (required) &#xA;
		/// UserId (required) &#xA;
		/// Email (optional) &#xA;
		/// </remarks>
		/// <param name="parameters">UserEditParameters</param>
		/// <returns>true, if user could be updated</returns>
		[HttpPut]
		[Authorize(Roles = $"{Roles.Admin}")]
		public async Task<bool> Change([FromBody] UserEditParameters parameters)
		{
			string email = parameters.Email ?? "";
			UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");
			bool userUpdated = false;

			foreach (Ldap currentLdap in ldaps)
			{
				// Try to update user in current Ldap
				if ((currentLdap.Id == parameters.LdapId || parameters.LdapId == 0) && currentLdap.IsWritable())
				{
					await Task.Run(() =>
					{
						if (currentLdap.UpdateUser(user.Dn, email))
						{
							userUpdated = true;
							Log.WriteAudit("UpdateUser", $"User {user.Dn} updated in Ldap Id: {parameters.LdapId} Name: {currentLdap.Host()}");
						}
					});
				}
			}
			if (userUpdated)
			{
				// Try to update user in local db
				try
				{
					var Variables = new
					{
						id = parameters.UserId,
						email = email
					};
					await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserEmail, Variables);
				}
				catch (Exception exception)
				{
					userUpdated = false;
					Log.WriteAudit("UpdateUser", $"Updating User Id: {parameters.UserId} Dn: {user.Dn} locally failed: {exception.Message}");
				}
			}
			return userUpdated;
		}

		// GET: api/<ValuesController>
		/// <summary>
		/// Change user password in specified Ldap
		/// </summary>
		/// <remarks>
		/// LdapId (required) &#xA;
		/// UserId (required) &#xA;
		/// OldPassword (required) &#xA;
		/// NewPassword (required) &#xA;
		/// </remarks>
		/// <param name="parameters">UserChangePasswordParameters</param>
		/// <returns>error message, empty if Ok</returns>
		[HttpPatch("EditPassword")]
		public async Task<ActionResult<string>> ChangePassword([FromBody] UserChangePasswordParameters parameters)
		{
			// the demo user (currently auditor) can't change his password
			if (User.IsInRole(Roles.Auditor))
				return Unauthorized();

			UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");

			string errorMsg = "";

			foreach (Ldap currentLdap in ldaps)
			{
				// if current Ldap is writable: Try to change password in current Ldap
				if ((currentLdap.Id == parameters.LdapId || parameters.LdapId == 0) && currentLdap.IsWritable())
				{
					bool passwordMustBeChanged = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = user.Dn }))[0].PasswordMustBeChanged;

					await Task.Run(async () =>
					{
						errorMsg = currentLdap.ChangePassword(user.Dn, parameters.OldPassword, parameters.NewPassword);
						if (errorMsg == "")
						{
							await UiUserHandler.UpdateUserPasswordChanged(apiConnection, user.Dn);
						}
					});
				}
			}

			// Return status and result
			return errorMsg;
		}

		// GET: api/<ValuesController>
		/// <summary>
		/// Reset user password in specified Ldap
		/// </summary>
		/// <remarks>
		/// LdapId (required) &#xA;
		/// UserId (required) &#xA;
		/// NewPassword (required) &#xA;
		/// </remarks>
		/// <param name="parameters">UserResetPasswordParameters</param>
		/// <returns>error message or Ok</returns>
		[HttpPatch("ResetPassword")]
		[Authorize(Roles = $"{Roles.Admin}")]
		public async Task<ActionResult<string>> ResetPassword([FromBody] UserResetPasswordParameters parameters)
		{
			UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");
			string errorMsg = "";

			foreach (Ldap currentLdap in ldaps)
			{
				// if current Ldap is internal: Try to update user password in current Ldap
				if ((currentLdap.Id == parameters.LdapId || parameters.LdapId == 0) && currentLdap.IsWritable())
				{
					await Task.Run(async () =>
					{
						errorMsg = currentLdap.SetPassword(user.Dn, parameters.NewPassword);
						if (errorMsg == "")
						{
							List<string> roles = [.. currentLdap.GetRoles([user.Dn])]; // TODO: Group roles are not included
							// the demo user (currently auditor) can't be forced to change password as he is not allowed to do it. Everyone else has to change it though
							bool passwordMustBeChanged = !roles.Contains(Roles.Auditor);
							await UiUserHandler.UpdateUserPasswordChanged(apiConnection, user.Dn, passwordMustBeChanged);
						}
					});
				}
			}

			// Return status and result
			return errorMsg == "" ? Ok() : Problem(errorMsg);
		}

		// DELETE api/<ValuesController>/5
		/// <summary>
		/// Remove user from all entries (groups, roles)
		/// </summary>
		/// <remarks>
		/// UserId (required) &#xA;
		/// </remarks>
		/// <param name="parameters">UserDeleteAllEntriesParameters</param>
		/// <returns>true if user removed from all entries</returns>
		[HttpDelete("AllGroupsAndRoles")]
		[Authorize(Roles = $"{Roles.Admin}")]
		public async Task<bool> DeleteAllGroupsAndRoles([FromBody] UserDeleteAllEntriesParameters parameters)
		{
			UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");

			bool userRemoved = false;
			List<Task> ldapRoleRequests = [];

			foreach (Ldap currentLdap in ldaps)
			{
				// Try to remove user from all roles and groups in current Ldap
				if (currentLdap.IsWritable() && (currentLdap.HasRoleHandling() || currentLdap.HasGroupHandling()))
				{
					ldapRoleRequests.Add(Task.Run(() =>
					{
						if (currentLdap.RemoveUserFromAllEntries(user.Dn))
						{
							userRemoved = true;
						}
					}));
				}
			}

			await Task.WhenAll(ldapRoleRequests);

			// Return status and result
			return userRemoved;
		}

		// DELETE api/<ValuesController>/5
		/// <summary>
		/// Delete user from specified Ldap
		/// </summary>
		/// <remarks>
		/// LdapId (required) &#xA;
		/// UserId (required) &#xA;
		/// </remarks>
		/// <param name="parameters">UserDeleteParameters</param>
		/// <returns>true if user deleted</returns>
		[HttpDelete]
		[Authorize(Roles = $"{Roles.Admin}")]
		public async Task<bool> Delete([FromBody] UserDeleteParameters parameters)
		{
			UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");
			bool userDeleted = false;

			foreach (Ldap currentLdap in ldaps)
			{
				// Try to delete user in current Ldap
				if (currentLdap.Id == parameters.LdapId || parameters.LdapId == 0)
				{
					if (currentLdap.IsWritable())
					{
						await Task.Run(() =>
						{
							if (currentLdap.DeleteUser(user.Dn))
							{
								userDeleted = true;
								Log.WriteAudit("DeleteUser", $"User {user.Dn} deleted from Ldap Id: {parameters.LdapId} Name: {currentLdap.Host()}");
							}
						});
					}
					else
					{
						// not allowed to delete user in Ldap
						userDeleted = true;
					}
				}
			}
			if (userDeleted)
			{
				// Try to delete user in local db
				try
				{
					var Variables = new { id = user.DbId };
					await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.deleteUser, Variables);
				}
				catch (Exception exception)
				{
					userDeleted = false;
					Log.WriteAudit("DeleteUser", $"Deleting User Id: {parameters.UserId} Dn: {user.Dn} locally failed: {exception.Message}");
				}
			}
			return userDeleted;
		}

		private async Task<UiUser?> resolveUser(int id)
		{
			List<UiUser> uiUsers;
			try
			{
				uiUsers = [.. (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUsers))];
				return uiUsers.FirstOrDefault(x => x.DbId == id);
			}
			catch (Exception exception)
			{
				Log.WriteAudit("UpdateUser", $"Could not get users: {exception.Message}");
				return null;
			}
		}
	}
}

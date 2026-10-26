using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        private readonly UserSessionRevocation sessionRevocation;
        private readonly GlobalConfig globalConfig;
        private readonly string WrongUserId = "Wrong UserId";
        private const string kWrongLdapId = "The LDAP connection does not belong to the user.";

        /// <summary>
        /// Constructor needing ldap list, connection and the global configuration holding the maximum page size
        /// </summary>
        public UserController(List<Ldap> ldaps, ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            sessionRevocation = new UserSessionRevocation(apiConnection, ldaps);
        }

        // GET: api/<UserController>
        /// <summary>
        /// Get all locally known users.
        /// </summary>
        /// <remarks>
        /// Results are ordered by user id, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> users (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further users follow.
        /// The query parameters <c>limit</c> and <c>offset</c> select the page.
        /// </remarks>
        /// <param name="paging">Optional query parameters <c>limit</c> and <c>offset</c>.</param>
        /// <returns>List of all locally known users</returns>
        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<UserGetReturnParameters>>> Get([FromQuery] PagingParameters paging)
        {
            int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
            if (!ListPaging.TryValidate(paging, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

            List<UiUser> users = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUsers)).ToList();
            IEnumerable<UserGetReturnParameters> orderedUsers = users
                .Where(user => user.DbId != 0)
                .OrderBy(user => user.DbId)
                .Select(user => user.ToApiParams());
            return ListPaging.TakePage(HttpContext, orderedUsers, paging, maxLimit);
        }

        // GET api/<ValuesController>/5
        /// <summary>
        /// Search user in specified Ldap
        /// </summary>
        /// <remarks>
        /// LdapId (required) &#xA;
        /// SearchPattern (optional) &#xA;
        /// Limit (optional), Offset (optional) &#xA;
        /// Results are ordered by user dn, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> users (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further users follow.
        /// </remarks>
        /// <param name="parameters">LdapUserGetParameters</param>
        /// <returns>List of users</returns>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<LdapUserGetReturnParameters>>> Get([FromBody] LdapUserGetParameters parameters)
        {
            int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
            if (!ListPaging.TryValidate(parameters, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

            List<LdapUserGetReturnParameters> allUsers = [];

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.Id == parameters.LdapId)
                {
                    await Task.Run(async () =>
                    {
                        // Get all users from current Ldap
                        allUsers = await currentLdap.GetAllUsers(parameters.SearchPattern);
                    });
                }
            }

            // Return status and result
            IEnumerable<LdapUserGetReturnParameters> orderedUsers = allUsers
                .OrderBy(user => user.UserDn, StringComparer.OrdinalIgnoreCase)
                .ThenBy(user => user.UserDn, StringComparer.Ordinal);
            return ListPaging.TakePage(HttpContext, orderedUsers, parameters, maxLimit);
        }

        // POST api/<ValuesController>
        /// <summary>
        /// Add user to specified Ldap
        /// </summary>
        /// <remarks>
        /// LdapId (optional; required if more than one LDAP connection is writable): writable LDAP connection to create the user in. The dn has to lie within its paths. &#xA;
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

            int userId = 0;
            // The local user belongs to the directory that holds the account: the dn alone does not
            // identify it, as the same dn may exist in another LDAP connection (SEC-11).
            Ldap? targetLdap = SelectLdapForNewUser(parameters);
            bool userAdded = targetLdap != null && await targetLdap.AddUser(parameters.UserDn, parameters.Password, email);
            if (userAdded)
            {
                Log.WriteAudit("AddUser", $"user {parameters.UserDn} successfully added to Ldap Id: {targetLdap!.Id} Name: {targetLdap.Host()}");
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
                        ldapConnectionId = targetLdap!.Id
                    };
                    ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(AuthQueries.upsertUiUser, Variables)).ReturnIds;
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
        /// LdapId (optional): LDAP connection of the user. The operation always applies to the LDAP connection of the local user; another id is rejected. &#xA;
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
            UiUser user = await ResolveUser(parameters.UserId) ?? throw new ArgumentException(WrongUserId);
            bool userUpdated = false;

            Ldap? userLdap = GetLdapOfUser(user, parameters.LdapId, "UpdateUser");
            if (userLdap != null && userLdap.IsWritable() && await userLdap.UpdateUser(user.Dn, email))
            {
                userUpdated = true;
                Log.WriteAudit("UpdateUser", $"User {user.Dn} updated in Ldap Id: {userLdap.Id} Name: {userLdap.Host()}");
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
        /// LdapId (optional): LDAP connection of the user. The operation always applies to the LDAP connection of the local user; another id is rejected. &#xA;
        /// UserId (required) &#xA;
        /// OldPassword (required) &#xA;
        /// NewPassword (required) &#xA;
        /// </remarks>
        /// <param name="parameters">UserChangePasswordParameters</param>
        /// <returns>error message, empty if Ok</returns>
        [HttpPatch("EditPassword")]
        [Authorize]
        public async Task<ActionResult<string>> ChangePassword([FromBody] UserChangePasswordParameters parameters)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            // the demo user (currently auditor) can't change his password
            if (CallerCanUseRole(User, parameters.ExecutionMode, Roles.Auditor))
                return Unauthorized();

            if (!CallerCanChangePassword(User, parameters.UserId))
            {
                return Forbid();
            }

            UiUser user = await ResolveUser(parameters.UserId) ?? throw new ArgumentException(WrongUserId);

            // the directory is the one of the local user, not one named by the caller (GHSA-695m-f6mv-9g72)
            Ldap? userLdap = GetLdapOfUser(user, parameters.LdapId, "ChangePassword");
            if (userLdap == null)
            {
                return BadRequest(kWrongLdapId);
            }

            string errorMsg = "";
            if (userLdap.IsWritable())
            {
                errorMsg = await userLdap.ChangePassword(user.Dn, parameters.OldPassword, parameters.NewPassword);
                if (errorMsg == "")
                {
                    await UiUserHandler.UpdateUserPasswordChanged(apiConnection, user.DbId);
                }
            }

            // Return status and result
            return errorMsg;
        }

        /// <summary>
        /// Checks whether the authenticated caller may change the requested user's password.
        /// </summary>
        private static bool CallerCanChangePassword(ClaimsPrincipal caller, int targetUserId)
        {
            if (caller.IsInRole(Roles.Admin))
            {
                return true;
            }

            string? userIdClaim = caller.FindFirstValue("x-hasura-user-id");
            return int.TryParse(userIdClaim, out int callerUserId) && callerUserId == targetUserId;
        }
        private static bool CallerCanUseRole(ClaimsPrincipal user, string executionMode, string role)
        {
            return ExecutionModeHelper.HasAnyRoleInExecutionMode(ExecutionModeHelper.GetUserRoles(user), executionMode, [role]);
        }

        // GET: api/<ValuesController>
        /// <summary>
        /// Reset user password in specified Ldap
        /// </summary>
        /// <remarks>
        /// LdapId (optional): LDAP connection of the user. The operation always applies to the LDAP connection of the local user; another id is rejected. &#xA;
        /// UserId (required) &#xA;
        /// NewPassword (required) &#xA;
        /// </remarks>
        /// <param name="parameters">UserResetPasswordParameters</param>
        /// <returns>error message or Ok</returns>
        [HttpPatch("ResetPassword")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<ActionResult<string>> ResetPassword([FromBody] UserResetPasswordParameters parameters)
        {
            UiUser user = await ResolveUser(parameters.UserId) ?? throw new ArgumentException(WrongUserId);
            Ldap? userLdap = GetLdapOfUser(user, parameters.LdapId, "ResetPassword");
            if (userLdap == null)
            {
                return BadRequest(kWrongLdapId);
            }

            string errorMsg = "";
            if (userLdap.IsWritable())
            {
                errorMsg = await userLdap.SetPassword(user.Dn, parameters.NewPassword);
                if (errorMsg == "")
                {
                    List<string> userDnList = [user.Dn];
                    List<string> roles = [.. await userLdap.GetRoles(userDnList)]; // TODO: Group roles are not included
                                                                                   // the demo user (currently auditor) can't be forced to change password as he is not allowed to do it. Everyone else has to change it though
                    bool passwordMustBeChanged = !roles.Contains(Roles.Auditor);
                    await UiUserHandler.UpdateUserPasswordChanged(apiConnection, user.DbId, passwordMustBeChanged);
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
            UiUser user = await ResolveUser(parameters.UserId) ?? throw new ArgumentException(WrongUserId);

            bool userRemoved = false;
            List<Task> ldapRoleRequests = [];

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to remove user from all roles and groups in current Ldap
                if (currentLdap.IsWritable() && (currentLdap.HasRoleHandling() || currentLdap.HasGroupHandling()))
                {
                    ldapRoleRequests.Add(Task.Run(async () =>
                    {
                        if (await currentLdap.RemoveUserFromAllEntries(user.Dn))
                        {
                            userRemoved = true;
                        }
                    }));
                }
            }

            await Task.WhenAll(ldapRoleRequests);

            if (userRemoved)
            {
                await sessionRevocation.RevokeSessionsOfUser(user.DbId, $"Removed user {user.Dn} from all groups and roles");
            }

            // Return status and result
            return userRemoved;
        }

        // DELETE api/<ValuesController>/5
        /// <summary>
        /// Delete user from specified Ldap
        /// </summary>
        /// <remarks>
        /// LdapId (optional): LDAP connection of the user. The operation always applies to the LDAP connection of the local user; another id is rejected. &#xA;
        /// UserId (required) &#xA;
        /// </remarks>
        /// <param name="parameters">UserDeleteParameters</param>
        /// <returns>true if user deleted</returns>
        [HttpDelete]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> Delete([FromBody] UserDeleteParameters parameters)
        {
            UiUser user = await ResolveUser(parameters.UserId) ?? throw new ArgumentException(WrongUserId);
            int userLdapId = user.LdapConnection?.Id ?? 0;
            if (parameters.LdapId != 0 && parameters.LdapId != userLdapId)
            {
                Log.WriteWarning("DeleteUser", $"Refused to delete user {user.Dn}: ldap id {parameters.LdapId} is not the ldap id {userLdapId} of local user {user.DbId}.");
                return false;
            }

            bool userDeleted;
            Ldap? userLdap = ldaps.FirstOrDefault(ldap => ldap.Id == userLdapId);
            if (userLdap != null && userLdap.IsWritable())
            {
                userDeleted = await DeleteDirectoryEntry(userLdap, user);
            }
            else
            {
                // not allowed to delete user in Ldap, or its directory is gone: only the local user is deleted
                userDeleted = true;
            }
            if (userDeleted)
            {
                // Try to delete user in local db; its refresh tokens are deleted with it, which ends its sessions
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

        /// <summary>
        /// Deletes the directory entry of a local user, but only while its dn still names the directory object the local
        /// user is bound to. When the dn was handed to another account, that account is preserved and only the local
        /// user is deleted, so the other account gets a new local user on its next login (GHSA-695m-f6mv-9g72).
        /// </summary>
        /// <param name="userLdap">Writable LDAP of the local user.</param>
        /// <param name="user">The local user.</param>
        /// <returns>true if the local user may be deleted.</returns>
        private async Task<bool> DeleteDirectoryEntry(Ldap userLdap, UiUser user)
        {
            string? directoryObjectId;
            try
            {
                UiUser[] storedUsers = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = user.DbId });
                directoryObjectId = storedUsers.FirstOrDefault()?.DirectoryObjectId;
            }
            catch (Exception exception)
            {
                Log.WriteAudit("DeleteUser", $"Could not read the directory binding of user Id: {user.DbId} Dn: {user.Dn}: {exception.Message}");
                return false;
            }

            LdapUserDeletion deletion = await userLdap.DeleteUser(user.Dn, directoryObjectId);
            if (deletion == LdapUserDeletion.Deleted)
            {
                Log.WriteAudit("DeleteUser", $"User {user.Dn} deleted from Ldap Id: {userLdap.Id} Name: {userLdap.Host()}");
            }
            else if (deletion == LdapUserDeletion.NotBound)
            {
                Log.WriteAudit("DeleteUser", $"Directory entry {user.Dn} in Ldap Id: {userLdap.Id} is not the directory object of local user Id: {user.DbId} " +
                    "and is kept. Only the local user is deleted.");
            }
            return deletion != LdapUserDeletion.Failed;
        }

        /// <summary>
        /// Determines the LDAP connection of a local user. A dn names a subject only inside its directory, so an
        /// operation on a local user may only reach the directory that the local user belongs to (GHSA-695m-f6mv-9g72).
        /// </summary>
        /// <param name="user">The local user.</param>
        /// <param name="requestedLdapId">LDAP connection named by the caller; 0 to use the one of the local user.</param>
        /// <param name="action">Name of the operation, for the log.</param>
        /// <returns>The connected LDAP of the local user, or null if the local user has none or the caller named another one.</returns>
        private Ldap? GetLdapOfUser(UiUser user, int requestedLdapId, string action)
        {
            int userLdapId = user.LdapConnection?.Id ?? 0;
            Ldap? userLdap = ldaps.FirstOrDefault(ldap => ldap.Id == userLdapId);
            if (userLdapId <= 0 || userLdap == null || (requestedLdapId != 0 && requestedLdapId != userLdapId))
            {
                Log.WriteWarning(action, $"Refused for user {user.Dn}: ldap id {requestedLdapId} does not name the connected ldap id {userLdapId} of local user {user.DbId}.");
                return null;
            }
            return userLdap;
        }

        /// <summary>
        /// Selects the writable LDAP connection to create a user in: the requested one, or the only writable one if
        /// none is requested. The dn has to lie within the paths of that connection, see <see cref="LdapDnNamespace"/>.
        /// </summary>
        /// <returns>The LDAP connection, or null if none or several are possible or the dn belongs to another connection.</returns>
        private Ldap? SelectLdapForNewUser(UserAddParameters parameters)
        {
            List<Ldap> writableLdaps = ldaps.Where(ldap => ldap.IsWritable()).ToList();
            Ldap? targetLdap;
            if (parameters.LdapId != 0)
            {
                targetLdap = writableLdaps.FirstOrDefault(ldap => ldap.Id == parameters.LdapId);
            }
            else
            {
                targetLdap = writableLdaps.Count == 1 ? writableLdaps[0] : null;
            }
            if (targetLdap == null)
            {
                Log.WriteWarning("AddUser", $"Refused to add user {parameters.UserDn}: ldap id {parameters.LdapId} does not name exactly one writable ldap.");
                return null;
            }
            if (!LdapDnNamespace.IsOwnedBy(ldaps, targetLdap.Id, parameters.UserDn))
            {
                Log.WriteWarning("AddUser", $"Refused to add user {parameters.UserDn} to ldap id {targetLdap.Id}: the dn is not within the paths of this connection or is also claimed by another one.");
                return null;
            }
            return targetLdap;
        }

        private async Task<UiUser?> ResolveUser(int id)
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

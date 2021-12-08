﻿using FWO.Api.Data;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Middleware.RequestParameters;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly List<Ldap> ldaps;
        private readonly APIConnection apiConnection;

        public UserController(List<Ldap> ldaps, APIConnection apiConnection)
        {
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        // GET: api/<UserController>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<List<UserGetReturnParameters>> Get()
        {
            List<UiUser> users = (await apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUsers)).ToList();
            List<UserGetReturnParameters> userList = new List<UserGetReturnParameters>();
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
        [HttpPost("Get")]
        [Authorize(Roles = "admin, auditor")]
        public async Task<List<KeyValuePair<string, string>>> Get([FromBody] UserGetParameters parameters)
        {
            List<KeyValuePair<string, string>> allUsers = new List<KeyValuePair<string, string>>();

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
        [HttpPost]
        [Authorize(Roles = "admin")]
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
                        if(currentLdap.AddUser(parameters.UserDn, parameters.Password, email))
                        {
                            userAdded = true;
                            Log.WriteAudit("AddUser", $"user {parameters.UserDn} successfully added to Ldap Id: {parameters.LdapId} Name: {currentLdap.Host()}");
                        }
                    });
                }
            }
            if(userAdded)
            {
                // Try to add user to local db
                try
                {
                    var Variables = new
                    {
                        uuid = parameters.UserDn,
                        uiuser_username = (new FWO.Api.Data.DistName(parameters.UserDn)).UserName,
                        email = email,
                        tenant = parameters.TenantId,
                        passwordMustBeChanged = parameters.PwChangeRequired,
                        ldapConnectionId = (parameters.LdapId != 0 ? parameters.LdapId : (int?)null)
                    };
                    ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.ApiClient.Queries.AuthQueries.addUser, Variables)).ReturnIds;
                    if(returnIds != null)
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
        [HttpPut]
        [Authorize(Roles = "admin")]
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
                        if(currentLdap.UpdateUser(user.Dn, email))
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
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.updateUserEmail, Variables);
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
        [HttpPatch("EditPassword")]
        public async Task<ActionResult<string>> ChangePassword([FromBody] UserChangePasswordParameters parameters)
        {
            // the demo user (currently auditor) can't change his password
            if (User.IsInRole("auditor"))
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
        [HttpPatch("ResetPassword")]
        [Authorize(Roles = "admin")]
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
                            List<string> roles = currentLdap.GetRoles(new List<string>() { user.Dn }).ToList();
                            // the demo user (currently auditor) can't be forced to change password as he is not allowed to do it. Everyone else has to change it though
                            bool passwordMustBeChanged = !roles.Contains("auditor"); 
                            await UiUserHandler.UpdateUserPasswordChanged(apiConnection, user.Dn, passwordMustBeChanged);
                        }
                    });
                }
            }

            // Return status and result
            return errorMsg == "" ? Ok() : Problem(errorMsg);
        }

        // DELETE api/<ValuesController>/5
        [HttpDelete("AllGroupsAndRoles")]
        [Authorize(Roles = "admin")]
        public async Task<bool> DeleteAllGroupsAndRoles([FromBody] UserDeleteAllEntriesParameters parameters)
        {
            UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");

            bool userRemoved = false;
            List<Task> ldapRoleRequests = new List<Task>();

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
        [HttpDelete]
        [Authorize(Roles = "admin")]
        public async Task<bool> Delete([FromBody] UserDeleteParameters parameters)
        {
            UiUser user = await resolveUser(parameters.UserId) ?? throw new Exception("Wrong UserId");
            bool userDeleted = false;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete user in current Ldap
                if ((currentLdap.Id == parameters.LdapId || parameters.LdapId == 0) && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        if(currentLdap.DeleteUser(user.Dn))
                        {
                            userDeleted = true;
                            Log.WriteAudit("DeleteUser", $"User {user.Dn} deleted from Ldap Id: {parameters.LdapId} Name: {currentLdap.Host()}");
                        }
                    });
                }
            }
            if (userDeleted)
            {
                // Try to delete user in local db
                try
                {
                    var Variables = new { id = user.DbId };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.deleteUser, Variables);
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
                uiUsers = (await apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUsers)).ToList();
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

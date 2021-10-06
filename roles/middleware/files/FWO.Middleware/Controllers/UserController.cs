using FWO.Api.Data;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

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

        // GET api/<ValuesController>/5
        [HttpGet("{ldapHostname}")]
        //[Authorize(Roles = "admin")]
        public async Task<List<KeyValuePair<string, string>>> Get(string ldapHostname, string searchPattern)
        {
            List<KeyValuePair<string, string>> allUsers = new List<KeyValuePair<string, string>>();

            foreach (Ldap currentLdap in ldaps)
            {
                if (currentLdap.Host() == ldapHostname)
                {
                    await Task.Run(() =>
                    {
                        // Get all users from current Ldap
                        allUsers = currentLdap.GetAllUsers(searchPattern);
                    });
                }
            }

            // Return status and result
            return allUsers;
        }

        public class AddParameters
        {
            public string password { get; set; }
            public string email { get; set; }
        }

        // POST api/<ValuesController>
        [HttpPost("{ldapHostname}/{userDn}")]
        // [Authorize(Roles = "admin")]
        public async Task<bool> Add(string ldapHostname, string userDn, [FromBody] AddParameters parameters)
        {
            string password = parameters.password;
            string email = parameters.email;

            bool userAdded = false;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add user to current Ldap
                if (currentLdap.Host() == ldapHostname && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        userAdded = currentLdap.AddUser(userDn, password, email);
                        if (userAdded) Log.WriteAudit("AddUser", $"user {userDn} successfully added to {ldapHostname}");
                    });
                }
            }

            return userAdded;
        }

        // PUT api/<ValuesController>/5
        [HttpPut("{ldapHostname}/{userDn}")]
        [Authorize(Roles = "admin")]
        public async Task<bool> Change(string ldapHostname, string userDn, string email)
        {
            bool userUpdated = false;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to update user in current Ldap
                if (currentLdap.Host() == ldapHostname && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        userUpdated = currentLdap.UpdateUser(userDn, email);
                        if (userUpdated) Log.WriteAudit("UpdateUser", $"User {userDn} updated in {ldapHostname}");
                    });
                }
            }

            return userUpdated;
        }


        // GET: api/<ValuesController>
        [HttpPatch("{ldapHostname}/{userDn}/password")]
        //[Authorize(Roles = "admin, reporter, reporter-viewall, recertifier")]
        public async Task<ActionResult<string>> ChangePassword(string ldapHostname, string userDn, string oldPassword, string newPassword)
        {
            // the demo user (currently auditor) can't change his password
            if (User.IsInRole("auditor"))
                return Unauthorized();

            string errorMsg = "";

            foreach (Ldap currentLdap in ldaps)
            {
                // if current Ldap is writable: Try to change password in current Ldap
                if (currentLdap.Host() == ldapHostname && currentLdap.IsWritable())
                {
                    bool passwordMustBeChanged = (await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { userDn = userDn }))[0].PasswordMustBeChanged;

                    await Task.Run(async () =>
                    {
                        errorMsg = currentLdap.ChangePassword(userDn, oldPassword, newPassword);
                        if (errorMsg == "")
                        {
                            await UiUserHandler.UpdateUserPasswordChanged(apiConnection, userDn);
                        }
                    });
                }
            }

            // Return status and result
            return errorMsg;
        }

        // GET: api/<ValuesController>
        [HttpOptions("{ldapHostname}/{userDn}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<string>> ResetPassword(string ldapHostname, string userDn, string password)
        {
            string errorMsg = "";

            foreach (Ldap currentLdap in ldaps)
            {
                // if current Ldap is internal: Try to update user password in current Ldap
                if (currentLdap.Host() == ldapHostname && currentLdap.IsWritable())
                {
                    await Task.Run(async () =>
                    {
                        errorMsg = currentLdap.SetPassword(userDn, password);
                        if (errorMsg == "")
                        {
                            List<string> roles = currentLdap.GetRoles(new List<string>() { userDn }).ToList();
                            // the demo user (currently auditor) can't be forced to change password as he is not allowed to do it. Everyone else has to change it though
                            bool passwordMustBeChanged = !roles.Contains("auditor"); 
                            await UiUserHandler.UpdateUserPasswordChanged(apiConnection, userDn, passwordMustBeChanged);
                        }
                    });
                }
            }

            // Return status and result
            return errorMsg == "" ? Ok() : Problem(errorMsg);
        }

        // DELETE api/<ValuesController>/5
        [HttpDelete("{userDn}")]
        [Authorize(Roles = "admin")]
        public async Task<bool> Delete(string ldapHostname, string userDn)
        {
            bool userDeleted = false;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete user in current Ldap
                if (currentLdap.Host() == ldapHostname && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        userDeleted = currentLdap.DeleteUser(userDn);
                        if (userDeleted) Log.WriteAudit("DeleteUser", $"User {userDn} deleted from {ldapHostname}");
                    });
                }
            }

            return userDeleted;
        }
    }
}

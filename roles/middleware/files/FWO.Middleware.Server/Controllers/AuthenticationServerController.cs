﻿using FWO.Api.Client.Queries;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FWO.Middleware.RequestParameters;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Controllers
{
    // [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationServerController : ControllerBase
    {
        private List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;

        public AuthenticationServerController(ApiConnection apiConnection, List<Ldap> ldaps)
        {
            this.apiConnection = apiConnection;
            this.ldaps = ldaps;
        }

        /// <summary>
        /// Test connection to the specified Ldap.
        /// </summary>
        /// <remarks>
        /// Address (required) &#xA;
        /// Port (required) &#xA; 
        /// SearchUser AND SearchUserPassword (optional) - leads to test of search user binding &#xA;
        /// WriteUser AND WriterUserPassword (optional) - leads to test of write user binding
        /// </remarks>
        /// <param name="parameters">Ldap connection parameters</param>
        /// <returns></returns>
        [HttpGet("TestConnection")]
        [Authorize(Roles = "admin, auditor")]
        public ActionResult<string> TestConnection([FromBody] LdapGetUpdateParameters parameters)
        {
            try
            {
                Ldap ldapToTest = new Ldap(parameters);
                ldapToTest.TestConnection();
            }
            catch (Exception e)
            {
                Problem("Connection test failed: " + e.Message);
            }
            return Ok("Connection tested successfully");
        }

        // GET: api/<LdapController>
        [HttpGet]
        [Authorize(Roles = "admin, auditor")]
        public async Task<List<LdapGetUpdateParameters>> Get()
        {
            UiLdapConnection[] ldapConnections = (await apiConnection.SendQueryAsync<UiLdapConnection[]>(AuthQueries.getAllLdapConnections));
            List<LdapGetUpdateParameters> ldapList = new List<LdapGetUpdateParameters>();
            foreach (UiLdapConnection conn in ldapConnections)
            {
                ldapList.Add(conn.ToApiParams());
            }
            return ldapList;
        }

        // POST api/<LdapController>/5
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<int> PostAsync([FromBody] LdapAddParameters ldapData)//, [FromHeader] string bearer)
        {
            // Add ldap to DB and to middleware ldap list
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(AuthQueries.newLdapConnection, ldapData)).ReturnIds;
            int ldapId = 0;
            if (returnIds != null)
            {
                ldapId = returnIds[0].NewId;
                ldaps.Add(new Ldap(new LdapGetUpdateParameters (ldapData, ldapId) {}));
                Log.WriteAudit("AddLdap", $"LDAP server {ldapData.Address}:{ldapData.Port} successfully added");
            }

            // Return status and result
            return ldapId;
        }

        // PUT api/<LdapController>/Update/5
        [HttpPut]
        [Authorize(Roles = "admin")]
        public async Task<int> Update([FromBody] LdapGetUpdateParameters ldapData)
        {
            // Update ldap in DB and in middleware ldap list
            int ldapId = (await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateLdapConnection, ldapData)).UpdatedId;
            if (ldapId == ldapData.Id)
            {
                ldaps[ldaps.FindIndex(x => x.Id == ldapId)] = new Ldap(ldapData);
                Log.WriteAudit("UpdateLdap", $"LDAP server {ldapData.Address}:{ldapData.Port} successfully updated");
            }

            // Return status and result
            return ldapId;
        }

        // DELETE api/<LdapController>/5
        [HttpDelete]
        [Authorize(Roles = "admin")]
        public async Task<int> Delete([FromBody] LdapDeleteParameters ldapData)
        {
            // Delete ldap in DB and in middleware ldap list
            int delId = (await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.deleteLdapConnection, ldapData)).DeletedId;
            if (delId == ldapData.Id)
            {
                ldaps.RemoveAll(x => x.Id == delId);
                Log.WriteAudit("DeleteLdap", $"LDAP server {ldapData.Id} successfully deleted");
            }

            // Return status and result
            return delId;
        }
    }
}

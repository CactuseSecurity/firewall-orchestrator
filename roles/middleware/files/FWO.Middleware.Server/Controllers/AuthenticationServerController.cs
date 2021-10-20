using FWO.ApiClient.Queries;
using FWO.ApiClient;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using FWO.Middleware.RequestParameters;
using FWO.Api.Data;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Controllers
{
    // [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationServerController : ControllerBase
    {
        private List<Ldap> ldaps = new List<Ldap>();
        private readonly string apiUri;

        public AuthenticationServerController(string apiUri, List<Ldap> ldaps)
        {
            this.apiUri = apiUri;
            this.ldaps = ldaps;
        }

        // GET: api/<LdapController>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<LdapController>/5
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public string Get(int id)
        {
            return "value";
        }

        // PUT api/<LdapController>/5
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<UiLdapConnection> PostAsync([FromBody] LdapAddParameters ldapData)//, [FromHeader] string bearer)
        {
            // Create Api connection with given jwt
            APIConnection apiConnection = new APIConnection(apiUri, "bearer");

            // Add ldap to DB and to middleware ldap list
            Ldap newLdap = (await apiConnection.SendQueryAsync<Ldap[]>(AuthQueries.newLdapConnection, ldapData))[0];
            ldaps.Add(newLdap);

            Log.WriteAudit("AddLdap", $"LDAP server {ldapData.Address}:{ldapData.Port} successfully added");

            // Return status and result
            return newLdap;
        }

        // DELETE api/<LdapController>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public void Delete(int id)
        {
        }
    }
}

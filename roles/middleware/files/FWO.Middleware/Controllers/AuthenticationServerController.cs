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

        public class AddLdapParameters
        {
            public string address {  get; set; }
            public string port { get; set; } = "636";
            public string searchUser { get; set; }
            public string tls { get; set; }
            public string tenantLevel { get; set; }
            public string searchUserPwd { get; set; }
            public string searchpathForUsers { get; set; }
            public string searchpathForRoles { get; set; }
            public string writeUser { get; set; }
            public string writeUserPwd { get; set; }
            public string tenantId { get; set; }
        }

        // PUT api/<LdapController>/5
        [HttpPost]
        //[Authorize(Roles = "admin")]
        public async Task<Ldap> PostAsync([FromBody] AddLdapParameters ldapData)//, [FromHeader] string bearer)
        {
            // Create Api connection with given jwt
            APIConnection apiConnection = new APIConnection(apiUri, "bearer");

            // Add ldap to DB and to middleware ldap list
            Ldap newLdap = (await apiConnection.SendQueryAsync<Ldap[]>(AuthQueries.newLdapConnection, ldapData))[0];
            ldaps.Add(newLdap);

            Log.WriteAudit("AddLdap", $"LDAP server {ldapData.address}:{ldapData.port} successfully added");

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

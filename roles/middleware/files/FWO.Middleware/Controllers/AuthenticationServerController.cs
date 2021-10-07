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

        public class LdapAddParameters
        {
            [JsonPropertyName("address")]
            public string Address {  get; set; }

            [JsonPropertyName("port")]
            public string Port { get; set; } = "636";

            [JsonPropertyName("searchUser")]
            public string SearchUser { get; set; }

            [JsonPropertyName("tls")]
            public string Tls { get; set; }

            [JsonPropertyName("tenantLevel")]
            public string TenantLevel { get; set; }

            [JsonPropertyName("searchUserPwd")]
            public string SearchUserPwd { get; set; }

            [JsonPropertyName("searchpathForUsers")]
            public string SearchpathForUsers { get; set; }

            [JsonPropertyName("searchpathForRoles")]
            public string SearchpathForRoles { get; set; }

            [JsonPropertyName("writeUser")]
            public string WriteUser { get; set; }

            [JsonPropertyName("writeUserPwd")]
            public string WriteUserPwd { get; set; }

            [JsonPropertyName("tenantId")]
            public string TenantId { get; set; }
        }

        // PUT api/<LdapController>/5
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<Ldap> PostAsync([FromBody] LdapAddParameters ldapData)//, [FromHeader] string bearer)
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

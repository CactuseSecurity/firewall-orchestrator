using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller for Ldap administration
    /// </summary>
    // [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationServerController : ControllerBase
    {
        private List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;
        private const string kNamespaceErrorKey = "searchpathForUsers";
        private const string kServersErrorKey = "addresses";

        /// <summary>
        /// Constructor needing connection and ldap list
        /// </summary>
        public AuthenticationServerController(ApiConnection apiConnection, List<Ldap> ldaps)
        {
            this.apiConnection = apiConnection;
            this.ldaps = ldaps;
        }

        /// <summary>
        /// Test connection to the specified Ldap.
        /// </summary>
        /// <remarks>
        /// Addresses (required): names or ip addresses of the servers of the directory, tried in this order (1 to 10 distinct servers sharing the port) &#xA;
        /// Port (required) &#xA; 
        /// SearchUser AND SearchUserPassword (optional) - leads to test of search user binding &#xA;
        /// WriteUser AND WriterUserPassword (optional) - leads to test of write user binding &#xA;
        /// The passwords are taken as entered (clear text). Stored credentials are never
        /// used here, so that the connection test cannot be turned into a way of replaying
        /// them against a freely chosen server.
        /// This is a POST although it changes nothing: the parameters, including credentials
        /// entered for the test, travel in the body, and a GET carrying a body is handled
        /// inconsistently by proxies and http clients, while its url and body risk being
        /// cached or logged along the way.
        /// </remarks>
        /// <param name="parameters">Ldap connection parameters</param>
        /// <returns></returns>
        [HttpPost("TestConnection")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<ActionResult<string>> TestConnection([FromBody] LdapGetUpdateParameters parameters)
        {
            string? serverError = ValidateServers(parameters.Addresses);
            if (serverError != null)
            {
                return BadRequest(serverError);
            }
            try
            {
                Ldap ldapToTest = new Ldap(parameters);
                await ldapToTest.TestConnection();
            }
            catch (Exception e)
            {
                return Problem("Connection test failed: " + e.Message);
            }
            return Ok("Connection tested successfully");
        }

        // GET: api/<LdapController>
        /// <summary>
        /// Get all connected Ldaps.
        /// </summary>
        /// <returns>List of all connected Ldaps</returns>
        /// <remarks>
        /// The credentials of the connections are deliberately not part of the answer.
        /// </remarks>
        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        public async Task<List<LdapGetUpdateParameters>> Get()
        {
            UiLdapConnection[] ldapConnections = await apiConnection.SendQueryAsync<UiLdapConnection[]>(AuthQueries.getAllLdapConnectionsWithoutSecrets);
            List<LdapGetUpdateParameters> ldapList = [];
            foreach (UiLdapConnection conn in ldapConnections)
            {
                ldapList.Add(conn.ToApiParams());
            }
            return ldapList;
        }

        // POST api/<LdapController>/5
        /// <summary>
        /// Add Ldap connection
        /// </summary>
        /// <remarks>
        /// Name (optional) &#xA;
        /// Addresses (required): names or ip addresses of the servers of the directory, tried in this order (1 to 10 distinct servers sharing the port) &#xA;
        /// Port (required) &#xA;
        /// Type (required) &#xA;
        /// PatternLength (required) &#xA;
        /// SearchUser (required) &#xA;
        /// Tls (required) &#xA;
        /// TenantLevel (required) &#xA;
        /// SearchUserPwd (required) &#xA;
        /// SearchpathForUsers (required) &#xA;
        /// SearchpathForRoles (optional) &#xA;
        /// SearchpathForGroups (optional) &#xA;
        /// WritepathForGroups (optional) &#xA;
        /// WriteUser (optional) &#xA;
        /// WriteUserPwd (optional) &#xA;
        /// TenantId (optional) &#xA;
        /// GlobalTenantName (optional) &#xA;
        /// Active (required) &#xA;
        /// </remarks>
        /// <param name="ldapData">LdapAddParameters</param>
        /// <returns>Id of new ldap, 0 if no ldap could be added</returns>
        /// <response code="400">The server list is empty, too long or invalid, or the search or write paths of an active connection overlap those of another active connection.</response>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Admin}")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<int>> PostAsync([FromBody] LdapAddParameters ldapData)//, [FromHeader] string bearer)
        {
            ActionResult? validationError = ValidateConnection(new LdapConnectionBase(new LdapGetUpdateParameters(ldapData, 0)));
            if (validationError != null)
            {
                return validationError;
            }

            // Add ldap to DB and to middleware ldap list
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(AuthQueries.newLdapConnection, ldapData)).ReturnIds;
            int ldapId = 0;
            if (returnIds != null)
            {
                ldapId = returnIds[0].NewId;
                ldaps.Add(new Ldap(new LdapGetUpdateParameters(ldapData, ldapId) { }));
                Log.WriteAudit("AddLdap", $"LDAP server(s) {LdapConnectionBase.FormatHosts(ldapData.Addresses, ldapData.Port)} successfully added");
            }

            // Return status and result
            return ldapId;
        }

        // PUT api/<LdapController>/Update/5
        /// <summary>
        /// Update Ldap connection
        /// </summary>
        /// <remarks>
        /// Id (required) &#xA;
        /// Name (optional) &#xA;
        /// Addresses (required): names or ip addresses of the servers of the directory, tried in this order (1 to 10 distinct servers sharing the port) &#xA;
        /// Port (required) &#xA;
        /// Type (required) &#xA;
        /// PatternLength (required) &#xA;
        /// SearchUser (required) &#xA;
        /// Tls (required) &#xA;
        /// TenantLevel (required) &#xA;
        /// SearchUserPwd (required) &#xA;
        /// SearchpathForUsers (required) &#xA;
        /// SearchpathForRoles (optional) &#xA;
        /// SearchpathForGroups (optional) &#xA;
        /// WritepathForGroups (optional) &#xA;
        /// WriteUser (optional) &#xA;
        /// WriteUserPwd (optional) &#xA;
        /// TenantId (optional) &#xA;
        /// GlobalTenantName (optional) &#xA;
        /// Active (required) &#xA;
        /// </remarks>
        /// <param name="ldapData">LdapGetUpdateParameters</param>
        /// <returns>Id of updated ldap</returns>
        /// <response code="400">The server list is empty, too long or invalid, or the search or write paths of an active connection overlap those of another active connection.</response>
        [HttpPut]
        [Authorize(Roles = $"{Roles.Admin}")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<int>> Update([FromBody] LdapGetUpdateParameters ldapData)
        {
            ActionResult? validationError = ValidateConnection(new LdapConnectionBase(ldapData));
            if (validationError != null)
            {
                return validationError;
            }

            await KeepStoredPasswords(ldapData);

            // Update ldap in DB and in middleware ldap list
            int ldapId = (await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateLdapConnection, ldapData)).UpdatedId;
            if (ldapId == ldapData.Id)
            {
                ldaps[ldaps.FindIndex(x => x.Id == ldapId)] = new Ldap(ldapData);
                Log.WriteAudit("UpdateLdap", $"LDAP server(s) {LdapConnectionBase.FormatHosts(ldapData.Addresses, ldapData.Port)} successfully updated");
            }

            // Return status and result
            return ldapId;
        }

        /// <summary>
        /// Validates a connection before it is saved: it needs at least one and at most <see cref="LdapConnectionBase.kMaxServers"/>
        /// distinct servers, and an active connection must not have search or write paths that equal or contain a path
        /// of another active connection. Every dn has to belong to exactly one directory, as roles, groups, owner
        /// responsibles and workflow assignments refer to subjects by their bare dn (GHSA-695m-f6mv-9g72); replicas of
        /// one directory are therefore servers of one connection.
        /// </summary>
        /// <param name="candidate">Connection to be saved.</param>
        /// <returns>A validation problem listing all errors, or null if the connection is valid.</returns>
        private BadRequestObjectResult? ValidateConnection(LdapConnectionBase candidate)
        {
            Dictionary<string, string[]> errors = [];
            string? serverError = ValidateServers(candidate.Addresses);
            if (serverError != null)
            {
                errors.Add(kServersErrorKey, new List<string> { serverError }.ToArray());
            }

            List<Ldap> overlappingLdaps = LdapDnNamespace.GetOverlappingLdaps(candidate, ldaps);
            if (overlappingLdaps.Count > 0)
            {
                string overlapText = string.Join(", ", overlappingLdaps.Select(ldap => $"{ldap.Id} ({ldap.Host()})"));
                Log.WriteWarning("LDAP Settings", $"Refused to save LDAP connection {candidate.Host()}: its paths overlap those of active connection(s) {overlapText}.");
                errors.Add(kNamespaceErrorKey, new List<string>
                {
                    $"The search and write paths of an active LDAP connection must not equal or contain a path of another active connection. " +
                    $"Connect to replicas of one directory by listing their servers in one connection. Overlapping connection(s): {overlapText}."
                }.ToArray());
            }

            return errors.Count == 0 ? null : BadRequest(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
        }

        private static string? ValidateServers(List<string>? addresses)
        {
            if (addresses == null || addresses.Count == 0 || addresses.Count > LdapConnectionBase.kMaxServers)
            {
                return $"Addresses must list between 1 and {LdapConnectionBase.kMaxServers} servers.";
            }
            if (addresses.Any(address => string.IsNullOrWhiteSpace(address) || address.Length > LdapConnectionBase.kMaxServerLength || address.Any(char.IsWhiteSpace)))
            {
                return $"Every server must be a name or ip address of at most {LdapConnectionBase.kMaxServerLength} characters without blanks.";
            }
            if (addresses.Distinct(StringComparer.OrdinalIgnoreCase).Count() < addresses.Count)
            {
                return "Every server may be listed only once.";
            }
            return null;
        }

        /// <summary>
        /// Fills in the passwords that were left empty by the caller with the ones currently stored.
        /// </summary>
        /// <remarks>
        /// The credentials are not handed out by <see cref="Get"/>, so an update coming from the ui
        /// only carries a password if the administrator has entered a new one. Without this an edit
        /// of any other setting would wipe the stored credentials.
        /// </remarks>
        /// <param name="ldapData">Ldap connection parameters, updated in place</param>
        private async Task KeepStoredPasswords(LdapGetUpdateParameters ldapData)
        {
            bool searchPwdMissing = string.IsNullOrEmpty(ldapData.SearchUserPwd);
            bool writePwdMissing = string.IsNullOrEmpty(ldapData.WriteUserPwd) && !string.IsNullOrEmpty(ldapData.WriteUser);
            if (!searchPwdMissing && !writePwdMissing)
            {
                return;
            }

            List<UiLdapConnection> storedConnections = await apiConnection.SendQueryAsync<List<UiLdapConnection>>(
                AuthQueries.getLdapConnectionSecrets, new { id = ldapData.Id });
            UiLdapConnection? storedConnection = storedConnections.FirstOrDefault();
            if (storedConnection == null)
            {
                return;
            }

            if (searchPwdMissing)
            {
                ldapData.SearchUserPwd = storedConnection.SearchUserPwd;
            }
            if (writePwdMissing)
            {
                ldapData.WriteUserPwd = storedConnection.WriteUserPwd;
            }
        }

        // DELETE api/<LdapController>/5
        /// <summary>
        /// Delete Ldap connection
        /// </summary>
        /// <remarks>
        /// Id (required) &#xA;
        /// </remarks>
        /// <param name="ldapData">LdapDeleteParameters</param>
        /// <returns>Id of deleted ldap connection</returns>
        [HttpDelete]
        [Authorize(Roles = $"{Roles.Admin}")]
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

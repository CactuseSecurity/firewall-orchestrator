using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for role api
	/// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NormalizedConfigController(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection) : ControllerBase
    {
        private readonly ApiConnection apiConnection = apiConnection;
        private readonly JwtWriter jwtWriter = jwtWriter;
        private readonly List<Ldap> ldaps = ldaps;

        private ApiConnection? apiConnectionUserContext = null;
        private UserConfig? userConfig = null;

        /// <summary>
        /// Get NormalizedConfig
        /// </summary>
        /// <param name="parameters">NormalizedConfigGetParameters</param>
        /// <returns>NormalizedConfig as json string</returns>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Reporter}, {Roles.ReporterViewAll}, {Roles.Modeller}, {Roles.Recertifier}")]
        public async Task<string> Get([FromBody] NormalizedConfigGetParameters parameters)
        {
            try
            {
                if (!await InitUserEnvironment() || apiConnectionUserContext == null || userConfig == null)
                {
                    return "";  // todo: Error message?
                }

                NormalizedConfig normalizedConfig = await NormalizedConfigGenerator.Generate([.. parameters.ManagementIds], parameters.ConfigTime, apiConnectionUserContext, userConfig);
                return JsonConvert.SerializeObject(normalizedConfig);
            }
            catch (Exception exception)
            {
                Log.WriteError("Get NormalizedConfig", "Error while getting normalized config.", exception);
            }
            return "";
        }

        private async Task<bool> InitUserEnvironment()
        {
            AuthManager authManager = new(jwtWriter, ldaps, apiConnection);
            UiUser targetUser = new() { Name = User.FindFirstValue("unique_name") ?? "", Dn = User.FindFirstValue("x-hasura-uuid") ?? "" };
            string jwt = await authManager.AuthorizeUserAsync(targetUser, validatePassword: false);
            apiConnectionUserContext = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
            apiConnectionUserContext.SetProperRole(User, [Roles.Admin, Roles.Auditor, Roles.Reporter, Roles.ReporterViewAll, Roles.Modeller, Roles.Recertifier]);
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
            userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, targetUser.DbId);
            return true;
        }
    }
}

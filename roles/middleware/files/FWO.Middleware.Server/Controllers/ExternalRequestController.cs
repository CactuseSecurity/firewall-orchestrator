using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Middleware.RequestParameters;
using FWO.Basics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for role api
	/// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalRequestController : ControllerBase
    {
		private readonly ApiConnection apiConnection;

        /// <summary>
		/// Constructor needing jwt writer, ldap list and connection
		/// </summary>
		public ExternalRequestController(ApiConnection apiConnection)
		{
			this.apiConnection = apiConnection;
		}

        /// <summary>
        /// Add new ExternalRequest
        /// </summary>
        /// <remarks>
        /// TicketId (required) &#xA;
        /// </remarks>
        /// <param name="parameters">ExternalRequestParameters</param>
        /// <returns>true if external request could be added</returns>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Modeller}")]
        public async Task<bool> Post([FromBody] ExternalRequestParameters parameters)
        {
            if(parameters.TicketId > 0)
            {
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(GlobalConfig, apiConnection, new(){ Language = GlobalConst.kEnglish });

                ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
                return await extRequestHandler.SendFirstRequest(parameters.TicketId);
            }
            else
            {
                return false;
            }
        }
    }
}

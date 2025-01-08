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
        /// <param name="parameters">ExternalRequestAddParameters</param>
        /// <returns>true if external request could be added</returns>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Modeller}, {Roles.Admin}")]
        public async Task<bool> Post([FromBody] ExternalRequestAddParameters parameters)
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

        /// <summary>
        /// Patch ExternalRequest state
        /// </summary>
        /// <remarks>
        /// ExtRequestId (required) &#xA;
        /// TicketId (required) &#xA;
        /// TaskNumber (required) &#xA;
        /// ExtQueryVariables (optional) &#xA;
        /// ExtRequestState (required) &#xA;
        /// </remarks>
        /// <param name="parameters">ExternalRequestPatchStateParameters</param>
        /// <returns>true if external request state could be patched</returns>
		[HttpPatch("PatchState")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> Change([FromBody] ExternalRequestPatchStateParameters parameters)
        {
            if(parameters.ExtRequestId > 0)
            {
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(GlobalConfig, apiConnection, new(){ Language = GlobalConst.kEnglish });
                ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
                ExternalRequest extRequest = new()
                {
                    Id = parameters.ExtRequestId,
                    TicketId = parameters.TicketId,
                    TaskNumber = parameters.TaskNumber,
                    ExtQueryVariables = parameters.ExtQueryVariables,
                    ExtRequestState = parameters.ExtRequestState
                };
                return await extRequestHandler.PatchState(extRequest);
            }
            else
            {
                return false;
            }
        }
    }
}

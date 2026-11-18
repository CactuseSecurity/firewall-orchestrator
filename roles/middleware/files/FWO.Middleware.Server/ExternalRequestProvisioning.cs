using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Starts the provisioning of a ticket for the state action "Create external requests" through the external
    /// request handler of the middleware server.
    /// </summary>
    public sealed class ExternalRequestProvisioning : IExternalRequestProvisioning
    {
        /// <inheritdoc/>
        public async Task<bool> StartProvisioning(ApiConnection apiConnection, UserConfig userConfig, long ticketId, long? scopeReqTaskId, bool byButton = false)
        {
            using ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
            return await extRequestHandler.StartProvisioning(ticketId, scopeReqTaskId, null, byButton);
        }

        /// <inheritdoc/>
        public async Task<bool> ProvisionImplTask(ApiConnection apiConnection, UserConfig userConfig, long ticketId, long implTaskId, bool byButton = false)
        {
            using ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
            return await extRequestHandler.ProvisionImplTask(ticketId, implTaskId, byButton);
        }
    }
}

using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Ui.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Restores the current UI session and records the outcome.
    /// </summary>
    public sealed class UiSessionRestorer
    {
        private const string kLogCategory = "Token Refresh Coordinator";
        private readonly AuthenticationStateProvider authenticationProvider;
        private readonly ApiConnection apiConnection;
        private readonly MiddlewareClient middlewareClient;
        private readonly UserConfig userConfig;

        /// <summary>
        /// Creates a restorer for the current UI session.
        /// </summary>
        public UiSessionRestorer(AuthenticationStateProvider authenticationProvider, ApiConnection apiConnection,
            MiddlewareClient middlewareClient, UserConfig userConfig)
        {
            this.authenticationProvider = authenticationProvider;
            this.apiConnection = apiConnection;
            this.middlewareClient = middlewareClient;
            this.userConfig = userConfig;
        }

        /// <summary>
        /// Restores authentication from the stored tokens and audits whether it succeeded.
        /// </summary>
        public async Task RestoreAsync()
        {
            bool refreshSuccess = await ((AuthStateProvider)authenticationProvider)
                .RestoreAuthenticationState(apiConnection, middlewareClient, userConfig);

            if (refreshSuccess)
            {
                Log.WriteAudit(kLogCategory, $"Successfully restored session for User \"{userConfig.User.Name}\" with DN: \"{userConfig.User.Dn}\".");
            }
            else
            {
                Log.WriteAudit(kLogCategory, $"Failed to restore session for User \"{userConfig.User.Name}\" with DN: \"{userConfig.User.Dn}\".");
            }
        }
    }
}

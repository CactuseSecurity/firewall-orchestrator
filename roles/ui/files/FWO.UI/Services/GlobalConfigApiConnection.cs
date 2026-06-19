using FWO.Api.Client;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Holds the API connection owned by the singleton global configuration instance.
    /// </summary>
    public class GlobalConfigApiConnection
    {
        /// <summary>
        /// Creates a holder for the singleton global configuration API connection.
        /// </summary>
        /// <param name="apiConnection">API connection used by the singleton global configuration.</param>
        public GlobalConfigApiConnection(ApiConnection apiConnection)
        {
            ApiConnection = apiConnection;
        }

        /// <summary>
        /// API connection used by the singleton global configuration.
        /// </summary>
        public ApiConnection ApiConnection { get; }
    }
}

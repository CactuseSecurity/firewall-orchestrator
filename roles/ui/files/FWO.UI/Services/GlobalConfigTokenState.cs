using FWO.Data.Middleware;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Stores the current anonymous token pair used by the singleton global configuration subscription.
    /// </summary>
    public class GlobalConfigTokenState
    {
        /// <summary>
        /// Creates token state initialized with the token used during UI startup.
        /// </summary>
        /// <param name="initialTokenPair">Initial anonymous token pair.</param>
        public GlobalConfigTokenState(TokenPair initialTokenPair)
        {
            CurrentTokenPair = initialTokenPair;
        }

        /// <summary>
        /// Current anonymous token pair for the singleton global configuration subscription.
        /// </summary>
        public TokenPair CurrentTokenPair { get; set; }
    }
}

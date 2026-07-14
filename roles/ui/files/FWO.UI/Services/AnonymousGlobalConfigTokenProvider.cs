using FWO.Data.Middleware;
using FWO.Middleware.Client;
using RestSharp;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Supplies anonymous bootstrap token pairs for the singleton global configuration subscription.
    /// </summary>
    public interface IAnonymousGlobalConfigTokenProvider
    {
        /// <summary>
        /// Requests a fresh anonymous bootstrap token pair from the middleware.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>A fresh anonymous token pair.</returns>
        Task<TokenPair> CreateTokenPairAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Middleware-backed anonymous token provider for the singleton global configuration subscription.
    /// </summary>
    public class AnonymousGlobalConfigTokenProvider : IAnonymousGlobalConfigTokenProvider, IDisposable
    {
        private readonly MiddlewareClient middlewareClient;
        private bool disposed;

        /// <summary>
        /// Creates a provider for anonymous bootstrap token pairs.
        /// </summary>
        /// <param name="middlewareServerUri">Base URI of the FWO middleware server.</param>
        public AnonymousGlobalConfigTokenProvider(string middlewareServerUri)
        {
            middlewareClient = new MiddlewareClient(middlewareServerUri);
        }

        /// <inheritdoc />
        public async Task<TokenPair> CreateTokenPairAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            RestResponse<TokenPair> response = await middlewareClient.CreateInitialJWT(cancellationToken);
            TokenPair? tokenPair = response.Data ?? TokenPairResponseParser.Parse(response, "Global Config Token Refresh");

            if (!response.IsSuccessful || tokenPair == null || string.IsNullOrWhiteSpace(tokenPair.AccessToken))
            {
                throw new InvalidOperationException($"Could not create anonymous global config token: {response.ErrorMessage ?? response.Content}");
            }

            return tokenPair;
        }

        /// <summary>
        /// Releases the resources used by this token provider.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                middlewareClient.Dispose();
            }

            disposed = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

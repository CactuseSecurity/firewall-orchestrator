namespace FWO.Ui.Services
{
    /// <summary>
    /// Coordinates UI token refresh lifetimes across Blazor circuits.
    /// </summary>
    public interface ITokenRefreshCoordinator : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Starts or joins the shared refresh loop for the current browser session.
        /// </summary>
        /// <returns>A task that represents the asynchronous start operation.</returns>
        Task StartAsync();

        /// <summary>
        /// Stops the current circuit's participation in the shared refresh loop.
        /// Components disposing the coordinator on the render dispatcher must use this method instead of
        /// <see cref="IDisposable.Dispose"/>, which can only wait for a running refresh for a limited time.
        /// </summary>
        /// <returns>A task that represents the asynchronous stop operation.</returns>
        Task StopAsync();
    }
}

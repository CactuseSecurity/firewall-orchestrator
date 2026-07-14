namespace FWO.Ui.Services
{
    /// <summary>
    /// Coordinates UI token refresh lifetimes across Blazor circuits.
    /// </summary>
    public interface ITokenRefreshCoordinator : IDisposable
    {
        /// <summary>
        /// Starts or joins the shared refresh loop for the current browser session.
        /// </summary>
        /// <returns>A task that represents the asynchronous start operation.</returns>
        Task StartAsync();

        /// <summary>
        /// Stops the current circuit's participation in the shared refresh loop.
        /// </summary>
        void Stop();
    }
}
